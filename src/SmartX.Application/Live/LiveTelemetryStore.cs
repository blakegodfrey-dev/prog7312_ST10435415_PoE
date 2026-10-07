using SmartX.Domain.Enums;
using SmartX.Domain.ValueObjects;

namespace SmartX.Application.Live;

/// <summary>
/// Single-process committed cache. Short locks protect all mutable collections;
/// the initialization semaphore serializes SQL hydration without holding a lock.
/// </summary>
public sealed class LiveTelemetryStore
{
    public const int DefaultHistoryCapacity = 2000;
    public const int MaximumHistoryCapacity = 100000;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private readonly Dictionary<string, DeviceSnapshot> _devicesByMac = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, string> _macBySensorId = new();
    private readonly SortedDictionary<DateTimeOffset, List<TelemetrySnapshot>> _recentHistory = new();
    private readonly Dictionary<Guid, DateTimeOffset> _historyTimestampById = new();
    private bool _initialized;

    public LiveTelemetryStore(int historyCapacity = DefaultHistoryCapacity)
    {
        if (historyCapacity < 1 || historyCapacity > MaximumHistoryCapacity)
            throw new ArgumentOutOfRangeException(nameof(historyCapacity));
        HistoryCapacity = historyCapacity;
    }

    public int HistoryCapacity { get; }
    public bool IsInitialized => Volatile.Read(ref _initialized);

