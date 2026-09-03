using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Application.Telemetry;
using SmartX.Domain.Entities;
using SmartX.Domain.Telemetry;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TelemetryController : ControllerBase
{
    private const int MaximumPageSize = 500;

    private static readonly Expression<
        Func<TelemetryRecord, TelemetryReadingResponse>>
        ResponseProjection = record => new TelemetryReadingResponse(
            record.Id,
            record.SensorId,
            record.ValueKind,
            record.FloatValue,
            record.IntegerValue,
            record.BooleanValue,
            record.RecordedAtUtc,
            record.ReceivedAtUtc,
            record.IsValid,
            record.ValidationMessage);

    private readonly SmartXDbContext _context;

    public TelemetryController(SmartXDbContext context)
    {
        _context = context;
    }

    [HttpPost("float")]
    public Task<ActionResult<TelemetryReadingResponse>> IngestFloat(
        TelemetryIngestionRequest<float> request,
        CancellationToken cancellationToken)
    {
        return Ingest(
            request,
            ValidateNumericValue,
            (packet, isValid, message) => TelemetryRecord.FromPacket(
                packet,
                isValid,
                message),
            cancellationToken);
    }

    [HttpPost("integer")]
    public Task<ActionResult<TelemetryReadingResponse>> IngestInteger(
        TelemetryIngestionRequest<int> request,
        CancellationToken cancellationToken)
    {
        return Ingest(
            request,
            ValidateNumericValue,
            (packet, isValid, message) => TelemetryRecord.FromPacket(
                packet,
                isValid,
                message),
            cancellationToken);
    }

    [HttpPost("boolean")]
    public Task<ActionResult<TelemetryReadingResponse>> IngestBoolean(
        TelemetryIngestionRequest<bool> request,
        CancellationToken cancellationToken)
    {
        return Ingest(
            request,
            ValidateBooleanValue,
            (packet, isValid, message) => TelemetryRecord.FromPacket(
                packet,
                isValid,
                message),
            cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TelemetryReadingResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var record = await _context.TelemetryRecords
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(ResponseProjection)
            .SingleOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            return NotFoundError(
                $"No telemetry record with identifier '{id}' exists.");
        }

        return Ok(record);
    }

    [HttpGet("sensors/{sensorId:guid}")]
    public async Task<ActionResult<TelemetryHistoryResponse>> GetHistory(
        Guid sensorId,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] bool? isValid = null,
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

        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            return ValidationError(
                nameof(fromUtc),
                "The start timestamp cannot be after the end timestamp.");
        }

        var sensor = await _context.Sensors
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == sensorId,
                cancellationToken);

        if (sensor is null)
        {
            return NotFoundError(
                $"No sensor with identifier '{sensorId}' exists.");
        }

        var query = _context.TelemetryRecords
            .AsNoTracking()
            .Where(record => record.SensorId == sensorId);

        if (fromUtc.HasValue)
        {
            var normalisedFromUtc = fromUtc.Value.ToUniversalTime();
            query = query.Where(record =>
                record.RecordedAtUtc >= normalisedFromUtc);
        }

        if (toUtc.HasValue)
        {
            var normalisedToUtc = toUtc.Value.ToUniversalTime();
            query = query.Where(record =>
                record.RecordedAtUtc <= normalisedToUtc);
        }

        if (isValid.HasValue)
        {
            query = query.Where(record =>
                record.IsValid == isValid.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var readings = await query
            .OrderByDescending(record => record.RecordedAtUtc)
            .ThenByDescending(record => record.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ResponseProjection)
            .ToListAsync(cancellationToken);

        return Ok(new TelemetryHistoryResponse(
            sensor.Id,
            sensor.FriendlyName,
            sensor.ValueKind,
            sensor.Unit,
            page,
            pageSize,
            totalCount,
            readings));
    }

    private async Task<ActionResult<TelemetryReadingResponse>> Ingest<T>(
        TelemetryIngestionRequest<T> request,
        Func<Sensor, T, ValueValidationResult> validateValue,
        Func<TelemetryPacket<T>, bool, string?, TelemetryRecord> createRecord,
        CancellationToken cancellationToken)
        where T : struct
    {
        if (request.ReceivedAtUtc.HasValue &&
            request.ReceivedAtUtc.Value < request.RecordedAtUtc)
        {
            return ValidationError(
                nameof(request.ReceivedAtUtc),
                "The received timestamp cannot precede the recorded timestamp.");
        }

        var sensor = await _context.Sensors
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.SensorId,
                cancellationToken);

        if (sensor is null)
        {
            return NotFoundError(
                $"No sensor with identifier '{request.SensorId}' exists.");
        }

        if (await _context.TelemetryRecords.AnyAsync(
                record => record.Id == request.Id,
                cancellationToken))
        {
            return ConflictError(
                $"Telemetry identifier '{request.Id}' already exists.");
        }

        TelemetryPacket<T> packet;

        try
        {
            packet = new TelemetryPacket<T>(
                request.Id,
                request.SensorId,
                request.Value,
                request.RecordedAtUtc,
                request.ReceivedAtUtc);

            TelemetryPacketTypeGuard.EnsureCompatible(sensor, packet);
        }
        catch (ArgumentException exception)
        {
            return ValidationError("telemetry", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ValidationError("telemetry", exception.Message);
        }

        var validation = validateValue(sensor, request.Value);
        var record = createRecord(
            packet,
            validation.IsValid,
            validation.Message);

        _context.TelemetryRecords.Add(record);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ConflictError(
                "The telemetry record conflicts with an existing reading.");
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = record.Id },
            ToResponse(record));
    }

    private static ValueValidationResult ValidateNumericValue(
        Sensor sensor,
        float value)
    {
        return ValidateNumericValue(sensor, (double)value);
    }

    private static ValueValidationResult ValidateNumericValue(
        Sensor sensor,
        int value)
    {
        return ValidateNumericValue(sensor, (double)value);
    }

    private static ValueValidationResult ValidateNumericValue(
        Sensor sensor,
        double value)
    {
        if (!sensor.ExpectedMinimum.HasValue ||
            !sensor.ExpectedMaximum.HasValue ||
            (value >= sensor.ExpectedMinimum.Value &&
             value <= sensor.ExpectedMaximum.Value))
        {
            return ValueValidationResult.Valid;
        }

        return new ValueValidationResult(
            false,
            $"Reading {value} {sensor.Unit} is outside the expected range " +
            $"of {sensor.ExpectedMinimum} to {sensor.ExpectedMaximum} " +
            $"{sensor.Unit}.");
    }

    private static ValueValidationResult ValidateBooleanValue(
        Sensor sensor,
        bool value)
    {
        _ = sensor;
        _ = value;

        return ValueValidationResult.Valid;
    }

    private static TelemetryReadingResponse ToResponse(
        TelemetryRecord record)
    {
        return new TelemetryReadingResponse(
            record.Id,
            record.SensorId,
            record.ValueKind,
            record.FloatValue,
            record.IntegerValue,
            record.BooleanValue,
            record.RecordedAtUtc,
            record.ReceivedAtUtc,
            record.IsValid,
            record.ValidationMessage);
    }

    private ActionResult ValidationError(string key, string message)
    {
        return BadRequest(new ValidationProblemDetails(
            new Dictionary<string, string[]>
            {
                [key] = [message]
            })
        {
            Title = "Telemetry request validation failed.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private ActionResult NotFoundError(string detail)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Telemetry resource not found.",
            Detail = detail,
            Status = StatusCodes.Status404NotFound
        });
    }

    private ActionResult ConflictError(string detail)
    {
        return Conflict(new ProblemDetails
        {
            Title = "Telemetry ingestion conflict.",
            Detail = detail,
            Status = StatusCodes.Status409Conflict
        });
    }

    private sealed record ValueValidationResult(
        bool IsValid,
        string? Message)
    {
        public static ValueValidationResult Valid { get; } =
            new(true, null);
    }
}
