using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Application.Telemetry;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/sensors/{sensorId:guid}/connection-status")]
public sealed class SensorConnectionStatusController : ControllerBase
{
    private readonly SmartXDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SensorConnectionStatusController(
        SmartXDbContext context,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    [HttpGet]
    public async Task<ActionResult<SensorConnectionStatusResponse>> Get(
        Guid sensorId,
        CancellationToken cancellationToken)
    {
        var sensorExists = await _context.Sensors
            .AsNoTracking()
            .AnyAsync(
                sensor => sensor.Id == sensorId,
                cancellationToken);

        if (!sensorExists)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Sensor not found.",
                Detail = $"No sensor with identifier '{sensorId}' exists.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var lastRecordedAtUtc = await _context.TelemetryRecords
            .AsNoTracking()
            .Where(record => record.SensorId == sensorId)
            .Select(record =>
                (DateTimeOffset?)record.RecordedAtUtc)
            .MaxAsync(cancellationToken);

        var evaluatedAtUtc = _timeProvider.GetUtcNow();
        var status = SensorConnectionStatusEvaluator.Evaluate(
            lastRecordedAtUtc,
            evaluatedAtUtc);

        var secondsSinceLastReading = lastRecordedAtUtc.HasValue
            ? Math.Max(
                0,
                (evaluatedAtUtc - lastRecordedAtUtc.Value).TotalSeconds)
            : (double?)null;

        return Ok(new SensorConnectionStatusResponse(
            sensorId,
            status,
            lastRecordedAtUtc,
            evaluatedAtUtc,
            secondsSinceLastReading,
            (int)SensorConnectionStatusEvaluator
                .ConnectedThreshold.TotalMinutes,
            (int)SensorConnectionStatusEvaluator
                .DisconnectedThreshold.TotalMinutes));
    }
}
