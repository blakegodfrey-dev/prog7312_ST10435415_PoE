namespace SmartX.Api.Contracts.Telemetry.Diagnostics;

public sealed record InvalidTelemetryPageResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<InvalidTelemetryReadingResponse> Readings);
