using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;

namespace SmartX.Application.Telemetry;

/// <summary>
/// Ensures that an incoming telemetry packet uses the value type
/// configured for its sensor.
/// </summary>
public static class TelemetryPacketTypeGuard
{
    public static void EnsureCompatible<T>(
        Sensor sensor,
        TelemetryPacket<T> packet)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(sensor);
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.SensorId != sensor.Id)
        {
            throw new InvalidOperationException(
                $"Telemetry packet '{packet.Id}' does not belong to " +
                $"sensor '{sensor.FriendlyName}'.");
        }

        var expectedType = sensor.ValueKind switch
        {
            TelemetryValueKind.Float => typeof(float),
            TelemetryValueKind.Integer => typeof(int),
            TelemetryValueKind.Boolean => typeof(bool),

            _ => throw new InvalidOperationException(
                $"Sensor telemetry kind '{sensor.ValueKind}' is not supported.")
        };

        if (typeof(T) != expectedType)
        {
            throw new InvalidOperationException(
                $"Sensor '{sensor.FriendlyName}' expects " +
                $"{expectedType.Name} telemetry, but received {typeof(T).Name}.");
        }
    }
}