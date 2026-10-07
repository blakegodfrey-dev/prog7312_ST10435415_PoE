namespace SmartX.Application.Live;

/// <summary>Counts refer to the retained global memory window, never full SQL history.</summary>
public sealed record RecentHistorySnapshot(
    int Capacity,
    int RetainedCount,
    int MatchingCount,
    int Limit,
    DateTimeOffset? OldestRetainedAtUtc,
    DateTimeOffset? NewestRetainedAtUtc,
    IReadOnlyList<TelemetrySnapshot> Readings)
{
    public bool IsLimited => MatchingCount > Readings.Count;
}
