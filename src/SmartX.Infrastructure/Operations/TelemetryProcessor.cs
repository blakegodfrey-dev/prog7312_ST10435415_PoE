using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartX.Application.Live;
using SmartX.Application.Operations;
using SmartX.Infrastructure.Live;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Infrastructure.Operations;

public sealed class OperationsOptions
{
    public int QueueCapacity { get; set; } = 5000;
    public int StaleSeconds { get; set; } = 30;
    public int DisconnectedSeconds { get; set; } = 90;
    public int ScanSeconds { get; set; } = 5;
    public int ProcessingDelayMilliseconds { get; set; }
    public bool AllowFaultInjection { get; set; }
}
public sealed class QueueFullException : Exception { }
public sealed class CommandRejectedException(string message) : Exception(message);
public sealed record TelemetryWork(IReadOnlyList<TelemetryRecord> Records, DateTimeOffset GatewayAt,
    TaskCompletionSource Completion, CommandHistoryEntry? Command = null, Guid? HeartbeatId = null);

public sealed class TelemetryDispatcher(TelemetryWorkQueue<TelemetryWork> queue, LiveTelemetryStore live,
    IncidentTracker incidents, TimeProvider clock)
{
    public Task SubmitAsync(IReadOnlyList<TelemetryRecord> records, CancellationToken token,
        CommandHistoryEntry? command = null, Guid? heartbeatId = null)
    {
        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var severity = records.Select(r => TelemetrySeverity.Classify(live.FindById(r.SensorId)!, Snapshot(r, live.FindById(r.SensorId)!.MacAddress))).DefaultIfEmpty(0).Max();
        var work = new TelemetryWork(records, clock.GetUtcNow(), completion, command, heartbeatId);
        if (!queue.TryEnqueue(work, Math.Max(1, records.Count), severity)) throw new QueueFullException();
        foreach (var id in records.Select(r => r.SensorId).Concat(heartbeatId is { } h ? new[] { h } : Array.Empty<Guid>()).Distinct()) incidents.ObservePacket(id);
        // Once accepted, client cancellation cannot discard work. The HTTP response
        // awaits a definitive commit/failure; shutdown completes queued callers explicitly.
        return completion.Task;
    }
    internal static TelemetrySnapshot Snapshot(TelemetryRecord r, string mac) => new(r.Id, r.SensorId, mac, r.ValueKind,
        r.FloatValue, r.IntegerValue, r.BooleanValue, r.RecordedAtUtc, r.ReceivedAtUtc, r.IsValid, r.ValidationMessage);
}

public sealed class TelemetryProcessor(IServiceScopeFactory scopes, TelemetryWorkQueue<TelemetryWork> queue,
    LiveTelemetryStore live, IncidentTracker incidents, IOptions<OperationsOptions> options,
    TimeProvider clock, ILogger<TelemetryProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var work = await queue.TakeAsync(stoppingToken);
                try
                {
                    if (options.Value.ProcessingDelayMilliseconds > 0)
                        await Task.Delay(TimeSpan.FromMilliseconds(options.Value.ProcessingDelayMilliseconds), clock, stoppingToken);
                    await PersistAsync(work, stoppingToken);
                    work.Completion.TrySetResult(); queue.Finish(true);
                }
                catch (Exception error)
                {
                    logger.LogError(error, "Telemetry work failed; no uncommitted readings were published.");
                    work.Completion.TrySetException(error); queue.Finish(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            queue.StopAccepting();
            while (queue.Status().NormalCount + queue.Status().PriorityCount > 0)
            {
                var pending = await queue.TakeAsync(CancellationToken.None);
                pending.Completion.TrySetException(new CommandRejectedException("Gateway stopped before persistence; retry using the same identifiers."));
                queue.Finish(false);
            }
        }
    }

    public async Task PersistAsync(TelemetryWork work, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SmartXDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<LiveTelemetryService>();
                await service.EnsureInitializedAsync(token);
                var ids = work.Records.Select(r => r.Id).ToArray();
                var existing = await db.TelemetryRecords.AsNoTracking().Where(r => ids.Contains(r.Id)).ToListAsync(token);
                var committed = attempt > 0 && existing.Count == ids.Length && ids.Length > 0 && existing.All(e => work.Records.Any(r => SameReading(e, r)));
                if (existing.Count > 0 && !committed) throw new DbUpdateException("Telemetry identifiers already exist.");
                if (!committed)
                {
                    if (work.Command is { } command)
                    {
                        var current = live.FindById(command.SensorId);
                        if (incidents.GetConnection(command.SensorId).State != "Connected" || current?.LatestReading?.BooleanValue != command.PreviousState || current?.LatestReading?.RecordedAtUtc >= work.Records[0].RecordedAtUtc)
                            throw new CommandRejectedException("Actuator changed or disconnected before acknowledgement. Refresh and retry.");
                        if (command.IsUndo)
                        {
                            var original = await db.CommandHistory.SingleAsync(c => c.Id == command.UndoOfId, token);
                            if (original.Undone) throw new CommandRejectedException("This command was already undone.");
                            original.Undone = true;
                        }
                        if (attempt > 0) command.Sequence = 0; // Retry a rolled-back identity allocation.
                        db.CommandHistory.Add(command);
                    }
                    db.TelemetryRecords.AddRange(work.Records);
                    foreach (var id in work.Records.Select(r => r.SensorId).Concat(work.HeartbeatId is { } h ? new[] { h } : Array.Empty<Guid>()).Distinct())
                    {
                        var receipt = await db.GatewayReceipts.FindAsync(new object[] { id }, token);
                        if (receipt is null) db.GatewayReceipts.Add(new() { SensorId = id, LastSeenAtUtc = work.GatewayAt });
                        else if (work.GatewayAt > receipt.LastSeenAtUtc) receipt.LastSeenAtUtc = work.GatewayAt;
                    }
                    // EF's single SaveChanges transaction atomically commits the complete
                    // telemetry batch, successful command and Undo marker together.
                    await db.SaveChangesAsync(token);
                }
                service.PublishCommitted(work.Records);
                foreach (var id in work.Records.Select(r => r.SensorId).Distinct()) incidents.ObserveReading(live.FindById(id)!);
                return;
            }
            catch (DbUpdateException error) when (attempt < 2 && error.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
                (sql.Number is -2 or 1205 or 40613 or 40197 or 40501))
            {
                logger.LogWarning(error, "Transient persistence failure; retry {Attempt} with identical reading identifiers.", attempt + 1);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), clock, token);
            }
        }
    }
    private static bool SameReading(TelemetryRecord a, TelemetryRecord b) => a.Id == b.Id && a.SensorId == b.SensorId &&
        a.ValueKind == b.ValueKind && a.FloatValue == b.FloatValue && a.IntegerValue == b.IntegerValue && a.BooleanValue == b.BooleanValue &&
        a.RecordedAtUtc == b.RecordedAtUtc && a.ReceivedAtUtc == b.ReceivedAtUtc && a.IsValid == b.IsValid && a.ValidationMessage == b.ValidationMessage;
}

public sealed class ConnectionMonitor(IServiceScopeFactory scopes, LiveTelemetryStore live, IncidentTracker incidents,
    IOptions<OperationsOptions> options, TimeProvider clock, ILogger<ConnectionMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<LiveTelemetryService>().EnsureInitializedAsync(token);
                incidents.Sweep(live.GetDevices().Select(d => d.Id));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning(error, "Connection monitor will retry initialization."); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.ScanSeconds), clock, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
        }
    }
}
