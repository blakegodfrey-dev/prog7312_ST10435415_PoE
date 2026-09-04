namespace SmartX.Api.Contracts.Telemetry.Diagnostics;

public sealed record SensorHealthSummaryResponse(
    int TotalSensorCount,
    int ConnectedSensorCount,
    int StaleSensorCount,
    int DisconnectedSensorCount,
    int NoDataSensorCount,
    int InvalidLatestReadingCount,
    DateTimeOffset EvaluatedAtUtc,
    int ConnectedThresholdMinutes,
    int DisconnectedThresholdMinutes);