using System.Runtime.CompilerServices;
using SmartX.Application.History;
using SmartX.Application.Live;
using SmartX.Tests.Live;

namespace SmartX.Tests.History;

public sealed class HistoricalTelemetryIndexTests
{
    [Fact]
    public async Task HydrationMergesConcurrentCommitsAndKeepsSensorsIsolated()
    {
        var index = new HistoricalTelemetryIndex();
        var a = LiveTestData.Sensor();
        var b = LiveTestData.Sensor(2);
        var old = LiveTestData.Reading(a, 0);
        var newer = LiveTestData.Reading(a, 1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loads = 0;
        async IAsyncEnumerable<TelemetrySnapshot> Load([EnumeratorCancellation] CancellationToken token)
        {
            Interlocked.Increment(ref loads);
            yield return old;
            entered.SetResult();
            await resume.Task.WaitAsync(token);
            yield return newer; // Also published while the SQL stream is active.
        }
        var first = index.EnsureInitializedAsync(Load);
        await entered.Task;
        index.PublishCommitted([newer, LiveTestData.Reading(b, 0)]);
        var second = index.EnsureInitializedAsync(Load);
        resume.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, loads);
        Assert.Equal(2, index.Query(a.Id).TotalCount);
        Assert.Single(index.Query(b.Id).Readings);
        Assert.Empty(index.Query(Guid.NewGuid()).Readings);
    }

    [Fact]
    public async Task FailedAndCancelledLoadsNeverExposePartialTreesAndCanRetry()
    {
        var sensor = LiveTestData.Sensor();
        var reading = LiveTestData.Reading(sensor, 0);
        var index = new HistoricalTelemetryIndex();
        index.PublishCommitted([reading]);
        async IAsyncEnumerable<TelemetrySnapshot> Failed([EnumeratorCancellation] CancellationToken _)
        {
            yield return reading;
            await Task.Yield();
            throw new InvalidOperationException("Simulated SQL stream failure");
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => index.EnsureInitializedAsync(Failed));
        Assert.False(index.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => index.Query(sensor.Id));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => index.EnsureInitializedAsync(Failed, cancelled.Token));
        async IAsyncEnumerable<TelemetrySnapshot> Empty([EnumeratorCancellation] CancellationToken _) { await Task.Yield(); yield break; }
        await index.EnsureInitializedAsync(Empty);
        Assert.Single(index.Query(sensor.Id).Readings); // Pending commit survived the failed rebuild.
        await Task.WhenAll(Enumerable.Range(1, 200).Select(i => Task.Run(() =>
        {
            index.PublishCommitted([LiveTestData.Reading(sensor, i)]);
            Assert.NotEmpty(index.Query(sensor.Id, pageSize: 1).Readings);
        })));
        Assert.Equal(201, index.Query(sensor.Id).TotalCount);
    }
}
