namespace SmartX.Api.Contracts.Telemetry;

public sealed record BulkTelemetryIngestionRequest(
    IReadOnlyList<BulkTelemetryItemRequest?>? Readings);
