using SmartX.Application.Live;
using SmartX.Application.Operations;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
namespace SmartX.Tests.Operations;

public sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class CollectionTests
{
    [Fact]
    public async Task PriorityBypassesBacklogAndSeverityThenArrivalIsDeterministic()
    {
        var q = new TelemetryWorkQueue<string>(10);
        Assert.True(q.TryEnqueue("normal-1", 1, 0)); Assert.True(q.TryEnqueue("normal-2", 1, 0));
        Assert.True(q.TryEnqueue("warning", 1, 1)); Assert.True(q.TryEnqueue("critical-1", 1, 3)); Assert.True(q.TryEnqueue("critical-2", 1, 3));
        Assert.Equal(2, q.Status().NormalCount); Assert.Equal(3, q.Status().PriorityCount);
        var actual = new List<string>(); for (var n = 0; n < 5; n++) actual.Add(await q.TakeAsync(default));
        Assert.Equal(new[] { "critical-1", "critical-2", "warning", "normal-1", "normal-2" }, actual);
    }
    [Fact]
    public async Task WholeBatchBackpressureAndCancellationDoNotDropAcceptedWork()
    {
        var q = new TelemetryWorkQueue<int>(3);
        Assert.True(q.TryEnqueue(1, 2, 0)); Assert.False(q.TryEnqueue(2, 2, 3));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => q.TakeAsync(cts.Token));
        Assert.Equal(1, await q.TakeAsync(default)); Assert.Equal(1, q.Status().Rejected);
        q.StopAccepting(); Assert.False(q.TryEnqueue(3, 1, 0));
    }
    [Fact]
    public async Task ConcurrentProducersRespectTheSharedBound()
    {
        var q = new TelemetryWorkQueue<int>(40);
        var accepted = 0;
        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => { if (q.TryEnqueue(i, 1, i % 2)) Interlocked.Increment(ref accepted); })));
        Assert.Equal(40, accepted); Assert.Equal(160, q.Status().Rejected);
        var values = new HashSet<int>(); for (var i = 0; i < accepted; i++) Assert.True(values.Add(await q.TakeAsync(default)));
    }
    [Fact]
    public void IncidentsDeduplicateResolveAndRecurIndependently()
    {
        var clock = new ManualClock(); var tracker = new IncidentTracker(clock, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        var id = Guid.NewGuid(); tracker.ObservePacket(id);
        tracker.InvalidPayload(id, "wrong type"); tracker.InvalidPayload(id, "wrong type");
        var first = Assert.Single(tracker.Active()); Assert.Equal(2, first.Observations);
        clock.Now += TimeSpan.FromSeconds(21); tracker.Sweep(new[] { id });
        Assert.Equal(2, tracker.Active().Count); Assert.Equal("Disconnected", tracker.GetConnection(id).State);
        tracker.ObservePacket(id); Assert.Single(tracker.Active());
        tracker.ObserveReading(Device(id, readingValid: true)); Assert.Empty(tracker.Active());
        tracker.InvalidPayload(id, "wrong type"); Assert.NotEqual(first.Id, Assert.Single(tracker.Active()).Id);
        Assert.Equal(2, tracker.Events().Count(i => i.ResolvedAtUtc is not null));
    }
    [Fact]
    public void PersistedClientTimestampsNeverBecomeGatewayHeartbeats()
    {
        var clock = new ManualClock(); var tracker = new IncidentTracker(clock, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        var device = Device(Guid.NewGuid(), true); tracker.ObserveReading(device);
        Assert.Null(tracker.GetConnection(device.Id).LastSeenAtUtc); Assert.Equal("Unknown", tracker.GetConnection(device.Id).State);
        clock.Now += TimeSpan.FromSeconds(21); tracker.Sweep(new[] { device.Id });
        Assert.Equal("Disconnected", tracker.GetConnection(device.Id).State);
        tracker.ObservePacket(device.Id); var seen = tracker.GetConnection(device.Id).LastSeenAtUtc;
        tracker.RestoreSeen(device.Id, clock.Now.AddDays(-1)); Assert.Equal(seen, tracker.GetConnection(device.Id).LastSeenAtUtc);
    }
    [Fact]
    public void HistoricalEvidenceChangesConfidenceAndConflictingActionsAreSuppressed()
    {
        var target = Device(Guid.NewGuid(), true); var context = "moisture:OutOfRange";
        var evidence = new List<BehaviourEvidence>();
        Assert.Empty(SuggestionEngine.Learn(evidence, new[] { target }, new HashSet<Guid>(), new HashSet<string> { context }));
        for (var i = 0; i < 3; i++) evidence.Add(new(Guid.NewGuid(), context, target.Id, "command", true, true));
        var tip = Assert.Single(SuggestionEngine.Learn(evidence.Concat(new[] { evidence[0] }), new[] { target }, new HashSet<Guid>(), new HashSet<string> { context }));
        Assert.Equal(3, tip.Support); Assert.Equal(1, tip.Confidence);
        evidence.Add(new(Guid.NewGuid(), context, target.Id, "command", false, true));
        tip = Assert.Single(SuggestionEngine.Learn(evidence, new[] { target }, new HashSet<Guid>(), new HashSet<string> { context })); Assert.Equal(.75, tip.Confidence);
        evidence.Add(new(Guid.NewGuid(), context, target.Id, "command", true, false));
        Assert.Equal(.75, Assert.Single(SuggestionEngine.Learn(evidence, new[] { target }, new HashSet<Guid>(), new HashSet<string> { context })).Confidence);
        evidence.Add(new(Guid.NewGuid(), context, target.Id, "command", false, true)); evidence.Add(new(Guid.NewGuid(), context, target.Id, "command", false, true));
        Assert.Empty(SuggestionEngine.Learn(evidence, new[] { target }, new HashSet<Guid>(), new HashSet<string> { context }));
        Assert.Empty(SuggestionEngine.Learn(evidence, new[] { target }, new HashSet<Guid> { target.Id }, new HashSet<string> { context }));
    }
    [Fact]
    public void AlreadySatisfiedAndIrrelevantActionsAreSuppressed()
    {
        var target = Device(Guid.NewGuid(), true); var evidence = Enumerable.Range(0, 4).Select(_ => new BehaviourEvidence(Guid.NewGuid(), "anomaly", target.Id, "command", false, true)).ToArray();
        Assert.Empty(SuggestionEngine.Learn(evidence, new[] { target }, new HashSet<Guid>(), new HashSet<string> { "anomaly" }));
        Assert.Empty(SuggestionEngine.Learn(evidence, new[] { target }, new HashSet<Guid>(), new HashSet<string> { "different" }));
    }
    internal static DeviceSnapshot Device(Guid id, bool readingValid) => new(id, "AA:BB:CC:DD:EE:01", "Pump A", SensorCategory.Actuator, "Pump state", TelemetryValueKind.Boolean, "", Guid.NewGuid(), null, null,
        new(Guid.NewGuid(), id, "AA:BB:CC:DD:EE:01", TelemetryValueKind.Boolean, null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, readingValid, readingValid ? null : "Invalid"));
}
