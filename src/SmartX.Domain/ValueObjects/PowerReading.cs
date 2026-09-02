using SmartX.Domain.Telemetry;

namespace SmartX.Domain.ValueObjects;

/// <summary>
/// Represents power consumption measured in watts during a specific
/// sampling period.
/// </summary>
public sealed record PowerReading
{
    public const string Unit = "W";

    public PowerReading(
        int watts,
        DateTimeOffset recordedAtUtc)
        : this(watts, recordedAtUtc, 1)
    {
    }

    private PowerReading(
        int watts,
        DateTimeOffset recordedAtUtc,
        int meterCount)
    {
        if (watts < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(watts),
                "Power consumption cannot be negative.");
        }

        if (recordedAtUtc == default)
        {
            throw new ArgumentException(
                "A recorded timestamp is required.",
                nameof(recordedAtUtc));
        }

        if (meterCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(meterCount),
                "A power reading must represent at least one meter.");
        }

        Watts = watts;
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
        MeterCount = meterCount;
    }

    public int Watts { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    /// <summary>
    /// Indicates how many meter readings contributed to this value.
    /// A normal reading represents one meter; an aggregate can
    /// represent multiple meters.
    /// </summary>
    public int MeterCount { get; }

    /// <summary>
    /// Creates a power reading from strongly typed integer telemetry.
    /// </summary>
    public static PowerReading FromTelemetry(
        TelemetryPacket<int> packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return new PowerReading(
            packet.Value,
            packet.RecordedAtUtc);
    }

    /// <summary>
    /// Combines power readings taken during the same sampling period.
    /// </summary>
    public static PowerReading operator +(
        PowerReading left,
        PowerReading right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.RecordedAtUtc != right.RecordedAtUtc)
        {
            throw new InvalidOperationException(
                "Power readings can only be combined when their " +
                "recorded timestamps match.");
        }

        var combinedWatts = checked(left.Watts + right.Watts);
        var combinedMeterCount = checked(
            left.MeterCount + right.MeterCount);

        return new PowerReading(
            combinedWatts,
            left.RecordedAtUtc,
            combinedMeterCount);
    }
}