    public async Task EnsureInitializedAsync(
        Func<CancellationToken, Task<LiveTelemetrySeed>> load,
        CancellationToken cancellationToken)
    {
        if (IsInitialized) return;
        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (IsInitialized) return;
            var seed = await load(cancellationToken);
            lock (_sync)
            {
                // Merge instead of replacing: a registration can commit while
                // SQL is being read. An older seed must not erase that write.
                foreach (var device in seed.Devices) UpsertDevice(device);
                foreach (var reading in seed.RecentReadings) ApplyReading(Normalize(reading));
                Volatile.Write(ref _initialized, true);
            }
        }
        finally { _initializationGate.Release(); }
    }

    public void RegisterCommitted(DeviceSnapshot device)
    {
        lock (_sync) UpsertDevice(device);
    }

    public DeviceSnapshot? FindByMac(string? macAddress)
    {
        if (!MacAddressNormalizer.TryNormalize(macAddress, out var canonical)) return null;
        lock (_sync) return _devicesByMac.GetValueOrDefault(canonical);
    }

    public DeviceSnapshot? FindById(Guid sensorId)
    {
        lock (_sync)
            return _macBySensorId.TryGetValue(sensorId, out var mac)
                ? _devicesByMac.GetValueOrDefault(mac) : null;
    }

    public IReadOnlyList<DeviceSnapshot> GetDevices()
    {
        DeviceSnapshot[] copy;
        lock (_sync) copy = _devicesByMac.Values.ToArray();
        return Array.AsReadOnly(copy.OrderBy(device => device.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.Id).ToArray());
    }

    public void PublishCommitted(IReadOnlyList<TelemetrySnapshot> readings)
    {
        var normalized = readings.Select(Normalize).ToArray();
        lock (_sync)
        {
            // Validate the whole committed batch before changing live state.
            foreach (var reading in normalized)
            {
                if (!_devicesByMac.TryGetValue(reading.MacAddress, out var device) ||
                    device.Id != reading.SensorId || device.ValueKind != reading.ValueKind)
                    throw new InvalidOperationException("Committed telemetry does not match a registered device.");
            }
            foreach (var reading in normalized)
            {
                ApplyReading(reading);
            }
        }
    }

    public RecentHistorySnapshot GetRecentHistory(
        Guid? sensorId = null, int limit = 100,
        DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null)
    {
        if (limit < 1 || limit > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        if (fromUtc > toUtc) throw new ArgumentException("The start timestamp cannot follow the end timestamp.");
        lock (_sync)
        {
            var matches = _recentHistory.Where(bucket =>
                    (!fromUtc.HasValue || bucket.Key >= fromUtc.Value) &&
                    (!toUtc.HasValue || bucket.Key <= toUtc.Value))
                .SelectMany(bucket => bucket.Value)
                .Where(reading => !sensorId.HasValue || reading.SensorId == sensorId.Value)
                .ToArray();
            // Buckets and their lists already have deterministic chronological order.
            var selected = matches.TakeLast(limit).ToArray();
            return new RecentHistorySnapshot(
                HistoryCapacity, _historyTimestampById.Count, matches.Length, limit,
                _recentHistory.Count == 0 ? null : _recentHistory.First().Key,
                _recentHistory.Count == 0 ? null : _recentHistory.Last().Key,
                Array.AsReadOnly(selected));
        }
    }

    private void UpsertDevice(DeviceSnapshot incoming)
    {
        var canonical = MacAddressNormalizer.Normalize(incoming.MacAddress);
        if (incoming.Id == Guid.Empty) throw new ArgumentException("A device identifier is required.");
        if (_devicesByMac.TryGetValue(canonical, out var existing) && existing.Id != incoming.Id)
            throw new InvalidOperationException("A MAC address is already linked to another device.");
        if (_macBySensorId.TryGetValue(incoming.Id, out var previousMac) && previousMac != canonical)
            throw new InvalidOperationException("A device identifier is already linked to another MAC address.");
        _devicesByMac[canonical] = incoming with
        {
            MacAddress = canonical,
            LatestReading = ChooseLatest(existing?.LatestReading, incoming.LatestReading),
            LastReceivedAtUtc = Later(existing?.LastReceivedAtUtc, incoming.LastReceivedAtUtc)
        };
        _macBySensorId[incoming.Id] = canonical;
    }

    private void ApplyReading(TelemetrySnapshot reading)
    {
        var device = _devicesByMac[reading.MacAddress];
        _devicesByMac[reading.MacAddress] = device with
        {
            LatestReading = ChooseLatest(device.LatestReading, reading),
            LastReceivedAtUtc = Later(device.LastReceivedAtUtc, reading.ReceivedAtUtc)
        };
        AddHistory(reading);
    }

    private void AddHistory(TelemetrySnapshot reading)
    {
        if (_historyTimestampById.ContainsKey(reading.Id)) return;
        if (!_recentHistory.TryGetValue(reading.RecordedAtUtc, out var bucket))
        {
            bucket = new List<TelemetrySnapshot>();
            _recentHistory.Add(reading.RecordedAtUtc, bucket);
        }
        var index = bucket.BinarySearch(reading, ReadingComparer.Instance);
        bucket.Insert(index < 0 ? ~index : index, reading);
        _historyTimestampById.Add(reading.Id, reading.RecordedAtUtc);
        while (_historyTimestampById.Count > HistoryCapacity)
        {
            var oldest = _recentHistory.First();
            var removed = oldest.Value[0];
            oldest.Value.RemoveAt(0);
            _historyTimestampById.Remove(removed.Id);
            if (oldest.Value.Count == 0) _recentHistory.Remove(oldest.Key);
        }
    }

    private static TelemetrySnapshot Normalize(TelemetrySnapshot reading)
    {
        if (reading.Id == Guid.Empty || reading.SensorId == Guid.Empty || reading.RecordedAtUtc == default)
            throw new ArgumentException("Committed telemetry requires identifiers and a timestamp.");
        var correctColumns = reading.ValueKind switch
        {
            TelemetryValueKind.Float => reading.FloatValue.HasValue && !reading.IntegerValue.HasValue && !reading.BooleanValue.HasValue,
            TelemetryValueKind.Integer => reading.IntegerValue.HasValue && !reading.FloatValue.HasValue && !reading.BooleanValue.HasValue,
            TelemetryValueKind.Boolean => reading.BooleanValue.HasValue && !reading.FloatValue.HasValue && !reading.IntegerValue.HasValue,
            _ => false
        };
        if (!correctColumns) throw new ArgumentException("Committed telemetry must preserve its native value column.");
        return reading with
        {
            MacAddress = MacAddressNormalizer.Normalize(reading.MacAddress),
            RecordedAtUtc = reading.RecordedAtUtc.ToUniversalTime(),
            ReceivedAtUtc = reading.ReceivedAtUtc.ToUniversalTime()
        };
    }

    private static TelemetrySnapshot? ChooseLatest(TelemetrySnapshot? current, TelemetrySnapshot? candidate)
        => candidate is not null && (current is null || ReadingComparer.Instance.Compare(candidate, current) > 0)
            ? candidate : current;

    private static DateTimeOffset? Later(DateTimeOffset? left, DateTimeOffset? right)
        => !left.HasValue || right > left ? right?.ToUniversalTime() : left?.ToUniversalTime();

    // Latest-value and equal-time order match SQL hydration, irrespective of arrival order.
    private sealed class ReadingComparer : IComparer<TelemetrySnapshot>
    {
        public static ReadingComparer Instance { get; } = new();
        public int Compare(TelemetrySnapshot? x, TelemetrySnapshot? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var order = x.RecordedAtUtc.CompareTo(y.RecordedAtUtc);
            if (order == 0) order = x.ReceivedAtUtc.CompareTo(y.ReceivedAtUtc);
            return order == 0 ? StringComparer.Ordinal.Compare(x.Id.ToString("D"), y.Id.ToString("D")) : order;
        }
    }
}
