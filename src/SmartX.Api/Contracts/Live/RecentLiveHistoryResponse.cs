namespace SmartX.Api.Contracts.Live;

public sealed record RecentLiveHistoryResponse(
    string Source, int Capacity, int RetainedCount, int MatchingCount,
    int Limit, bool IsLimited,
    DateTimeOffset? OldestRetainedAtUtc, DateTimeOffset? NewestRetainedAtUtc,
    IReadOnlyList<LiveTelemetryReadingResponse> Readings);
