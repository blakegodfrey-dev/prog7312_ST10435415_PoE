using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Telemetry.Diagnostics;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;
using SmartX.Application.Telemetry;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/telemetry/diagnostics")]
public sealed class TelemetryDiagnosticsController : ControllerBase
{
    private const int MaximumPageSize = 500;

    private readonly SmartXDbContext _context;
    private readonly TimeProvider _timeProvider;

    public TelemetryDiagnosticsController(
        SmartXDbContext context,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    [HttpGet("health-summary")]
    public async Task<ActionResult<SensorHealthSummaryResponse>>
        GetHealthSummary(
        CancellationToken cancellationToken = default)
    {
        var sensorIds = await _context.Sensors
            .AsNoTracking()
            .Select(sensor => sensor.Id)
            .ToListAsync(cancellationToken);

        var latestReadings = await _context.TelemetryRecords
            .AsNoTracking()
            .GroupBy(record => record.SensorId)
            .Select(group => group
                .OrderByDescending(record => record.RecordedAtUtc)
                .ThenByDescending(record => record.Id)
                .Select(record => new
                {
                    record.SensorId,
                    record.RecordedAtUtc,
                    record.IsValid
                })
                .First())
            .ToListAsync(cancellationToken);

        var latestReadingBySensorId = latestReadings.ToDictionary(
            reading => reading.SensorId);

        var evaluatedAtUtc = _timeProvider.GetUtcNow();
        var connectedSensorCount = 0;
        var staleSensorCount = 0;
        var disconnectedSensorCount = 0;
        var noDataSensorCount = 0;
        var invalidLatestReadingCount = 0;

        foreach (var sensorId in sensorIds)
        {
            if (!latestReadingBySensorId.TryGetValue(
                    sensorId,
                    out var latestReading))
            {
                noDataSensorCount++;
                continue;
            }

            if (!latestReading.IsValid)
            {
                invalidLatestReadingCount++;
            }

            var status = SensorConnectionStatusEvaluator.Evaluate(
                latestReading.RecordedAtUtc,
                evaluatedAtUtc);

            switch (status)
            {
                case SensorConnectionStatus.Connected:
                    connectedSensorCount++;
                    break;

                case SensorConnectionStatus.Stale:
                    staleSensorCount++;
                    break;

                case SensorConnectionStatus.Disconnected:
                    disconnectedSensorCount++;
                    break;

                case SensorConnectionStatus.NoData:
                    noDataSensorCount++;
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported sensor connection status '{status}'.");
            }
        }

        return Ok(new SensorHealthSummaryResponse(
            sensorIds.Count,
            connectedSensorCount,
            staleSensorCount,
            disconnectedSensorCount,
            noDataSensorCount,
            invalidLatestReadingCount,
            evaluatedAtUtc,
            (int)SensorConnectionStatusEvaluator
                .ConnectedThreshold.TotalMinutes,
            (int)SensorConnectionStatusEvaluator
                .DisconnectedThreshold.TotalMinutes));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<TelemetryDiagnosticsSummaryResponse>>
        GetSummary(
            [FromQuery] DateTimeOffset? fromUtc = null,
            [FromQuery] DateTimeOffset? toUtc = null,
            CancellationToken cancellationToken = default)
    {
        if (!IsValidTimeRange(fromUtc, toUtc))
        {
            return TimeRangeValidationError();
        }

        var normalisedFromUtc = fromUtc?.ToUniversalTime();
        var normalisedToUtc = toUtc?.ToUniversalTime();
        var query = ApplyTimeRange(
            _context.TelemetryRecords.AsNoTracking(),
            normalisedFromUtc,
            normalisedToUtc);

        var totalReadings = await query.CountAsync(cancellationToken);
        var invalidReadings = await query.CountAsync(
            record => !record.IsValid,
            cancellationToken);
        var affectedSensorCount = await query
            .Where(record => !record.IsValid)
            .Select(record => record.SensorId)
            .Distinct()
            .CountAsync(cancellationToken);
        var floatReadings = await query.CountAsync(
            record => record.ValueKind == TelemetryValueKind.Float,
            cancellationToken);
        var integerReadings = await query.CountAsync(
            record => record.ValueKind == TelemetryValueKind.Integer,
            cancellationToken);
        var booleanReadings = await query.CountAsync(
            record => record.ValueKind == TelemetryValueKind.Boolean,
            cancellationToken);

        DateTimeOffset? earliestRecordedAtUtc = null;
        DateTimeOffset? latestRecordedAtUtc = null;

        if (totalReadings > 0)
        {
            earliestRecordedAtUtc = await query.MinAsync(
                record => record.RecordedAtUtc,
                cancellationToken);
            latestRecordedAtUtc = await query.MaxAsync(
                record => record.RecordedAtUtc,
                cancellationToken);
        }

        var invalidPercentage = totalReadings == 0
            ? 0
            : invalidReadings * 100d / totalReadings;

        return Ok(new TelemetryDiagnosticsSummaryResponse(
            normalisedFromUtc,
            normalisedToUtc,
            totalReadings,
            totalReadings - invalidReadings,
            invalidReadings,
            invalidPercentage,
            affectedSensorCount,
            floatReadings,
            integerReadings,
            booleanReadings,
            earliestRecordedAtUtc,
            latestRecordedAtUtc));
    }

    [HttpGet("invalid")]
    public async Task<ActionResult<InvalidTelemetryPageResponse>>
        GetInvalidReadings(
            [FromQuery] Guid? sensorId = null,
            [FromQuery] DateTimeOffset? fromUtc = null,
            [FromQuery] DateTimeOffset? toUtc = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return ValidationError(
                nameof(page),
                "Page must be at least one.");
        }

        if (pageSize < 1 || pageSize > MaximumPageSize)
        {
            return ValidationError(
                nameof(pageSize),
                $"Page size must be between 1 and {MaximumPageSize}.");
        }

        if (!IsValidTimeRange(fromUtc, toUtc))
        {
            return TimeRangeValidationError();
        }

        if (sensorId.HasValue &&
            !await _context.Sensors.AnyAsync(
                sensor => sensor.Id == sensorId.Value,
                cancellationToken))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Telemetry resource not found.",
                Detail = $"No sensor with identifier '{sensorId}' exists.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var normalisedFromUtc = fromUtc?.ToUniversalTime();
        var normalisedToUtc = toUtc?.ToUniversalTime();
        var query = ApplyTimeRange(
            _context.TelemetryRecords
                .AsNoTracking()
                .Where(record => !record.IsValid),
            normalisedFromUtc,
            normalisedToUtc);

        if (sensorId.HasValue)
        {
            query = query.Where(record =>
                record.SensorId == sensorId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var readings = await (
                from record in query
                join sensor in _context.Sensors.AsNoTracking()
                    on record.SensorId equals sensor.Id
                join deploymentNode in _context.DeploymentNodes.AsNoTracking()
                    on sensor.DeploymentNodeId equals deploymentNode.Id
                orderby record.RecordedAtUtc descending, record.Id descending
                select new InvalidTelemetryReadingResponse(
                    record.Id,
                    record.SensorId,
                    sensor.FriendlyName,
                    sensor.MeasuredProperty,
                    sensor.Unit,
                    deploymentNode.Id,
                    deploymentNode.Code,
                    record.ValueKind,
                    record.FloatValue,
                    record.IntegerValue,
                    record.BooleanValue,
                    record.RecordedAtUtc,
                    record.ReceivedAtUtc,
                    record.ValidationMessage!))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new InvalidTelemetryPageResponse(
            page,
            pageSize,
            totalCount,
            readings));
    }

    private static IQueryable<TelemetryRecord> ApplyTimeRange(
        IQueryable<TelemetryRecord> query,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        if (fromUtc.HasValue)
        {
            query = query.Where(record =>
                record.RecordedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(record =>
                record.RecordedAtUtc <= toUtc.Value);
        }

        return query;
    }

    private static bool IsValidTimeRange(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        return !fromUtc.HasValue ||
               !toUtc.HasValue ||
               fromUtc.Value <= toUtc.Value;
    }

    private ActionResult TimeRangeValidationError()
    {
        return ValidationError(
            "fromUtc",
            "The start timestamp cannot be after the end timestamp.");
    }

    private ActionResult ValidationError(string key, string message)
    {
        return BadRequest(new ValidationProblemDetails(
            new Dictionary<string, string[]>
            {
                [key] = [message]
            })
        {
            Title = "Telemetry diagnostics validation failed.",
            Status = StatusCodes.Status400BadRequest
        });
    }
}
