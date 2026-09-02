using SmartX.Domain.Telemetry;
using SmartX.Domain.ValueObjects;
using Xunit;

namespace SmartX.Tests.Domain;

public sealed class PowerReadingTests
{
    [Fact]
    public void AdditionOperator_CombinesPowerFromSameSamplingPeriod()
    {
        // Arrange
        var recordedAt = new DateTimeOffset(
            2026,
            9,
            2,
            8,
            0,
            0,
            TimeSpan.Zero);

        var pumpReading = new PowerReading(820, recordedAt);
        var growLightReading = new PowerReading(630, recordedAt);

        // Act
        var totalReading = pumpReading + growLightReading;

        // Assert
        Assert.Equal(1450, totalReading.Watts);
        Assert.Equal(recordedAt, totalReading.RecordedAtUtc);
        Assert.Equal(2, totalReading.MeterCount);
    }

    [Fact]
    public void AdditionOperator_DoesNotChangeOriginalReadings()
    {
        // Arrange
        var recordedAt = DateTimeOffset.UtcNow;
        var firstReading = new PowerReading(820, recordedAt);
        var secondReading = new PowerReading(630, recordedAt);

        // Act
        _ = firstReading + secondReading;

        // Assert
        Assert.Equal(820, firstReading.Watts);
        Assert.Equal(630, secondReading.Watts);
        Assert.Equal(1, firstReading.MeterCount);
        Assert.Equal(1, secondReading.MeterCount);
    }

    [Fact]
    public void AdditionOperator_RejectsDifferentSamplingPeriods()
    {
        // Arrange
        var firstReading = new PowerReading(
            820,
            new DateTimeOffset(
                2026,
                9,
                2,
                8,
                0,
                0,
                TimeSpan.Zero));

        var secondReading = new PowerReading(
            630,
            new DateTimeOffset(
                2026,
                9,
                2,
                8,
                5,
                0,
                TimeSpan.Zero));

        // Act
        var action = () => firstReading + secondReading;

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Constructor_RejectsNegativePowerConsumption()
    {
        // Act
        var action = () =>
            new PowerReading(-100, DateTimeOffset.UtcNow);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void FromTelemetry_CreatesReadingFromIntegerPacket()
    {
        // Arrange
        var recordedAt = DateTimeOffset.UtcNow;

        var packet = new TelemetryPacket<int>(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1450,
            recordedAt);

        // Act
        var reading = PowerReading.FromTelemetry(packet);

        // Assert
        Assert.Equal(1450, reading.Watts);
        Assert.Equal(
            recordedAt.ToUniversalTime(),
            reading.RecordedAtUtc);
        Assert.Equal(1, reading.MeterCount);
    }

    [Fact]
    public void AdditionOperator_ThrowsWhenCombinedWattsOverflow()
    {
        // Arrange
        var recordedAt = DateTimeOffset.UtcNow;
        var maximumReading = new PowerReading(int.MaxValue, recordedAt);
        var additionalReading = new PowerReading(1, recordedAt);

        // Act
        var action = () => maximumReading + additionalReading;

        // Assert
        Assert.Throws<OverflowException>(action);
    }
}