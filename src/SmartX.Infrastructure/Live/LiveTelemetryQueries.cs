using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Infrastructure.Live;

/// <summary>Reusable SQL-side hydration queries, also checked against the SQL Server provider.</summary>
public static class LiveTelemetryQueries
{
    public sealed record ReceiptTime(Guid SensorId, DateTimeOffset ReceivedAtUtc);

    public static IQueryable<TelemetryRecord> LatestReadings(IQueryable<TelemetryRecord> records)
        => records.GroupBy(record => record.SensorId)
            .Select(group => group.OrderByDescending(record => record.RecordedAtUtc)
                .ThenByDescending(record => record.ReceivedAtUtc)
                .ThenByDescending(record => record.Id.ToString()).First());

    public static IQueryable<ReceiptTime> LastReceivedTimes(IQueryable<TelemetryRecord> records)
        => records.GroupBy(record => record.SensorId)
            .Select(group => new ReceiptTime(group.Key, group.Max(record => record.ReceivedAtUtc)));

    public static IQueryable<TelemetryRecord> RecentReadings(IQueryable<TelemetryRecord> records, int capacity)
        => records.OrderByDescending(record => record.RecordedAtUtc)
            .ThenByDescending(record => record.ReceivedAtUtc)
            .ThenByDescending(record => record.Id.ToString()).Take(capacity);
}
