using SmartX.Domain.Enums;

namespace SmartX.Application.Live;

/// <summary>Native typed columns; no object-valued telemetry or string coercion.</summary>
public sealed record TelemetrySnapshot(
    Guid Id,
    Guid SensorId,
    string MacAddress,
    TelemetryValueKind ValueKind,
    float? FloatValue,
    int? IntegerValue,
    bool? BooleanValue,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset ReceivedAtUtc,
    bool IsValid,
    string? ValidationMessage);
