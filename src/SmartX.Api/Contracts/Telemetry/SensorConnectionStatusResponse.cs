using System.Text.Json.Serialization;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Telemetry;

public sealed record SensorConnectionStatusResponse(
    Guid SensorId,
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<SensorConnectionStatus>))]
    SensorConnectionStatus Status,
    DateTimeOffset? LastRecordedAtUtc,
    DateTimeOffset EvaluatedAtUtc,
    double? SecondsSinceLastReading,
    int ConnectedThresholdMinutes,
    int DisconnectedThresholdMinutes);
