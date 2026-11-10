using System.Text.Json.Serialization;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Telemetry.Diagnostics;

public sealed record InvalidTelemetryReadingResponse(
    Guid Id,
    Guid SensorId,
    string SensorName,
    string MeasuredProperty,
    string Unit,
    Guid DeploymentNodeId,
    string DeploymentLocationCode,
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<TelemetryValueKind>))]
    TelemetryValueKind ValueKind,
    float? FloatValue,
    int? IntegerValue,
    bool? BooleanValue,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset ReceivedAtUtc,
    string ValidationMessage);
