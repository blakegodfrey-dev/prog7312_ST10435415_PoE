using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartX.Application.Live;
using SmartX.Application.Operations;
using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;
namespace SmartX.Infrastructure.Operations;

/// <summary>One gateway operator history. A semaphore serializes command/Undo and
/// hydration. SQL is authoritative; eligible entries rebuild the actual Stack after restart.</summary>
public sealed class CommandService(IServiceScopeFactory scopes, TelemetryDispatcher dispatcher,
    LiveTelemetryStore live, IncidentTracker incidents, TimeProvider clock, IOptions<OperationsOptions> options)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Stack<CommandHistoryEntry> undo = new();
    private bool initialized;
    public async Task<int> UndoCountAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { await HydrateAsync(token); return undo.Count; }
        finally { gate.Release(); }
    }
    public async Task<CommandHistoryEntry> ExecuteAsync(Guid id, Guid target, bool desired, bool simulateFailure, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { return await ExecuteLockedAsync(id, target, desired, simulateFailure, null, token); }
        finally { gate.Release(); }
    }
    public async Task<CommandHistoryEntry?> UndoAsync(Guid id, bool simulateFailure, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var repeated = await scope.ServiceProvider.GetRequiredService<SmartXDbContext>().CommandHistory.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, token);
            if (repeated is not null) return repeated.IsUndo ? repeated : throw new CommandRejectedException("Identifier already belongs to a manual command.");
            await HydrateAsync(token);
            if (!undo.TryPeek(out var entry)) return null;
            // Never overwrite a state changed by subsequent real device telemetry.
            if (live.FindById(entry.SensorId)?.LatestReading?.BooleanValue != entry.DesiredState)
                return await FailedAsync(id, entry.SensorId, entry.PreviousState!.Value, entry, "Actuator state differs from the command being undone.", token);
            var result = await ExecuteLockedAsync(id, entry.SensorId, entry.PreviousState!.Value, simulateFailure, entry, token);
            if (result.Successful) undo.Pop();
            return result;
        }
        finally { gate.Release(); }
    }
    private async Task<CommandHistoryEntry> ExecuteLockedAsync(Guid id, Guid target, bool desired, bool simulateFailure,
        CommandHistoryEntry? original, CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartXDbContext>();
        var repeated = await db.CommandHistory.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, token);
        if (repeated is not null)
        {
            if (repeated.SensorId != target || repeated.DesiredState != desired || repeated.IsUndo != (original is not null))
                throw new CommandRejectedException("Command identifier was reused with a different payload.");
            return repeated;
        }
        await HydrateAsync(token);
        var device = live.FindById(target) ?? throw new CommandRejectedException("Unknown target device.");
        if (device.Category != SensorCategory.Actuator || device.ValueKind != TelemetryValueKind.Boolean)
            return await FailedAsync(id, target, desired, original, "Only registered Boolean actuators support set-state commands.", token);
        if (incidents.GetConnection(target).State != "Connected" || device.LatestReading is not { IsValid: true, BooleanValue: not null })
            return await FailedAsync(id, target, desired, original, "Actuator is unavailable or its actual state is unknown.", token);
        if (device.LatestReading.BooleanValue == desired)
            return await FailedAsync(id, target, desired, original, "Actuator already has the requested state.", token);
        if (simulateFailure && !options.Value.AllowFaultInjection) throw new CommandRejectedException("Fault injection is disabled.");
        if (simulateFailure) return await FailedAsync(id, target, desired, original, "Simulated actuator acknowledgement timed out.", token);
        var now = clock.GetUtcNow();
        if (device.LatestReading.RecordedAtUtc >= now)
            return await FailedAsync(id, target, desired, original, "Latest device timestamp is ahead of the gateway; correct the device clock first.", token);
        var record = TelemetryRecord.FromPacket(new TelemetryPacket<bool>(Guid.NewGuid(), target, desired, now, now));
        var command = new CommandHistoryEntry { Id = id, SensorId = target, DesiredState = desired,
            PreviousState = device.LatestReading.BooleanValue, Successful = true, IsUndo = original is not null, UndoOfId = original?.Id,
            AtUtc = now, Message = "Simulated ESP32 acknowledged state; telemetry committed.", Context = ContextFor(device, original is not null), TelemetryId = record.Id };
        try { await dispatcher.SubmitAsync(new[] { record }, token, command); }
        catch (CommandRejectedException error) { return await FailedAsync(id, target, desired, original, error.Message, CancellationToken.None); }
        catch (DbUpdateException) { return await FailedAsync(id, target, desired, original, "Persistence failed; actuator state was not committed.", CancellationToken.None); }
        if (original is null) undo.Push(command); // O(1), only after durable acknowledgement.
        return command;
    }
    private async Task<CommandHistoryEntry> FailedAsync(Guid id, Guid target, bool desired, CommandHistoryEntry? original, string reason, CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartXDbContext>();
        var failed = new CommandHistoryEntry { Id = id, SensorId = target, DesiredState = desired, PreviousState = live.FindById(target)?.LatestReading?.BooleanValue,
            AtUtc = clock.GetUtcNow(), Message = reason, Context = ContextFor(live.FindById(target), original is not null), IsUndo = original is not null, UndoOfId = original?.Id };
        db.CommandHistory.Add(failed); await db.SaveChangesAsync(token); return failed;
    }
    private async Task HydrateAsync(CancellationToken token)
    {
        if (initialized) return;
        await using var scope = scopes.CreateAsyncScope();
        var entries = await scope.ServiceProvider.GetRequiredService<SmartXDbContext>().CommandHistory.AsNoTracking()
            .Where(c => c.Successful && !c.IsUndo && !c.Undone).OrderBy(c => c.Sequence).ThenBy(c => c.AtUtc).ThenBy(c => c.Id).ToListAsync(token);
        undo.Clear(); foreach (var entry in entries) undo.Push(entry); initialized = true;
    }
    public string ContextFor(DeviceSnapshot? target, bool isUndo = false)
    {
        if (isUndo) return "undo";
        // Prefer local context; a facility-wide incident still explains searches
        // for a remote circulation pump serving a grow bed on another node.
        var anomaly = incidents.Active().Where(i => i.Type == "OutOfRange")
            .OrderByDescending(i => live.FindById(i.DeviceId)?.DeploymentNodeId == target?.DeploymentNodeId)
            .ThenByDescending(i => i.Severity == "critical").ThenByDescending(i => i.LastObservedAtUtc).ThenBy(i => i.DeviceId).FirstOrDefault();
        return TelemetryContext.For(anomaly is null ? null : live.FindById(anomaly.DeviceId)) ?? "normal";
    }
}
