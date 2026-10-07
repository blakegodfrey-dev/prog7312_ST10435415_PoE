using SmartX.Application.Live;
using SmartX.Domain.Enums;
using SmartX.Domain.ValueObjects;

namespace SmartX.Tests.Live;

public sealed class LiveTelemetryStoreTests
{
    [Theory]
    [InlineData("a4:cf:12:8b:40:01")]
    [InlineData("  A4:CF:12:8B:40:01  ")]
    [InlineData("A4:CF:12:8B:40:01")]
    public void CanonicalMacAndGuidLookupsResolveTheSameRegisteredDevice(string input)
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(20, sensor);
        Assert.Equal(sensor.MacAddress, MacAddressNormalizer.Normalize(input));
        Assert.Equal(store.FindById(sensor.Id), store.FindByMac(input));
    }

    [Theory]
    [InlineData("A4-CF-12-8B-40-01")]
    [InlineData("A4:CF:12:8B:40")]
    [InlineData("GG:CF:12:8B:40:01")]
    [InlineData("")]
    public void InvalidMacFormatsDoNotCreateAlternativeRegistryKeys(string input)
    {
        Assert.False(MacAddressNormalizer.TryNormalize(input, out _));
        Assert.Throws<ArgumentException>(() => MacAddressNormalizer.Normalize(input));
        Assert.Null(new LiveTelemetryStore().FindByMac(input));
    }

    [Fact]
    public void DuplicateMacCannotReplaceAnotherDevice()
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(20, sensor);
        var conflicting = DeviceSnapshot.FromSensor(LiveTestData.Sensor(2)) with { MacAddress = sensor.MacAddress };
        Assert.Throws<InvalidOperationException>(() => store.RegisterCommitted(conflicting));
        Assert.Equal(sensor.Id, Assert.Single(store.GetDevices()).Id);
    }

    [Fact]
    public void OutOfOrderArrivalsAreChronologicalAndDoNotRegressTheCurrentValue()
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(20, sensor);
        var older = LiveTestData.Reading(sensor, 1, 10, received: LiveTestData.Time.AddMinutes(4));
        store.PublishCommitted([LiveTestData.Reading(sensor, 2, 20), LiveTestData.Reading(sensor, 3, 30), older]);
        Assert.Equal(new[] { 10, 20, 30 }, store.GetRecentHistory().Readings.Select(r => r.IntegerValue!.Value));
        Assert.Equal(30, store.FindById(sensor.Id)!.LatestReading!.IntegerValue);
        Assert.Equal(older.ReceivedAtUtc, store.FindById(sensor.Id)!.LastReceivedAtUtc);
    }

    [Fact]
    public void SameTimestampReadingsAcrossAndWithinDevicesAreAllPreserved()
    {
        var first = LiveTestData.Sensor();
        var second = LiveTestData.Sensor(2);
        var store = LiveTestData.Store(20, first, second);
        store.PublishCommitted([LiveTestData.Reading(first, 1, 10), LiveTestData.Reading(first, 1, 11), LiveTestData.Reading(second, 1, 12)]);
        Assert.Equal(3, store.GetRecentHistory().Readings.Count);
        Assert.Equal(2, store.GetRecentHistory(first.Id).Readings.Count);
    }

    [Fact]
    public void EquivalentUtcInstantsNormalizeWithoutLosingSameTimeReadings()
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(20, sensor);
        var reading = LiveTestData.Reading(sensor, 1);
        store.PublishCommitted([reading, reading with { Id = Guid.NewGuid(), RecordedAtUtc = reading.RecordedAtUtc.ToOffset(TimeSpan.FromHours(2)) }]);
        var history = store.GetRecentHistory();
        Assert.Equal(2, history.Readings.Count);
        Assert.All(history.Readings, r => Assert.Equal(TimeSpan.Zero, r.RecordedAtUtc.Offset));
        Assert.Equal(history.OldestRetainedAtUtc, history.NewestRetainedAtUtc);
    }

    [Fact]
    public void EqualRecordedTimesUseReceivedTimeThenCanonicalIdentifierAsATieBreak()
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(20, sensor);
        var first = LiveTestData.Reading(sensor, 1, 10, Guid.Parse("00000001-0000-0000-0000-000000000000"));
        var second = first with { Id = Guid.Parse("00000002-0000-0000-0000-000000000000"), IntegerValue = 20 };
        var latest = first with { Id = Guid.NewGuid(), ReceivedAtUtc = first.ReceivedAtUtc.AddSeconds(1), IntegerValue = 30 };
        store.PublishCommitted([latest, second, first]);
        Assert.Equal(new[] { 10, 20, 30 }, store.GetRecentHistory().Readings.Select(r => r.IntegerValue!.Value));
        Assert.Equal(latest, store.FindById(sensor.Id)!.LatestReading);
    }

    [Fact]
    public void CapacityEvictsOldestReadingsAndPreservesEachDevicesLatestSnapshot()
    {
        var quiet = LiveTestData.Sensor();
        var busy = LiveTestData.Sensor(2);
        var store = LiveTestData.Store(3, quiet, busy);
        store.PublishCommitted([LiveTestData.Reading(quiet, 0, 7)]);
        store.PublishCommitted(Enumerable.Range(1, 5).Select(i => LiveTestData.Reading(busy, i, i)).ToArray());
        store.PublishCommitted([LiveTestData.Reading(busy, -1, 99)]);
        Assert.Equal(new[] { 3, 4, 5 }, store.GetRecentHistory().Readings.Select(r => r.IntegerValue!.Value));
        Assert.Empty(store.GetRecentHistory(quiet.Id).Readings);
        Assert.Equal(7, store.FindById(quiet.Id)!.LatestReading!.IntegerValue);
        Assert.Equal(5, store.FindById(busy.Id)!.LatestReading!.IntegerValue);
    }

    [Fact]
    public void CapacityAlsoBoundsOneLargeTimestampBucket()
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(3, sensor);
        store.PublishCommitted(Enumerable.Range(1, 10).Select(i => LiveTestData.Reading(sensor, 1, i,
            received: LiveTestData.Time.AddMinutes(1).AddSeconds(i))).ToArray());
        Assert.Equal(new[] { 8, 9, 10 }, store.GetRecentHistory().Readings.Select(r => r.IntegerValue!.Value));
        Assert.Equal(3, store.GetRecentHistory().RetainedCount);
    }

    [Fact]
    public void RepeatedPublicationOfAPersistedIdentifierDoesNotDuplicateHistory()
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(3, sensor);
        var reading = LiveTestData.Reading(sensor, 1);
        store.PublishCommitted([reading]);
        store.PublishCommitted([reading]);
        Assert.Single(store.GetRecentHistory().Readings);
    }

    [Fact]
    public void RangeAndLimitReturnTheNewestMatchingReadingsInAscendingOrder()
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(20, sensor);
        store.PublishCommitted(Enumerable.Range(1, 8).Select(i => LiveTestData.Reading(sensor, i, i)).ToArray());
        var result = store.GetRecentHistory(sensor.Id, 2, LiveTestData.Time.AddMinutes(2), LiveTestData.Time.AddMinutes(6));
        Assert.Equal(new[] { 5, 6 }, result.Readings.Select(r => r.IntegerValue!.Value));
        Assert.Equal(5, result.MatchingCount);
        Assert.Equal(8, result.RetainedCount);
        Assert.True(result.IsLimited);
    }

    [Theory]
    [InlineData(TelemetryValueKind.Float)]
    [InlineData(TelemetryValueKind.Integer)]
    [InlineData(TelemetryValueKind.Boolean)]
    public void ZeroAndFalseRemainNativeTypedValues(TelemetryValueKind kind)
    {
        var sensor = LiveTestData.Sensor(1, kind);
        var store = LiveTestData.Store(3, sensor);
        store.PublishCommitted([LiveTestData.Reading(sensor, 1, 0)]);
        var reading = store.FindById(sensor.Id)!.LatestReading!;
        Assert.Equal(kind == TelemetryValueKind.Float ? 0f : (float?)null, reading.FloatValue);
        Assert.Equal(kind == TelemetryValueKind.Integer ? 0 : (int?)null, reading.IntegerValue);
        Assert.Equal(kind == TelemetryValueKind.Boolean ? false : (bool?)null, reading.BooleanValue);
    }

    [Fact]
    public void MixedDeviceBatchIsValidatedBeforeAnyLiveUpdate()
    {
        var known = LiveTestData.Sensor();
        var store = LiveTestData.Store(20, known);
        Assert.Throws<InvalidOperationException>(() => store.PublishCommitted([
            LiveTestData.Reading(known, 1), LiveTestData.Reading(LiveTestData.Sensor(2), 2)]));
        Assert.Null(store.FindById(known.Id)!.LatestReading);
        Assert.Empty(store.GetRecentHistory().Readings);
    }

    [Fact]
    public async Task ConcurrentHydrationCallsExecuteOneLoader()
    {
        var store = new LiveTelemetryStore();
        var calls = 0;
        async Task<LiveTelemetrySeed> Load(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            await Task.Yield();
            return new LiveTelemetrySeed([DeviceSnapshot.FromSensor(LiveTestData.Sensor())], []);
        }
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.EnsureInitializedAsync(Load, CancellationToken.None)));
        Assert.Equal(1, calls);
        Assert.True(store.IsInitialized);
        Assert.Single(store.GetDevices());
    }

    [Fact]
    public async Task FailedHydrationCanRetryWithoutReportingInitializedState()
    {
        var store = new LiveTelemetryStore();
        await Assert.ThrowsAsync<IOException>(() => store.EnsureInitializedAsync(_ => throw new IOException(), CancellationToken.None));
        Assert.False(store.IsInitialized);
        await store.EnsureInitializedAsync(_ => Task.FromResult(new LiveTelemetrySeed([], [])), CancellationToken.None);
        Assert.True(store.IsInitialized);
    }

    [Fact]
    public async Task WritesDuringHydrationSurviveAnOlderSeed()
    {
        var first = LiveTestData.Sensor();
        var second = LiveTestData.Sensor(2);
        var store = LiveTestData.Store(20, first);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<LiveTelemetrySeed>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hydration = store.EnsureInitializedAsync(_ => { started.SetResult(); return finish.Task; }, CancellationToken.None);
        await started.Task;
        store.RegisterCommitted(DeviceSnapshot.FromSensor(second));
        var newer = LiveTestData.Reading(first, 3);
        store.PublishCommitted([newer, LiveTestData.Reading(second, 2)]);
        finish.SetResult(new LiveTelemetrySeed([DeviceSnapshot.FromSensor(first)], [LiveTestData.Reading(first, 1)]));
        await hydration;
        Assert.Equal(2, store.GetDevices().Count);
        Assert.Equal(newer, store.FindById(first.Id)!.LatestReading);
        Assert.Equal(3, store.GetRecentHistory().Readings.Count);
    }

    [Fact]
    public async Task ConcurrentPublicationKeepsTheWindowBoundedAndTheLatestValueCorrect()
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(50, sensor);
        await Task.WhenAll(Enumerable.Range(1, 200).Select(i => Task.Run(() =>
            store.PublishCommitted([LiveTestData.Reading(sensor, i, i)]))));
        var snapshot = store.GetRecentHistory(limit: 50);
        Assert.Equal(50, snapshot.RetainedCount);
        Assert.Equal(Enumerable.Range(151, 50), snapshot.Readings.Select(r => r.IntegerValue!.Value));
        Assert.Equal(200, store.FindById(sensor.Id)!.LatestReading!.IntegerValue);
    }

    [Fact]
    public void ReturnedSnapshotsRemainUnchangedAfterLaterWrites()
    {
        var sensor = LiveTestData.Sensor();
        var store = LiveTestData.Store(20, sensor);
        store.PublishCommitted([LiveTestData.Reading(sensor, 1)]);
        var devices = store.GetDevices();
        var history = store.GetRecentHistory();
        store.PublishCommitted([LiveTestData.Reading(sensor, 2)]);
        Assert.Single(history.Readings);
        Assert.Equal(LiveTestData.Time.AddMinutes(1), devices[0].LatestReading!.RecordedAtUtc);
        Assert.Equal(2, store.GetRecentHistory().RetainedCount);
    }
}
