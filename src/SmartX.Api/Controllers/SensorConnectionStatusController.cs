using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Application.Live;
using SmartX.Application.Operations;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Live;
using SmartX.Infrastructure.Operations;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/sensors/{sensorId:guid}/connection-status")]
public sealed class SensorConnectionStatusController : ControllerBase
{
    private readonly LiveTelemetryService _live;
    private readonly IncidentTracker _incidents;
    private readonly TimeProvider _timeProvider;

    [ActivatorUtilitiesConstructor]
    public SensorConnectionStatusController(SmartXDbContext context, LiveTelemetryService live,
        IncidentTracker incidents, TimeProvider timeProvider)
    {
        _live = live;
        _incidents = incidents;
        _timeProvider = timeProvider;
    }

    public SensorConnectionStatusController(SmartXDbContext context, TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        var defaults = new OperationsOptions();
        _incidents = new IncidentTracker(_timeProvider, TimeSpan.FromSeconds(defaults.StaleSeconds),
            TimeSpan.FromSeconds(defaults.DisconnectedSeconds));
        _live = new LiveTelemetryService(context, new LiveTelemetryStore(), _incidents);
    }

    [HttpGet]
    public async Task<ActionResult<SensorConnectionStatusResponse>> Get(Guid sensorId, CancellationToken cancellationToken)
    {
        await _live.EnsureInitializedAsync(cancellationToken);
        var device = _live.Store.FindById(sensorId);
        if (device is null)
            return NotFound(new ProblemDetails { Title = "Sensor not found.",
                Detail = $"No sensor with identifier '{sensorId}' exists.", Status = StatusCodes.Status404NotFound });

        var connection = _incidents.GetConnection(sensorId);
        var now = _timeProvider.GetUtcNow();
        var recorded = device.LatestReading?.RecordedAtUtc;
        return Ok(new SensorConnectionStatusResponse(sensorId,
            Enum.Parse<SensorConnectionStatus>(connection.State), recorded, now,
            recorded.HasValue ? Math.Max(0, (now - recorded.Value).TotalSeconds) : null,
            _incidents.StaleAfter.TotalMinutes, _incidents.DisconnectedAfter.TotalMinutes,
            connection.LastSeenAtUtc,
            connection.LastSeenAtUtc.HasValue ? Math.Max(0, (now - connection.LastSeenAtUtc.Value).TotalSeconds) : null,
            _incidents.StaleAfter.TotalSeconds, _incidents.DisconnectedAfter.TotalSeconds));
    }
}
