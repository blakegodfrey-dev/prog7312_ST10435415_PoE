using System.Text.Json.Serialization;
using SmartX.Domain.Enums;

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
    IReadOnlyList<TelemetryReadingResponse> Readings);
