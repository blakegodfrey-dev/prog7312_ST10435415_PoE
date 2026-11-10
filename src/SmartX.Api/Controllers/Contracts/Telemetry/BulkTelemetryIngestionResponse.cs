namespace SmartX.Api.Contracts.Telemetry;

public sealed record BulkTelemetryIngestionResponse(
    int SubmittedCount,
    int StoredCount,
    int ValidCount,
    int InvalidCount,
    IReadOnlyList<TelemetryReadingResponse> Readings);
