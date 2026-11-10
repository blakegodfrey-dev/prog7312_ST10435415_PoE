using System.Text.Json.Serialization;
using SmartX.Domain.Enums;
using SmartX.Application.History;

namespace SmartX.Api.Contracts.Telemetry;

public sealed record TelemetryHistoryResponse(
    Guid SensorId,
    string SensorName,
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<TelemetryValueKind>))]
    TelemetryValueKind ValueKind,
    string Unit,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<TelemetryReadingResponse> Readings)
{
    // Additive diagnostic evidence for the PoE; existing response fields remain.
    public HistoricalQueryStatistics? Tree { get; init; }
}
