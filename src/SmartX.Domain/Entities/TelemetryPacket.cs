namespace SmartX.Domain.Telemetry;

/// <summary>
/// Carries one strongly typed telemetry reading from a registered sensor.
/// Supported value types are float, int and bool.
/// </summary>
/// <typeparam name="T">
/// The native telemetry value type.
/// </typeparam>
public sealed class TelemetryPacket<T>
    where T : struct
{
    public TelemetryPacket(
        Guid id,
        Guid sensorId,
        T value,
        DateTimeOffset recordedAtUtc,
        DateTimeOffset? receivedAtUtc = null)
    {
        EnsureSupportedType();

        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "A telemetry packet must have a valid identifier.",
                nameof(id));
        }

        if (sensorId == Guid.Empty)
        {
            throw new ArgumentException(
                "A telemetry packet must identify its sensor.",
                nameof(sensorId));
        }

        if (recordedAtUtc == default)
        {
            throw new ArgumentException(
                "A recorded timestamp is required.",
                nameof(recordedAtUtc));
        }

        Id = id;
        SensorId = sensorId;
        Value = value;
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
        ReceivedAtUtc = (receivedAtUtc ?? DateTimeOffset.UtcNow)
            .ToUniversalTime();
    }

    public Guid Id { get; }

    public Guid SensorId { get; }

    public T Value { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    private static void EnsureSupportedType()
    {
        var suppliedType = typeof(T);

        if (suppliedType != typeof(float) &&
            suppliedType != typeof(int) &&
            suppliedType != typeof(bool))
        {
            throw new NotSupportedException(
                $"Telemetry type '{suppliedType.Name}' is not supported. " +
                "Smart-X supports float, int and bool telemetry.");
        }
    }
}