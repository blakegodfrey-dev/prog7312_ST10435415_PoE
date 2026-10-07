using System.Text.Json.Serialization;
using SmartX.Application.Live;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Live;

public sealed record LiveDeviceResponse(
    Guid Id, string MacAddress, string FriendlyName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SensorCategory>))] SensorCategory Category,
    string MeasuredProperty,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TelemetryValueKind>))] TelemetryValueKind ValueKind,
    string Unit, Guid DeploymentNodeId, double? ExpectedMinimum, double? ExpectedMaximum,
    LiveTelemetryReadingResponse? LatestReading, DateTimeOffset? LastReceivedAtUtc)
{
    public static LiveDeviceResponse FromSnapshot(DeviceSnapshot device) => new(
        device.Id, device.MacAddress, device.FriendlyName, device.Category,
        device.MeasuredProperty, device.ValueKind, device.Unit, device.DeploymentNodeId,
        device.ExpectedMinimum, device.ExpectedMaximum,
        device.LatestReading is null ? null : LiveTelemetryReadingResponse.FromSnapshot(device.LatestReading),
        device.LastReceivedAtUtc);
}
