using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Live;
using SmartX.Application.Live;
using SmartX.Application.Operations;
using SmartX.Infrastructure.Live;
using SmartX.Infrastructure.Operations;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;
namespace SmartX.Api.Controllers;

public sealed record SetStateRequest([Required] Guid Id, [Required] Guid SensorId, [Required] bool? DesiredState, bool SimulateFailure = false);
public sealed record UndoRequest([Required] Guid Id, bool SimulateFailure = false);
public sealed record InteractionRequest(Guid Id, Guid TargetId, [Required] string Kind, [MaxLength(150)] string? Query);
public sealed record IncidentNoteRequest([Required] string Label, [MaxLength(500)] string? Note);

[ApiController]
[Route("api/operations")]
public sealed class OperationsController(LiveTelemetryService service, LiveTelemetryStore live, IncidentTracker incidents,
    TelemetryWorkQueue<TelemetryWork> queue, TelemetryDispatcher dispatcher, CommandService commands,
    SmartXDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken token)
    {
        await service.EnsureInitializedAsync(token);
        incidents.Sweep(live.GetDevices().Select(d => d.Id));
        var devices = live.GetDevices();
        var recent = live.GetRecentHistory(limit: 500);
        return Ok(new { Devices = devices.Select(d => new { Device = LiveDeviceResponse.FromSnapshot(d), Connection = incidents.GetConnection(d.Id) }),
            ActiveIncidents = incidents.Active(), Events = incidents.Events(), Processing = queue.Status(),
            UndoCount = await commands.UndoCountAsync(token),
            Commands = await db.CommandHistory.AsNoTracking().OrderByDescending(c => c.Sequence).Take(100).ToListAsync(token),
            History = recent.Readings.Select(LiveTelemetryReadingResponse.FromSnapshot), RecentCapacity = recent.Capacity,
            StaleSeconds = incidents.StaleAfter.TotalSeconds, DisconnectedSeconds = incidents.DisconnectedAfter.TotalSeconds,
            Suggestions = await SuggestionsAsync(devices, token) });
    }
    [HttpPost("heartbeat/{sensorId:guid}")]
    public async Task<IActionResult> Heartbeat(Guid sensorId, CancellationToken token)
    {
        await service.EnsureInitializedAsync(token);
        if (live.FindById(sensorId) is null) return NotFound();
        try { await dispatcher.SubmitAsync(Array.Empty<TelemetryRecord>(), token, heartbeatId: sensorId); return Ok(incidents.GetConnection(sensorId)); }
        catch (QueueFullException) { return Problem(statusCode: 429, title: "Gateway queue full; heartbeat was not accepted."); }
    }
    [HttpPost("commands")]
    public async Task<IActionResult> Command(SetStateRequest request, CancellationToken token)
    {
        if (request.Id == Guid.Empty || request.SensorId == Guid.Empty) return Problem(statusCode: 400, title: "Command and device identifiers are required.");
        await service.EnsureInitializedAsync(token);
        if (live.FindById(request.SensorId) is null) return NotFound();
        try { return Ok(await commands.ExecuteAsync(request.Id, request.SensorId, request.DesiredState!.Value, request.SimulateFailure, token)); }
        catch (QueueFullException) { return Problem(statusCode: 429, title: "Gateway queue full; command was not accepted."); }
        catch (CommandRejectedException error) { return Problem(statusCode: 409, title: "Command rejected", detail: error.Message); }
    }
    [HttpPost("undo")]
    public async Task<IActionResult> Undo(UndoRequest request, CancellationToken token)
    {
        if (request.Id == Guid.Empty) return Problem(statusCode: 400, title: "Undo identifier is required.");
        await service.EnsureInitializedAsync(token);
        try
        {
            var result = await commands.UndoAsync(request.Id, request.SimulateFailure, token);
            return result is null ? Problem(statusCode: 409, title: "There is no successful command to undo.") : Ok(result);
        }
        catch (QueueFullException) { return Problem(statusCode: 429, title: "Gateway queue full; Undo was not accepted."); }
        catch (CommandRejectedException error) { return Problem(statusCode: 409, title: "Undo rejected", detail: error.Message); }
    }
    [HttpPut("incidents/{id:guid}")]
    public IActionResult Annotate(Guid id, IncidentNoteRequest request) => incidents.Annotate(id, request.Label, request.Note)
        ? Ok() : Problem(statusCode: 400, title: "Unknown active incident or unsupported label.");
    [HttpPost("interactions")]
    public async Task<IActionResult> Interaction(InteractionRequest request, CancellationToken token)
    {
        if (request.Id == Guid.Empty || request.TargetId == Guid.Empty || request.Kind is not ("search" or "view") ||
            (request.Kind == "search" && string.IsNullOrWhiteSpace(request.Query))) return Problem(statusCode: 400, title: "A submitted search or device view is required.");
        await service.EnsureInitializedAsync(token);
        var target = live.FindById(request.TargetId);
        if (target is null) return NotFound();
        var existing = await db.Interactions.AsNoTracking().SingleOrDefaultAsync(i => i.Id == request.Id, token);
        if (existing is not null) return existing.TargetId == request.TargetId && existing.Kind == request.Kind && existing.Query == (request.Query?.Trim() ?? "")
            ? Ok() : Problem(statusCode: 409, title: "Interaction identifier reused with a different payload.");
        db.Interactions.Add(new() { Id = request.Id, TargetId = request.TargetId, Kind = request.Kind, Query = request.Query?.Trim() ?? "",
            Context = commands.ContextFor(target), AtUtc = clock.GetUtcNow() });
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateException) { return Problem(statusCode: 409, title: "Interaction conflicted; refresh before retrying."); }
        return Ok();
    }
    private async Task<IReadOnlyList<SuggestedAction>> SuggestionsAsync(IReadOnlyList<DeviceSnapshot> devices, CancellationToken token)
    {
        // Bounded rolling durable evidence; failed/Undo commands never teach successful actions.
        var actions = await db.CommandHistory.AsNoTracking().Where(c => c.Successful && !c.IsUndo)
            .OrderByDescending(c => c.Sequence).Take(5000).ToListAsync(token);
        var interactions = await db.Interactions.AsNoTracking().OrderByDescending(i => i.AtUtc).Take(5000).ToListAsync(token);
        var evidence = actions.Select(c => new BehaviourEvidence(c.Id, c.Context, c.SensorId, "command", c.DesiredState, true))
            .Concat(interactions.Select(i => new BehaviourEvidence(i.Id, i.Context, i.TargetId, i.Kind, null, true)));
        var contexts = incidents.Active().Where(i => i.Type == "OutOfRange").Select(i => TelemetryContext.For(live.FindById(i.DeviceId))).Where(c => c is not null).Select(c => c!).ToHashSet();
        // "normal" searches are retained but do not generate perpetual context-free tips.
        var unavailable = devices.Where(d => incidents.GetConnection(d.Id).State != "Connected").Select(d => d.Id).ToHashSet();
        return SuggestionEngine.Learn(evidence, devices, unavailable, contexts);
    }
}
