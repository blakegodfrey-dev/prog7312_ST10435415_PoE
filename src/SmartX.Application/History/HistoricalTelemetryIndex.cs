using SmartX.Application.Live;

namespace SmartX.Application.History;

/// <summary>
/// Single-process, full historical index. Unlike the bounded Part 2 recent
/// cache, it retains every persisted packet. SQL remains the source of truth.
/// </summary>
public sealed class HistoricalTelemetryIndex
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private Dictionary<Guid, TelemetryAvlTree> _trees = [];
    private readonly Dictionary<Guid, TelemetrySnapshot> _pending = [];
    private bool _initialized;
    public bool IsInitialized => Volatile.Read(ref _initialized);

    public async Task EnsureInitializedAsync(
        Func<CancellationToken, IAsyncEnumerable<TelemetrySnapshot>> load,
        CancellationToken cancellationToken = default)
    {
        if (IsInitialized) return;
        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (IsInitialized) return;
            // Build privately while asynchronously streaming SQL. A cancelled or
            // failed load cannot expose a partial tree; the next request retries.
            var rebuilt = new Dictionary<Guid, TelemetryAvlTree>();
            await foreach (var reading in load(cancellationToken).WithCancellation(cancellationToken))
                Add(rebuilt, reading);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                // A packet may commit before or during hydration. Merge those
                // publications, deduplicating IDs already seen by the SQL reader.
                foreach (var reading in _pending.Values) Add(rebuilt, reading);
                _trees = rebuilt;
                _pending.Clear();
                Volatile.Write(ref _initialized, true);
            }
        }
        finally { _initializationGate.Release(); }
    }

    public void PublishCommitted(IReadOnlyList<TelemetrySnapshot> readings)
    {
        lock (_sync)
        {
            foreach (var reading in readings)
            {
                if (_initialized) Add(_trees, reading);
                else _pending.TryAdd(reading.Id, reading);
            }
        }
    }

    public HistoricalTelemetryPage Query(Guid sensorId, DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null, bool? isValid = null, int page = 1, int pageSize = 100)
    {
        lock (_sync)
        {
            if (!_initialized) throw new InvalidOperationException("Historical index must be loaded before querying.");
            return (_trees.GetValueOrDefault(sensorId) ?? new TelemetryAvlTree(sensorId))
                .Query(fromUtc, toUtc, isValid, page, pageSize);
        }
    }

    private static void Add(Dictionary<Guid, TelemetryAvlTree> trees, TelemetrySnapshot reading)
    {
        if (!trees.TryGetValue(reading.SensorId, out var tree))
            trees.Add(reading.SensorId, tree = new TelemetryAvlTree(reading.SensorId));
        tree.Insert(reading);
    }
}
