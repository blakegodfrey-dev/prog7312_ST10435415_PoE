using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Domain.ValueObjects;

namespace SmartX.Application.Live;

/// <summary>Immutable registered-device configuration and committed live state.</summary>
public sealed record DeviceSnapshot(
    Guid Id,
    string MacAddress,
    string FriendlyName,
    SensorCategory Category,
    string MeasuredProperty,
    TelemetryValueKind ValueKind,
    string Unit,
    Guid DeploymentNodeId,
    double? ExpectedMinimum,
    double? ExpectedMaximum,
    TelemetrySnapshot? LatestReading = null,
    DateTimeOffset? LastReceivedAtUtc = null)
{
    public static DeviceSnapshot FromSensor(Sensor sensor) => new(
        sensor.Id, MacAddressNormalizer.Normalize(sensor.MacAddress),
        sensor.FriendlyName, sensor.Category, sensor.MeasuredProperty,
        sensor.ValueKind, sensor.Unit, sensor.DeploymentNodeId,
        sensor.ExpectedMinimum, sensor.ExpectedMaximum);

    // Existing Part 1 validators consume a Sensor. Reconstruct its small,
    // detached configuration from the O(1) cache lookup; never retain EF entities.
    public Sensor ToSensor() => new(
        Id, MacAddress, FriendlyName, Category, MeasuredProperty, ValueKind,
        Unit, DeploymentNodeId, ExpectedMinimum, ExpectedMaximum);
}
