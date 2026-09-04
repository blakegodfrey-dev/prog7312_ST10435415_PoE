using SmartX.Domain.Enums;

namespace SmartX.Application.Telemetry;

public static class SensorConnectionStatusEvaluator
{
    public static readonly TimeSpan ConnectedThreshold =
        TimeSpan.FromMinutes(5);

    public static readonly TimeSpan DisconnectedThreshold =
        TimeSpan.FromMinutes(15);

    public static SensorConnectionStatus Evaluate(
        DateTimeOffset? lastRecordedAtUtc,
        DateTimeOffset evaluatedAtUtc)
    {
        if (!lastRecordedAtUtc.HasValue)
        {
            return SensorConnectionStatus.NoData;
        }

        var readingAge = evaluatedAtUtc - lastRecordedAtUtc.Value;

        if (readingAge <= ConnectedThreshold)
        {
            return SensorConnectionStatus.Connected;
        }

        if (readingAge <= DisconnectedThreshold)
        {
            return SensorConnectionStatus.Stale;
        }

        return SensorConnectionStatus.Disconnected;
    }
}
