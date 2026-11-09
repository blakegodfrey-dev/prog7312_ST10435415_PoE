using Microsoft.EntityFrameworkCore;
using SmartX.Infrastructure.Live;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Tests.Live;

public sealed class LiveTelemetrySqlTranslationTests
{
    [Fact]
    public void ActualHydrationQueriesTranslateUsingTheSqlServerProviderWithoutConnecting()
    {
        using var context = new SmartXDbContext(new DbContextOptionsBuilder<SmartXDbContext>()
            .UseSqlServer("Server=127.0.0.1;Database=TranslationOnly;Integrated Security=true;").Options);
        var latest = LiveTelemetryQueries.LatestReadings(context.TelemetryRecords.AsNoTracking()).ToQueryString();
        var received = LiveTelemetryQueries.LastReceivedTimes(context.TelemetryRecords.AsNoTracking()).ToQueryString();
        var recent = LiveTelemetryQueries.RecentReadings(context.TelemetryRecords.AsNoTracking(), 3).ToQueryString();
        var historical = LiveTelemetryQueries.HistoricalReadings(context.TelemetryRecords.AsNoTracking(),
            context.Sensors.AsNoTracking()).ToQueryString();
        Assert.Contains("ROW_NUMBER()", latest);
        Assert.Contains("PARTITION BY", latest);
        Assert.Contains("CONVERT(varchar(36)", latest);
        Assert.Contains("MAX(", received);
        Assert.Contains("TOP(", recent);
        Assert.Contains("ORDER BY", recent);
        Assert.Contains("INNER JOIN", historical);
        Assert.Contains("RecordedAtUtc", historical);
        Assert.DoesNotContain("TOP(", historical); // Reload must include more than the bounded recent cache.
    }
}
