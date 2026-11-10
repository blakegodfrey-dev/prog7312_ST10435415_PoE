using System.Text.Json.Serialization;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Sensors;

public sealed record SensorResponse(
    Guid Id,
    string MacAddress,
    string FriendlyName,
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<SensorCategory>))]
    SensorCategory Category,
    string MeasuredProperty,
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<TelemetryValueKind>))]
    TelemetryValueKind ValueKind,
    string Unit,
    double? ExpectedMinimum,
    double? ExpectedMaximum,
    DeploymentLocationResponse DeploymentLocation);
