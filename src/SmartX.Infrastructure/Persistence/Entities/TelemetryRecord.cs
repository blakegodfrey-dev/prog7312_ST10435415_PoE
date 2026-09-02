using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;

namespace SmartX.Infrastructure.Persistence.Entities;

/// <summary>
/// Database-safe representation of one telemetry reading.
/// The generic packet remains the ingestion model, while this entity stores
/// each supported value type in its own nullable column.
/// </summary>
public sealed class TelemetryRecord
{
    public const int MaximumValidationMessageLength = 500;

    private TelemetryRecord()
    {
        // Required by Entity Framework Core.
    }

    private TelemetryRecord(
        Guid id,
        Guid sensorId,
        TelemetryValueKind valueKind,
        float? floatValue,
        int? integerValue,
        bool? booleanValue,
        DateTimeOffset recordedAtUtc,
        DateTimeOffset receivedAtUtc,
        bool isValid,
        string? validationMessage)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "A telemetry record must have a valid identifier.",
                nameof(id));
        }

        if (sensorId == Guid.Empty)
        {
            throw new ArgumentException(
                "A telemetry record must identify its sensor.",
                nameof(sensorId));
        }

        var normalisedValidationMessage = NormaliseValidationMessage(
            isValid,
            validationMessage);

        Id = id;
        SensorId = sensorId;
        ValueKind = valueKind;
        FloatValue = floatValue;
        IntegerValue = integerValue;
        BooleanValue = booleanValue;
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
        ReceivedAtUtc = receivedAtUtc.ToUniversalTime();
        IsValid = isValid;
        ValidationMessage = normalisedValidationMessage;
    }

    public Guid Id { get; private set; }

    public Guid SensorId { get; private set; }

    public TelemetryValueKind ValueKind { get; private set; }

    public float? FloatValue { get; private set; }

    public int? IntegerValue { get; private set; }

    public bool? BooleanValue { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public bool IsValid { get; private set; }

    public string? ValidationMessage { get; private set; }

    public static TelemetryRecord FromPacket(
        TelemetryPacket<float> packet,
        bool isValid = true,
        string? validationMessage = null)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return new TelemetryRecord(
            packet.Id,
            packet.SensorId,
            TelemetryValueKind.Float,
            packet.Value,
            null,
            null,
            packet.RecordedAtUtc,
            packet.ReceivedAtUtc,
            isValid,
            validationMessage);
    }

    public static TelemetryRecord FromPacket(
        TelemetryPacket<int> packet,
        bool isValid = true,
        string? validationMessage = null)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return new TelemetryRecord(
            packet.Id,
            packet.SensorId,
            TelemetryValueKind.Integer,
            null,
            packet.Value,
            null,
            packet.RecordedAtUtc,
            packet.ReceivedAtUtc,
            isValid,
            validationMessage);
    }

    public static TelemetryRecord FromPacket(
        TelemetryPacket<bool> packet,
        bool isValid = true,
        string? validationMessage = null)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return new TelemetryRecord(
            packet.Id,
            packet.SensorId,
            TelemetryValueKind.Boolean,
            null,
            null,
            packet.Value,
            packet.RecordedAtUtc,
            packet.ReceivedAtUtc,
            isValid,
            validationMessage);
    }

    private static string? NormaliseValidationMessage(
        bool isValid,
        string? validationMessage)
    {
        var normalisedMessage = string.IsNullOrWhiteSpace(validationMessage)
            ? null
            : validationMessage.Trim();

        if (isValid && normalisedMessage is not null)
        {
            throw new ArgumentException(
                "A valid telemetry record cannot have a validation error message.",
                nameof(validationMessage));
        }

        if (!isValid && normalisedMessage is null)
        {
            throw new ArgumentException(
                "An invalid telemetry record requires a validation error message.",
                nameof(validationMessage));
        }

        if (normalisedMessage?.Length > MaximumValidationMessageLength)
        {
            throw new ArgumentException(
                $"A validation message cannot exceed " +
                $"{MaximumValidationMessageLength} characters.",
                nameof(validationMessage));
        }

        return normalisedMessage;
    }
}
