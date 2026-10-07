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
        Assert.Contains("ROW_NUMBER()", latest);
        Assert.Contains("PARTITION BY", latest);
        Assert.Contains("CONVERT(varchar(36)", latest);
        Assert.Contains("MAX(", received);
        Assert.Contains("TOP(", recent);
        Assert.Contains("ORDER BY", recent);
    }
}
