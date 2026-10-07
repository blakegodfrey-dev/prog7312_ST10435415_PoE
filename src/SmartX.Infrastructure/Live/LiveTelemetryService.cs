using Microsoft.EntityFrameworkCore;
using SmartX.Application.Live;
using SmartX.Domain.Entities;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Infrastructure.Live;

/// <summary>Scoped SQL reader/publisher around the process-wide committed store.</summary>
public sealed class LiveTelemetryService(SmartXDbContext context, LiveTelemetryStore store)
{
    public LiveTelemetryStore Store { get; } = store;

    public Task EnsureInitializedAsync(CancellationToken cancellationToken)
        => Store.EnsureInitializedAsync(LoadSeedAsync, cancellationToken);

    public async Task<Sensor?> ResolveSensorAsync(Guid sensorId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        return Store.FindById(sensorId)?.ToSensor();
    }

    public async Task<Dictionary<Guid, Sensor>> ResolveSensorsAsync(
        IReadOnlyCollection<Guid> sensorIds, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        var result = new Dictionary<Guid, Sensor>();
        foreach (var id in sensorIds)
        {
            var snapshot = Store.FindById(id);
            if (snapshot is not null) result[id] = snapshot.ToSensor();
        }
        return result;
    }

    public void RegisterCommitted(Sensor sensor)
        => Store.RegisterCommitted(DeviceSnapshot.FromSensor(sensor));

    // Invoke only after SaveChangesAsync returns successfully. No await/cancellation
    // point can abandon publication after the database commit has completed.
    public void PublishCommitted(IReadOnlyList<TelemetryRecord> records)
    {
        var snapshots = records.Select(record =>
        {
            var device = Store.FindById(record.SensorId)
                ?? throw new InvalidOperationException("The committed device is missing from the registry.");
            return ToSnapshot(record, device.MacAddress);
        }).ToArray();
        Store.PublishCommitted(snapshots);
    }

    private async Task<LiveTelemetrySeed> LoadSeedAsync(CancellationToken cancellationToken)
    {
        // Hydrate every registered device, including those without a reading.
        // SQL does the latest-per-device selection; full history never enters memory.
        var sensors = await context.Sensors.AsNoTracking().ToListAsync(cancellationToken);
        var latest = await LiveTelemetryQueries.LatestReadings(context.TelemetryRecords.AsNoTracking())
            .ToListAsync(cancellationToken);
        var lastReceived = await LiveTelemetryQueries.LastReceivedTimes(context.TelemetryRecords.AsNoTracking())
            .ToDictionaryAsync(item => item.SensorId, item => item.ReceivedAtUtc, cancellationToken);
        var recent = await LiveTelemetryQueries.RecentReadings(context.TelemetryRecords.AsNoTracking(), Store.HistoryCapacity)
            .ToListAsync(cancellationToken);
        // A registration may commit between these queries. Resolve any newly
        // observed foreign keys before mapping; do not assume a frozen sensor list.
        var missingIds = latest.Select(record => record.SensorId)
            .Concat(recent.Select(record => record.SensorId)).Concat(lastReceived.Keys)
            .Except(sensors.Select(sensor => sensor.Id)).ToArray();
        if (missingIds.Length > 0)
            sensors.AddRange(await context.Sensors.AsNoTracking()
                .Where(sensor => missingIds.Contains(sensor.Id)).ToListAsync(cancellationToken));
        var latestById = latest.ToDictionary(record => record.SensorId);
        var devices = sensors.Select(sensor =>
        {
            var device = DeviceSnapshot.FromSensor(sensor);
            return device with
            {
                LatestReading = latestById.TryGetValue(sensor.Id, out var record)
                    ? ToSnapshot(record, sensor.MacAddress) : null,
                LastReceivedAtUtc = lastReceived.TryGetValue(sensor.Id, out var received) ? received : null
            };
        }).ToArray();
        var macById = devices.ToDictionary(device => device.Id, device => device.MacAddress);
        var readings = recent.Select(record => ToSnapshot(record, macById[record.SensorId])).ToArray();
        return new LiveTelemetrySeed(devices, readings);
    }

    private static TelemetrySnapshot ToSnapshot(TelemetryRecord record, string macAddress)
        => new(record.Id, record.SensorId, macAddress, record.ValueKind,
            record.FloatValue, record.IntegerValue, record.BooleanValue,
            record.RecordedAtUtc, record.ReceivedAtUtc, record.IsValid, record.ValidationMessage);
}
