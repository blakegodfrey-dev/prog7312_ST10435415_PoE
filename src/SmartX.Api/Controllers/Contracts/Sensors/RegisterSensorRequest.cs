using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Sensors;

public sealed record RegisterSensorRequest(
    [Required]
    Guid Id,

    [Required]
    [StringLength(17)]
    string MacAddress,

    [Required]
    [StringLength(150)]
    string FriendlyName,

    [property: JsonConverter(
        typeof(JsonStringEnumConverter<SensorCategory>))]
    SensorCategory Category,

    [Required]
    [StringLength(100)]
    string MeasuredProperty,

    [property: JsonConverter(
        typeof(JsonStringEnumConverter<TelemetryValueKind>))]
    TelemetryValueKind ValueKind,

    [StringLength(30)]
    string Unit,

    [Required]
    Guid DeploymentNodeId,

    double? ExpectedMinimum,

    double? ExpectedMaximum);
