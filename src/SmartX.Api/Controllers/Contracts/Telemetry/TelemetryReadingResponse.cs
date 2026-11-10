using System.Text.Json.Serialization;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Telemetry;

public sealed record TelemetryReadingResponse(
    Guid Id,
    Guid SensorId,
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<TelemetryValueKind>))]
    TelemetryValueKind ValueKind,
    float? FloatValue,
    int? IntegerValue,
    bool? BooleanValue,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset ReceivedAtUtc,
    bool IsValid,
    string? ValidationMessage);
