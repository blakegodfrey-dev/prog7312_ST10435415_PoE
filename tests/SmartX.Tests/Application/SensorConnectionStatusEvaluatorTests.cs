using SmartX.Application.Telemetry;
using SmartX.Domain.Enums;

namespace SmartX.Tests.Application;

public sealed class SensorConnectionStatusEvaluatorTests
{
    private static readonly DateTimeOffset EvaluatedAtUtc =
        new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_ReturnsNoDataWhenNoReadingExists()
    {
        var result = SensorConnectionStatusEvaluator.Evaluate(
            null,
            EvaluatedAtUtc);

        Assert.Equal(SensorConnectionStatus.NoData, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Evaluate_ReturnsConnectedWithinFiveMinutes(int minutes)
    {
        var result = SensorConnectionStatusEvaluator.Evaluate(
            EvaluatedAtUtc.AddMinutes(-minutes),
            EvaluatedAtUtc);

        Assert.Equal(SensorConnectionStatus.Connected, result);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(15)]
    public void Evaluate_ReturnsStaleBetweenFiveAndFifteenMinutes(
        int minutes)
    {
        var result = SensorConnectionStatusEvaluator.Evaluate(
            EvaluatedAtUtc.AddMinutes(-minutes),
            EvaluatedAtUtc);

        Assert.Equal(SensorConnectionStatus.Stale, result);
    }

    [Fact]
    public void Evaluate_ReturnsDisconnectedAfterFifteenMinutes()
    {
        var result = SensorConnectionStatusEvaluator.Evaluate(
            EvaluatedAtUtc.AddMinutes(-16),
            EvaluatedAtUtc);

        Assert.Equal(SensorConnectionStatus.Disconnected, result);
    }
}
