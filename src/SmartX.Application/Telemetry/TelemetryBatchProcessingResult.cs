using SmartX.Domain.Telemetry;

namespace SmartX.Application.Telemetry;

/// <summary>
/// Contains the accepted packets and processing counts produced from
/// a raw telemetry batch.
/// </summary>
public sealed class TelemetryBatchProcessingResult<T>
    where T : struct
{
    private readonly IReadOnlyList<TelemetryPacket<T>> _acceptedPackets;

    internal TelemetryBatchProcessingResult(
        List<TelemetryPacket<T>> acceptedPackets,
        int inspectedPacketCount,
        int rejectedPacketCount)
    {
        ArgumentNullException.ThrowIfNull(acceptedPackets);

        _acceptedPackets = acceptedPackets.AsReadOnly();
        InspectedPacketCount = inspectedPacketCount;
        RejectedPacketCount = rejectedPacketCount;
    }

    public IReadOnlyList<TelemetryPacket<T>> AcceptedPackets =>
        _acceptedPackets;

    public int InspectedPacketCount { get; }

    public int AcceptedPacketCount => _acceptedPackets.Count;

    public int RejectedPacketCount { get; }
}