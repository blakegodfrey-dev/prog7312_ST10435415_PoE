using System.Text.Json.Serialization;

namespace SmartX.Api.Contracts.Telemetry;

public sealed record TelemetryIngestionRequest<T>(
    Guid Id,
    Guid SensorId,
    [property: JsonRequired] T Value,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset? ReceivedAtUtc)
    where T : struct;
