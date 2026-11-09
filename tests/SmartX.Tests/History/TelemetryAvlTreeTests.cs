using SmartX.Application.History;
using SmartX.Application.Live;
using SmartX.Tests.Live;

namespace SmartX.Tests.History;

public sealed class TelemetryAvlTreeTests
{
    [Theory]
    [InlineData(3, 2, 1)] // LL
    [InlineData(1, 2, 3)] // RR
    [InlineData(3, 1, 2)] // LR
    [InlineData(1, 3, 2)] // RL
    public void AllRotationCasesPreserveBalanceAndOrdering(int a, int b, int c)
    {
        var sensor = LiveTestData.Sensor();
        var tree = new TelemetryAvlTree(sensor.Id);
        foreach (var minute in new[] { a, b, c })
        {
            tree.Insert(LiveTestData.Reading(sensor, minute, minute));
            Assert.True(tree.ValidateInvariants());
        }
        Assert.Equal(2, tree.Height);
        Assert.Equal(new int?[] { 3, 2, 1 }, tree.Query().Readings.Select(r => r.IntegerValue));
    }

    [Fact]
    public void EqualTimestampsOffsetsAndIdempotentReplayArePreserved()
    {
        var sensor = LiveTestData.Sensor();
        var tree = new TelemetryAvlTree(sensor.Id);
        var first = LiveTestData.Reading(sensor, 0);
        tree.Insert(first);
        Assert.False(tree.Insert(first));
        tree.Insert(first with { Id = Guid.NewGuid(), RecordedAtUtc = first.RecordedAtUtc.ToOffset(TimeSpan.FromHours(2)) });
        var page = tree.Query(first.RecordedAtUtc, first.RecordedAtUtc);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Readings.Select(r => r.Id).Distinct().Count());
        Assert.All(page.Readings, r => Assert.Equal(TimeSpan.Zero, r.RecordedAtUtc.Offset));
        Assert.Equal(page.Readings.OrderByDescending(r => r.Id.ToString("D"), StringComparer.Ordinal).Select(r => r.Id),
            page.Readings.Select(r => r.Id));
    }

    [Fact]
    public void RandomRangesValidityAndPagesMatchIndependentSortedOracle()
    {
        var sensor = LiveTestData.Sensor();
        var random = new Random(7312);
        var source = Enumerable.Range(0, 5000).Select(i => LiveTestData.Reading(sensor, random.Next(500), i)
            with { IsValid = i % 3 != 0 }).ToArray();
        var tree = new TelemetryAvlTree(sensor.Id);
        foreach (var reading in source) tree.Insert(reading);
        Assert.True(tree.ValidateInvariants());
        foreach (var validity in new bool?[] { null, true, false })
        for (var trial = 0; trial < 25; trial++)
        {
            var start = random.Next(500);
            var end = start + random.Next(100);
            var page = random.Next(1, 5);
            var expected = source.Where(r => r.RecordedAtUtc >= LiveTestData.Time.AddMinutes(start) &&
                r.RecordedAtUtc <= LiveTestData.Time.AddMinutes(end) && (!validity.HasValue || r.IsValid == validity))
                .OrderByDescending(r => r.RecordedAtUtc).ThenByDescending(r => r.Id.ToString("D"), StringComparer.Ordinal).ToArray();
            var actual = tree.Query(LiveTestData.Time.AddMinutes(start), LiveTestData.Time.AddMinutes(end), validity, page, 17);
            Assert.Equal(expected.Length, actual.TotalCount);
            Assert.Equal(expected.Skip((page - 1) * 17).Take(17).Select(r => r.Id), actual.Readings.Select(r => r.Id));
        }
    }

    [Fact]
    public void LargeMonotonicHistoryAndDeepPageDoNotScanTheHistory()
    {
        var sensor = LiveTestData.Sensor();
        var tree = new TelemetryAvlTree(sensor.Id);
        for (var i = 0; i < 100000; i++) tree.Insert(LiveTestData.Reading(sensor, i, i));
        Assert.True(tree.ValidateInvariants());
        Assert.InRange(tree.Height, 1, 25);
        var narrow = tree.Query(LiveTestData.Time.AddMinutes(100), LiveTestData.Time.AddMinutes(110));
        Assert.Equal(11, narrow.TotalCount);
        Assert.InRange(narrow.Tree.VisitedNodes, 1, 150);
        var deep = tree.Query(page: 3900, pageSize: 25);
        Assert.Equal(2524, deep.Readings[0].IntegerValue);
        Assert.InRange(deep.Tree.VisitedNodes, 1, 150);
        Assert.Empty(tree.Query(page: int.MaxValue, pageSize: 500).Readings);
    }

    [Fact]
    public void BoundariesOpenEmptyAndInvalidRangesAreHandled()
    {
        var sensor = LiveTestData.Sensor();
        var tree = new TelemetryAvlTree(sensor.Id);
        Assert.Empty(tree.Query().Readings);
        for (var i = 0; i < 5; i++) tree.Insert(LiveTestData.Reading(sensor, i, i));
        Assert.Equal(3, tree.Query(LiveTestData.Time.AddMinutes(1), LiveTestData.Time.AddMinutes(3)).TotalCount);
        Assert.Equal(2, tree.Query(fromUtc: LiveTestData.Time.AddMinutes(3)).TotalCount);
        Assert.Equal(1, tree.Query(toUtc: LiveTestData.Time).TotalCount);
        Assert.Empty(tree.Query(fromUtc: LiveTestData.Time.AddDays(1)).Readings);
        Assert.Throws<ArgumentException>(() => tree.Query(LiveTestData.Time.AddMinutes(1), LiveTestData.Time));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(page: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(pageSize: 501));
        Assert.Throws<ArgumentException>(() => tree.Insert(LiveTestData.Reading(LiveTestData.Sensor(2), 0)));
    }
}
