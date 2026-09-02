using SmartX.Application.Telemetry;
using SmartX.Domain.Telemetry;
using Xunit;

namespace SmartX.Tests.Application;

public sealed class RawTelemetryBatchProcessorTests
{
    [Fact]
    public void Inspect_ProcessesVariableLengthIntegerBatches()
    {
        // Arrange
        TelemetryPacket<int>[][] rawBatches =
        [
            [
                CreatePacket(820),
                CreatePacket(830)
            ],
            [
                CreatePacket(1450)
            ],
            [
                CreatePacket(600),
                CreatePacket(610),
                CreatePacket(620)
            ]
        ];

        // Act
        var result = RawTelemetryBatchProcessor.Inspect(rawBatches);

        // Assert
        Assert.Equal(3, result.BatchCount);
        Assert.Equal(6, result.PacketCount);
        Assert.Equal(0, result.EmptyBatchCount);
        Assert.Equal(3, result.LargestBatchSize);
    }

    [Fact]
    public void Inspect_RecordsEmptyBatches()
    {
        // Arrange
        TelemetryPacket<bool>[][] rawBatches =
        [
            [],
            [
                CreatePacket(true)
            ],
            []
        ];

        // Act
        var result = RawTelemetryBatchProcessor.Inspect(rawBatches);

        // Assert
        Assert.Equal(3, result.BatchCount);
        Assert.Equal(1, result.PacketCount);
        Assert.Equal(2, result.EmptyBatchCount);
        Assert.Equal(1, result.LargestBatchSize);
    }

    [Fact]
    public void Inspect_AcceptsAnEmptyJaggedArray()
    {
        // Arrange
        var rawBatches =
            Array.Empty<TelemetryPacket<float>[]>();

        // Act
        var result = RawTelemetryBatchProcessor.Inspect(rawBatches);

        // Assert
        Assert.Equal(0, result.BatchCount);
        Assert.Equal(0, result.PacketCount);
        Assert.Equal(0, result.EmptyBatchCount);
        Assert.Equal(0, result.LargestBatchSize);
    }

    [Fact]
    public void Inspect_RejectsNullInnerBatch()
    {
        // Arrange
        TelemetryPacket<float>[][] rawBatches =
        [
            null!
        ];

        // Act
        var action = () =>
            RawTelemetryBatchProcessor.Inspect(rawBatches);

        // Assert
        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Contains("index 0 is null", exception.Message);
    }

    [Fact]
    public void Inspect_RejectsNullPacketInsideBatch()
    {
        // Arrange
        TelemetryPacket<float>[][] rawBatches =
        [
            [
                null!
            ]
        ];

        // Act
        var action = () =>
            RawTelemetryBatchProcessor.Inspect(rawBatches);

        // Assert
        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Contains(
            "batch 0, position 0 is null",
            exception.Message);
    }

    private static TelemetryPacket<T> CreatePacket<T>(T value)
        where T : struct
    {
        return new TelemetryPacket<T>(
            Guid.NewGuid(),
            Guid.NewGuid(),
            value,
            DateTimeOffset.UtcNow);
    }
}