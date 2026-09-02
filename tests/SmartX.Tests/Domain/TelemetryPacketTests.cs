using SmartX.Domain.Telemetry;
using Xunit;

namespace SmartX.Tests.Domain;

public sealed class TelemetryPacketTests
{
    [Fact]
    public void FloatPacket_PreservesFloatValue()
    {
        // Arrange
        var sensorId = Guid.NewGuid();
        const float expectedValue = 23.75f;

        // Act
        var packet = new TelemetryPacket<float>(
            Guid.NewGuid(),
            sensorId,
            expectedValue,
            DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(expectedValue, packet.Value);
        Assert.Equal(sensorId, packet.SensorId);
    }

    [Fact]
    public void IntegerPacket_PreservesIntegerValue()
    {
        // Arrange
        var sensorId = Guid.NewGuid();
        const int expectedValue = 1450;

        // Act
        var packet = new TelemetryPacket<int>(
            Guid.NewGuid(),
            sensorId,
            expectedValue,
            DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(expectedValue, packet.Value);
        Assert.Equal(sensorId, packet.SensorId);
    }

    [Fact]
    public void BooleanPacket_PreservesBooleanValue()
    {
        // Arrange
        var sensorId = Guid.NewGuid();
        const bool expectedValue = true;

        // Act
        var packet = new TelemetryPacket<bool>(
            Guid.NewGuid(),
            sensorId,
            expectedValue,
            DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(expectedValue, packet.Value);
        Assert.Equal(sensorId, packet.SensorId);
    }

    [Fact]
    public void UnsupportedPacketType_ThrowsNotSupportedException()
    {
        // Act
        var action = () => new TelemetryPacket<double>(
            Guid.NewGuid(),
            Guid.NewGuid(),
            23.75,
            DateTimeOffset.UtcNow);

        // Assert
        Assert.Throws<NotSupportedException>(action);
    }
}