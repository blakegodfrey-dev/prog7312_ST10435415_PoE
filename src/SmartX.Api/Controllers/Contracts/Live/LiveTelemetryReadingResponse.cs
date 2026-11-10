using System.Text.Json.Serialization;
using SmartX.Application.Live;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Live;

public sealed record LiveTelemetryReadingResponse(
    Guid Id, Guid SensorId, string MacAddress,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TelemetryValueKind>))] TelemetryValueKind ValueKind,
    float? FloatValue, int? IntegerValue, bool? BooleanValue,
    DateTimeOffset RecordedAtUtc, DateTimeOffset ReceivedAtUtc,
    bool IsValid, string? ValidationMessage)
{
    public static LiveTelemetryReadingResponse FromSnapshot(TelemetrySnapshot reading) => new(
        reading.Id, reading.SensorId, reading.MacAddress, reading.ValueKind,
        reading.FloatValue, reading.IntegerValue, reading.BooleanValue,
        reading.RecordedAtUtc, reading.ReceivedAtUtc, reading.IsValid, reading.ValidationMessage);
}
