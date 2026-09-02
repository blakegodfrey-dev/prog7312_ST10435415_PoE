using SmartX.Application.Telemetry;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;
using Xunit;

namespace SmartX.Tests.Application;

public sealed class TelemetryPacketTypeGuardTests
{
    [Fact]
    public void EnsureCompatible_AcceptsFloatForFloatSensor()
    {
        var sensor = CreateSensor(
            TelemetryValueKind.Float,
            SensorCategory.Environmental,
            "Temperature",
            "deg C",
            18,
            24);

        var packet = new TelemetryPacket<float>(
            Guid.NewGuid(),
            sensor.Id,
            21.5f,
            DateTimeOffset.UtcNow);

        var exception = Record.Exception(
            () => TelemetryPacketTypeGuard.EnsureCompatible(sensor, packet));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureCompatible_AcceptsIntegerForIntegerSensor()
    {
        var sensor = CreateSensor(
            TelemetryValueKind.Integer,
            SensorCategory.PowerConsumption,
            "Power Usage",
            "W",
            0,
            2000);

        var packet = new TelemetryPacket<int>(
            Guid.NewGuid(),
            sensor.Id,
            1450,
            DateTimeOffset.UtcNow);

        var exception = Record.Exception(
            () => TelemetryPacketTypeGuard.EnsureCompatible(sensor, packet));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureCompatible_AcceptsBooleanForBooleanSensor()
    {
        var sensor = CreateSensor(
            TelemetryValueKind.Boolean,
            SensorCategory.Actuator,
            "Valve State",
            string.Empty);

        var packet = new TelemetryPacket<bool>(
            Guid.NewGuid(),
            sensor.Id,
            true,
            DateTimeOffset.UtcNow);

        var exception = Record.Exception(
            () => TelemetryPacketTypeGuard.EnsureCompatible(sensor, packet));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureCompatible_RejectsWrongPacketType()
    {
        var sensor = CreateSensor(
            TelemetryValueKind.Float,
            SensorCategory.Environmental,
            "Temperature",
            "deg C",
            18,
            24);

        var incorrectPacket = new TelemetryPacket<bool>(
            Guid.NewGuid(),
            sensor.Id,
            true,
            DateTimeOffset.UtcNow);

        var action = () =>
            TelemetryPacketTypeGuard.EnsureCompatible(
                sensor,
                incorrectPacket);

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void EnsureCompatible_RejectsPacketForDifferentSensor()
    {
        var sensor = CreateSensor(
            TelemetryValueKind.Integer,
            SensorCategory.PowerConsumption,
            "Power Usage",
            "W",
            0,
            2000);

        var packet = new TelemetryPacket<int>(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1450,
            DateTimeOffset.UtcNow);

        var action = () =>
            TelemetryPacketTypeGuard.EnsureCompatible(sensor, packet);

        Assert.Throws<InvalidOperationException>(action);
    }

    private static Sensor CreateSensor(
        TelemetryValueKind valueKind,
        SensorCategory category,
        string measuredProperty,
        string unit,
        double? expectedMinimum = null,
        double? expectedMaximum = null)
    {
        return new Sensor(
            Guid.NewGuid(),
            "A4:CF:12:8B:39:01",
            $"{measuredProperty} Sensor",
            category,
            measuredProperty,
            valueKind,
            unit,
            Guid.NewGuid(),
            expectedMinimum,
            expectedMaximum);
    }
}