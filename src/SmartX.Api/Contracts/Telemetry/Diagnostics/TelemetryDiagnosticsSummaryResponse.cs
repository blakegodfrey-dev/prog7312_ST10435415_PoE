namespace SmartX.Api.Contracts.Telemetry.Diagnostics;

public sealed record TelemetryDiagnosticsSummaryResponse(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int TotalReadings,
    int ValidReadings,
    int InvalidReadings,
    double InvalidPercentage,
    int AffectedSensorCount,
    int FloatReadings,
    int IntegerReadings,
    int BooleanReadings,
    DateTimeOffset? EarliestRecordedAtUtc,
    DateTimeOffset? LatestRecordedAtUtc);
