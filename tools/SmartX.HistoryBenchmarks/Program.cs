using System.Diagnostics;
using System.Globalization;
using SmartX.Application.History;
using SmartX.Application.Live;
using SmartX.Domain.Enums;

// Algorithm-only measurements. They exclude SQL, HTTP, hydration and rendering.
var sensorId = Guid.Parse("73127312-0000-0000-0000-000000000001");
var start = new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero);
Console.WriteLine("readings,height,build_ms,matched,visited_nodes,median_query_us,linear_scan_us");
foreach (var size in new[] { 10000, 50000, 100000 })
{
    var source = Enumerable.Range(1, size).Select(i => new TelemetrySnapshot(
        new Guid(i, 0, 0, new byte[8]), sensorId, "02:73:12:00:00:01", TelemetryValueKind.Float,
        45 + (i % 10), null, null, start.AddSeconds(i), start.AddSeconds(i + 1), true, null)).ToArray();
    var tree = new TelemetryAvlTree(sensorId);
    var watch = Stopwatch.StartNew();
    foreach (var reading in source) tree.Insert(reading); // Deliberately sorted arrival.
    var build = watch.Elapsed.TotalMilliseconds;
    if (!tree.ValidateInvariants()) throw new InvalidOperationException("AVL invariant check failed.");
    var from = start.AddSeconds(size / 10);
    var to = from.AddSeconds(120);
    var result = tree.Query(from, to, pageSize: 500);
    var expected = source.Where(r => r.RecordedAtUtc >= from && r.RecordedAtUtc <= to)
        .OrderByDescending(r => r.RecordedAtUtc).Select(r => r.Id).ToArray();
    if (!expected.SequenceEqual(result.Readings.Select(r => r.Id)))
        throw new InvalidOperationException("Range differs from independent scan.");
    for (var i = 0; i < 100; i++) tree.Query(from, to, pageSize: 500);
    var samples = new double[501];
    for (var i = 0; i < samples.Length; i++)
    {
        watch.Restart();
        tree.Query(from, to, pageSize: 500);
        samples[i] = watch.Elapsed.TotalMicroseconds;
    }
    Array.Sort(samples);
    watch.Restart();
    var scanned = 0;
    for (var i = 0; i < 100; i++)
        scanned += source.Count(r => r.RecordedAtUtc >= from && r.RecordedAtUtc <= to);
    var scanMicroseconds = watch.Elapsed.TotalMicroseconds / 100;
    GC.KeepAlive(scanned);
    Console.WriteLine(string.Join(",", size, tree.Height, build.ToString("F3", CultureInfo.InvariantCulture),
        result.TotalCount, result.Tree.VisitedNodes, samples[samples.Length / 2].ToString("F3", CultureInfo.InvariantCulture),
        scanMicroseconds.ToString("F3", CultureInfo.InvariantCulture)));
}
