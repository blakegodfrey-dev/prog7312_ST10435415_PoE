using SmartX.Domain.Entities;
using SmartX.Domain.Telemetry;

namespace SmartX.Application.Telemetry;

/// <summary>
/// Inspects and processes variable-length sequential telemetry batches
/// represented by a jagged array.
/// </summary>
public static class RawTelemetryBatchProcessor
{
    public static RawBatchInspection Inspect<T>(
        TelemetryPacket<T>[][] rawBatches)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(rawBatches);

        var packetCount = 0;
        var emptyBatchCount = 0;
        var largestBatchSize = 0;

        for (var batchIndex = 0;
             batchIndex < rawBatches.Length;
             batchIndex++)
        {
            var batch = rawBatches[batchIndex];

            if (batch is null)
            {
                throw new ArgumentException(
                    $"Raw telemetry batch at index {batchIndex} is null.",
                    nameof(rawBatches));
            }

            if (batch.Length == 0)
            {
                emptyBatchCount++;
            }

            if (batch.Length > largestBatchSize)
            {
                largestBatchSize = batch.Length;
            }

            for (var packetIndex = 0;
                 packetIndex < batch.Length;
                 packetIndex++)
            {
                if (batch[packetIndex] is null)
                {
                    throw new ArgumentException(
                        $"Telemetry packet at batch {batchIndex}, " +
                        $"position {packetIndex} is null.",
                        nameof(rawBatches));
                }

                packetCount++;
            }
        }

        return new RawBatchInspection(
            rawBatches.Length,
            packetCount,
            emptyBatchCount,
            largestBatchSize);
    }

    /// <summary>
    /// Moves packets belonging to the selected sensor and using its
    /// configured telemetry type into a dynamic List collection.
    /// </summary>
    public static TelemetryBatchProcessingResult<T> ProcessForSensor<T>(
        Sensor sensor,
        TelemetryPacket<T>[][] rawBatches)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(sensor);

        var inspection = Inspect(rawBatches);

        // The List is preallocated to the largest possible accepted size.
        // Invalid packets will leave some capacity unused.
        var acceptedPackets =
            new List<TelemetryPacket<T>>(inspection.PacketCount);

        var rejectedPacketCount = 0;

        for (var batchIndex = 0;
             batchIndex < rawBatches.Length;
             batchIndex++)
        {
            var batch = rawBatches[batchIndex];

            for (var packetIndex = 0;
                 packetIndex < batch.Length;
                 packetIndex++)
            {
                var packet = batch[packetIndex];

                if (TelemetryPacketTypeGuard.IsCompatible(
                        sensor,
                        packet))
                {
                    acceptedPackets.Add(packet);
                }
                else
                {
                    rejectedPacketCount++;
                }
            }
        }

        return new TelemetryBatchProcessingResult<T>(
            acceptedPackets,
            inspection.PacketCount,
            rejectedPacketCount);
    }
}