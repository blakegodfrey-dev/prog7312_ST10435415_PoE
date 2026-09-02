using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Tests.Infrastructure;

public sealed class TelemetryRecordTests
{
    [Fact]
    public void FromPacket_FloatPacket_PopulatesOnlyFloatColumn()
    {
        var packet = CreatePacket(21.75f);

        var record = TelemetryRecord.FromPacket(packet);

        Assert.Equal(TelemetryValueKind.Float, record.ValueKind);
        Assert.Equal(21.75f, record.FloatValue);
        Assert.Null(record.IntegerValue);
        Assert.Null(record.BooleanValue);
        Assert.True(record.IsValid);
        Assert.Null(record.ValidationMessage);
    }

    [Fact]
    public void FromPacket_IntegerPacket_PopulatesOnlyIntegerColumn()
    {
        var packet = CreatePacket(850);

        var record = TelemetryRecord.FromPacket(packet);

        Assert.Equal(TelemetryValueKind.Integer, record.ValueKind);
        Assert.Null(record.FloatValue);
        Assert.Equal(850, record.IntegerValue);
        Assert.Null(record.BooleanValue);
    }

    [Fact]
    public void FromPacket_BooleanPacket_PopulatesOnlyBooleanColumn()
    {
        var packet = CreatePacket(true);

        var record = TelemetryRecord.FromPacket(packet);

        Assert.Equal(TelemetryValueKind.Boolean, record.ValueKind);
        Assert.Null(record.FloatValue);
        Assert.Null(record.IntegerValue);
        Assert.True(record.BooleanValue is true);
    }

    [Fact]
    public void FromPacket_InvalidReading_StoresTrimmedValidationMessage()
    {
        var packet = CreatePacket(41.5f);

        var record = TelemetryRecord.FromPacket(
            packet,
            isValid: false,
            validationMessage: "  Above expected nutrient temperature range.  ");

        Assert.False(record.IsValid);
        Assert.Equal(
            "Above expected nutrient temperature range.",
            record.ValidationMessage);
    }

    [Fact]
    public void FromPacket_InvalidReadingWithoutMessage_ThrowsArgumentException()
    {
        var packet = CreatePacket(41.5f);

        var exception = Assert.Throws<ArgumentException>(
            () => TelemetryRecord.FromPacket(
                packet,
                isValid: false));

        Assert.Equal("validationMessage", exception.ParamName);
    }

    [Fact]
    public void FromPacket_ValidReadingWithErrorMessage_ThrowsArgumentException()
    {
        var packet = CreatePacket(21.75f);

        var exception = Assert.Throws<ArgumentException>(
            () => TelemetryRecord.FromPacket(
                packet,
                validationMessage: "Unexpected error."));

        Assert.Equal("validationMessage", exception.ParamName);
    }

    [Fact]
    public void FromPacket_PreservesPacketIdentityAndUtcTimestamps()
    {
        var packetId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var recordedAt = new DateTimeOffset(
            2026,
            9,
            2,
            12,
            30,
            0,
            TimeSpan.FromHours(2));
        var receivedAt = recordedAt.AddSeconds(2);
        var packet = new TelemetryPacket<int>(
            packetId,
            sensorId,
            900,
            recordedAt,
            receivedAt);

        var record = TelemetryRecord.FromPacket(packet);

        Assert.Equal(packetId, record.Id);
        Assert.Equal(sensorId, record.SensorId);
        Assert.Equal(recordedAt.ToUniversalTime(), record.RecordedAtUtc);
        Assert.Equal(receivedAt.ToUniversalTime(), record.ReceivedAtUtc);
    }

    private static TelemetryPacket<T> CreatePacket<T>(T value)
        where T : struct
    {
        var recordedAtUtc = new DateTimeOffset(
            2026,
            9,
            2,
            10,
            0,
            0,
            TimeSpan.Zero);

        return new TelemetryPacket<T>(
            Guid.NewGuid(),
            Guid.NewGuid(),
            value,
            recordedAtUtc,
            recordedAtUtc.AddSeconds(1));
    }
}
