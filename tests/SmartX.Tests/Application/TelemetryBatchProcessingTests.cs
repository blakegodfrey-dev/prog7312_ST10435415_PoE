using SmartX.Application.Telemetry;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;
using Xunit;

namespace SmartX.Tests.Application;

public sealed class TelemetryBatchProcessingTests
{
    [Fact]
    public void ProcessForSensor_MovesAcceptedPacketsIntoListInOrder()
    {
        // Arrange
        var sensor = CreateSensor(
            TelemetryValueKind.Integer,
            SensorCategory.PowerConsumption);

        TelemetryPacket<int>[][] rawBatches =
        [
            [
                CreatePacket(sensor.Id, 820),
                CreatePacket(sensor.Id, 830)
            ],
            [
                CreatePacket(sensor.Id, 840)
            ]
        ];

        // Act
        var result = RawTelemetryBatchProcessor.ProcessForSensor(
            sensor,
            rawBatches);

        // Assert
        Assert.Equal(3, result.InspectedPacketCount);
        Assert.Equal(3, result.AcceptedPacketCount);
        Assert.Equal(0, result.RejectedPacketCount);

        Assert.Equal(
            [820, 830, 840],
            result.AcceptedPackets
                .Select(packet => packet.Value)
                .ToArray());
    }

    [Fact]
    public void ProcessForSensor_RejectsPacketsFromDifferentSensor()
    {
        // Arrange
        var sensor = CreateSensor(
            TelemetryValueKind.Float,
            SensorCategory.Environmental);

        TelemetryPacket<float>[][] rawBatches =
        [
            [
                CreatePacket(sensor.Id, 21.5f),
                CreatePacket(Guid.NewGuid(), 30.8f)
            ]
        ];

        // Act
        var result = RawTelemetryBatchProcessor.ProcessForSensor(
            sensor,
            rawBatches);

        // Assert
        Assert.Equal(2, result.InspectedPacketCount);
        Assert.Equal(1, result.AcceptedPacketCount);
        Assert.Equal(1, result.RejectedPacketCount);
        Assert.Equal(21.5f, result.AcceptedPackets[0].Value);
    }

    [Fact]
    public void ProcessForSensor_RejectsPacketsUsingWrongType()
    {
        // Arrange
        var floatSensor = CreateSensor(
            TelemetryValueKind.Float,
            SensorCategory.Environmental);

        TelemetryPacket<bool>[][] rawBatches =
        [
            [
                CreatePacket(floatSensor.Id, true)
            ]
        ];

        // Act
        var result = RawTelemetryBatchProcessor.ProcessForSensor(
            floatSensor,
            rawBatches);

        // Assert
        Assert.Equal(1, result.InspectedPacketCount);
        Assert.Equal(0, result.AcceptedPacketCount);
        Assert.Equal(1, result.RejectedPacketCount);
        Assert.Empty(result.AcceptedPackets);
    }

    [Fact]
    public void ProcessForSensor_HandlesEmptyBatches()
    {
        // Arrange
        var sensor = CreateSensor(
            TelemetryValueKind.Boolean,
            SensorCategory.Actuator);

        TelemetryPacket<bool>[][] rawBatches =
        [
            [],
            []
        ];

        // Act
        var result = RawTelemetryBatchProcessor.ProcessForSensor(
            sensor,
            rawBatches);

        // Assert
        Assert.Equal(0, result.InspectedPacketCount);
        Assert.Equal(0, result.AcceptedPacketCount);
        Assert.Equal(0, result.RejectedPacketCount);
        Assert.Empty(result.AcceptedPackets);
    }

    private static Sensor CreateSensor(
        TelemetryValueKind valueKind,
        SensorCategory category)
    {
        var isBoolean = valueKind == TelemetryValueKind.Boolean;

        return new Sensor(
            Guid.NewGuid(),
            "A4:CF:12:8B:39:01",
            "Test Sensor",
            category,
            isBoolean ? "Valve State" : "Sensor Reading",
            valueKind,
            isBoolean ? string.Empty : "unit",
            Guid.NewGuid(),
            isBoolean ? null : 0,
            isBoolean ? null : 5000);
    }

    private static TelemetryPacket<T> CreatePacket<T>(
        Guid sensorId,
        T value)
        where T : struct
    {
        return new TelemetryPacket<T>(
            Guid.NewGuid(),
            sensorId,
            value,
            DateTimeOffset.UtcNow);
    }
}