namespace SmartX.Api.Contracts.Telemetry;

public sealed record TelemetryIngestionRequest<T>(
    Guid Id,
    Guid SensorId,
    T Value,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset? ReceivedAtUtc)
    where T : struct;
