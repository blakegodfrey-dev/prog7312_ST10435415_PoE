namespace SmartX.Application.Telemetry;

/// <summary>
/// Describes the structure of an inspected raw telemetry batch.
/// </summary>
public sealed record RawBatchInspection(
    int BatchCount,
    int PacketCount,
    int EmptyBatchCount,
    int LargestBatchSize);