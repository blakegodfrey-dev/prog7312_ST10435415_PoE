using SmartX.Application.Live;

namespace SmartX.Application.Operations;

public readonly record struct IncidentKey(Guid DeviceId, string Type);
public sealed record Incident(Guid Id, Guid DeviceId, string Type, string Severity, string Reason,
    DateTimeOffset OpenedAtUtc, DateTimeOffset LastObservedAtUtc, DateTimeOffset? ResolvedAtUtc,
    int Observations, string Label = "unresolved", string? Note = null);
public sealed record Connection(Guid DeviceId, DateTimeOffset? LastSeenAtUtc, string State);

/// <summary>Gateway receipt time is authoritative, never a client-supplied timestamp.
/// The HashSet admits one warning per (device,type), while recurrence gets a new episode.</summary>
public sealed class IncidentTracker(TimeProvider clock, TimeSpan staleAfter, TimeSpan disconnectedAfter)
{
    private readonly object sync = new();
    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private bool initialized;

    public async Task EnsureInitializedAsync(Func<CancellationToken, Task> restore, CancellationToken token)
    {
        if (Volatile.Read(ref initialized)) return;
        await initializationGate.WaitAsync(token);
        try
        {
            if (initialized) return;
            await restore(token);
            Volatile.Write(ref initialized, true);
        }
        finally { initializationGate.Release(); }
    }

    private readonly HashSet<IncidentKey> activeKeys = new();
    private readonly Dictionary<IncidentKey, Incident> active = new();
    private readonly Dictionary<Guid, DateTimeOffset> seen = new();
    private readonly Queue<Incident> recentResolved = new();
    private readonly DateTimeOffset started = clock.GetUtcNow();
    public TimeSpan StaleAfter { get; } = staleAfter > TimeSpan.Zero && disconnectedAfter > staleAfter
        ? staleAfter : throw new ArgumentException("Connection timings must be positive and ordered.");
    public TimeSpan DisconnectedAfter { get; } = disconnectedAfter;

    public void RestoreSeen(Guid id, DateTimeOffset at) { lock (sync) if (!seen.TryGetValue(id, out var previous) || at > previous) seen[id] = at; }
    public void ObservePacket(Guid id)
    {
        lock (sync)
        {
            seen[id] = clock.GetUtcNow();
            Resolve(new(id, "Stale")); Resolve(new(id, "Disconnected"));
        }
    }
    public Connection GetConnection(Guid id)
    {
        lock (sync)
        {
            DateTimeOffset? last = seen.TryGetValue(id, out var at) ? at : null;
            var age = clock.GetUtcNow() - (last ?? started);
            return new(id, last, age >= DisconnectedAfter ? "Disconnected" : age >= StaleAfter ? "Stale" : last is null ? "Unknown" : "Connected");
        }
    }
    public void Sweep(IEnumerable<Guid> ids)
    {
        lock (sync)
        foreach (var id in ids)
        {
            var connection = GetConnection(id);
            Set(new(id, "Stale"), connection.State == "Stale", "warning", "Gateway has not received a recent packet.");
            Set(new(id, "Disconnected"), connection.State == "Disconnected", "critical", "Gateway heartbeat timed out; latest telemetry is historical.");
        }
    }
    public void ObserveReading(DeviceSnapshot device)
    {
        lock (sync)
        {
            var r = device.LatestReading;
            Set(new(device.Id, "OutOfRange"), r is { IsValid: false }, TelemetrySeverity.Classify(device, r) > 1 ? "critical" : "warning", r?.ValidationMessage ?? "Invalid reading");
            if (r is { IsValid: true }) Resolve(new(device.Id, "InvalidPayload"));
        }
    }
    public void InvalidPayload(Guid id, string reason)
    {
        lock (sync) { ObservePacket(id); Set(new(id, "InvalidPayload"), true, "warning", reason); }
    }
    public IReadOnlyList<Incident> Active() { lock (sync) return active.Values.OrderByDescending(i => i.OpenedAtUtc).ToArray(); }
    public IReadOnlyList<Incident> Events() { lock (sync) return recentResolved.Concat(active.Values).OrderByDescending(i => i.OpenedAtUtc).ToArray(); }
    public bool Annotate(Guid id, string label, string? note)
    {
        if (!new[] { "unresolved", "confirmed", "expected", "false positive" }.Contains(label) || note?.Length > 500) return false;
        lock (sync)
        {
            var pair = active.FirstOrDefault(p => p.Value.Id == id);
            if (pair.Value is null) return false;
            active[pair.Key] = pair.Value with { Label = label, Note = note };
            return true;
        }
    }
    private void Set(IncidentKey key, bool condition, string severity, string reason)
    {
        if (!condition) { Resolve(key); return; }
        var now = clock.GetUtcNow();
        if (activeKeys.Add(key)) active[key] = new(Guid.NewGuid(), key.DeviceId, key.Type, severity, reason, now, now, null, 1);
        else active[key] = active[key] with { LastObservedAtUtc = now, Observations = active[key].Observations + 1, Reason = reason, Severity = severity };
    }
    private void Resolve(IncidentKey key)
    {
        if (!activeKeys.Remove(key)) return;
        recentResolved.Enqueue(active[key] with { ResolvedAtUtc = clock.GetUtcNow() });
        active.Remove(key);
        while (recentResolved.Count > 200) recentResolved.Dequeue();
    }
}
