using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartX.Application.Live;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Tests.Live;

internal static class LiveTestData
{
    public static readonly DateTimeOffset Time = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    public static readonly Guid NodeId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public static Sensor Sensor(int suffix = 1, TelemetryValueKind kind = TelemetryValueKind.Integer) => new(
        Guid.Parse($"aaaaaaaa-0000-0000-0000-{suffix:000000000000}"),
        $"A4:CF:12:8B:40:{suffix:X2}", $"Device {suffix}",
        kind == TelemetryValueKind.Boolean ? SensorCategory.Actuator : SensorCategory.Environmental,
        "Test reading", kind, kind == TelemetryValueKind.Boolean ? "state" : "unit", NodeId,
        kind == TelemetryValueKind.Boolean ? null : 0,
        kind == TelemetryValueKind.Boolean ? null : 1000);

    public static TelemetrySnapshot Reading(Sensor sensor, int minute, int value = 1,
        Guid? id = null, DateTimeOffset? received = null) => new(
        id ?? Guid.NewGuid(), sensor.Id, sensor.MacAddress, sensor.ValueKind,
        sensor.ValueKind == TelemetryValueKind.Float ? value : null,
        sensor.ValueKind == TelemetryValueKind.Integer ? value : null,
        sensor.ValueKind == TelemetryValueKind.Boolean ? value != 0 : null,
        Time.AddMinutes(minute), received ?? Time.AddMinutes(minute).AddSeconds(1), true, null);

    public static TelemetryRecord Record(Sensor sensor, int minute, int value = 1, Guid? id = null,
        DateTimeOffset? received = null) => sensor.ValueKind switch
    {
        TelemetryValueKind.Integer => TelemetryRecord.FromPacket(new TelemetryPacket<int>(
            id ?? Guid.NewGuid(), sensor.Id, value, Time.AddMinutes(minute), received)),
        TelemetryValueKind.Float => TelemetryRecord.FromPacket(new TelemetryPacket<float>(
            id ?? Guid.NewGuid(), sensor.Id, value, Time.AddMinutes(minute), received)),
        _ => TelemetryRecord.FromPacket(new TelemetryPacket<bool>(
            id ?? Guid.NewGuid(), sensor.Id, value != 0, Time.AddMinutes(minute), received))
    };

    public static SmartXDbContext Context(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<SmartXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(interceptors).Options;
        return new SmartXDbContext(options);
    }

    public static LiveTelemetryStore Store(int capacity = 2000, params Sensor[] sensors)
    {
        var store = new LiveTelemetryStore(capacity);
        foreach (var sensor in sensors) store.RegisterCommitted(DeviceSnapshot.FromSensor(sensor));
        return store;
    }
}
