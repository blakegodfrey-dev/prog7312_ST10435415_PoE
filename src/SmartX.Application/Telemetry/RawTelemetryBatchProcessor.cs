using SmartX.Domain.Telemetry;

namespace SmartX.Application.Telemetry;

/// <summary>
/// Inspects variable-length sequential telemetry batches represented
/// by a jagged array.
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
}