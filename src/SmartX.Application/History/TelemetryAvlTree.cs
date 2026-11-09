using SmartX.Application.Live;

namespace SmartX.Application.History;

public sealed record HistoricalQueryStatistics(string Structure, int IndexedReadings, int Height, int VisitedNodes);
public sealed record HistoricalTelemetryPage(int TotalCount, IReadOnlyList<TelemetrySnapshot> Readings,
    HistoricalQueryStatistics Tree);

/// <summary>
/// One sensor's AVL index, ordered by (UTC recorded time, ordinal packet ID).
/// Subtree total/valid counts support timestamp ranks and filtered pagination
/// without enumerating either the full history or every matching record.
/// The owning HistoricalTelemetryIndex synchronizes access.
/// </summary>
public sealed class TelemetryAvlTree(Guid sensorId)
{
    private sealed class Node(TelemetrySnapshot reading)
    {
        public TelemetrySnapshot Reading { get; } = reading;
        public Node? Left { get; set; }
        public Node? Right { get; set; }
        public int Height { get; set; } = 1;
        public int Count { get; set; } = 1;
        public int ValidCount { get; set; } = reading.IsValid ? 1 : 0;
    }

    private Node? _root;
    private readonly HashSet<Guid> _ids = [];
    public int Count => Size(_root, null);
    public int Height => NodeHeight(_root);

    public bool Insert(TelemetrySnapshot reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        if (reading.SensorId != sensorId || reading.SensorId == Guid.Empty || reading.Id == Guid.Empty ||
            reading.RecordedAtUtc == default)
            throw new ArgumentException("A historical reading requires the correct sensor, packet ID and timestamp.");
        // Committed packets are immutable. Replaying a seed/publication is idempotent.
        if (!_ids.Add(reading.Id)) return false;
        _root = Insert(_root, reading with
        {
            RecordedAtUtc = reading.RecordedAtUtc.ToUniversalTime(),
            ReceivedAtUtc = reading.ReceivedAtUtc.ToUniversalTime()
        });
        return true;
    }

    public HistoricalTelemetryPage Query(DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null,
        bool? isValid = null, int page = 1, int pageSize = 100)
    {
        if (fromUtc > toUtc) throw new ArgumentException("The start timestamp cannot follow the end timestamp.");
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var visited = 0;
        var lower = fromUtc.HasValue ? CountBefore(fromUtc.Value, false, isValid, ref visited) : 0;
        var upper = toUtc.HasValue ? CountBefore(toUtc.Value, true, isValid, ref visited) : Size(_root, isValid);
        var matched = upper - lower;
        var offset = ((long)page - 1) * pageSize;
        var readings = new List<TelemetrySnapshot>(pageSize);
        if (offset < matched)
        {
            // Newest first. Skip entire subtrees by rank, including readings
            // newer than the inclusive upper bound, then collect just this page.
            var skip = (long)Size(_root, isValid) - upper + offset;
            var take = (int)Math.Min(pageSize, matched - offset);
            CollectDescending(_root, isValid, ref skip, take, readings, ref visited);
        }
        return new(matched, readings.AsReadOnly(), new("AVL", Count, Height, visited));
    }

    private int CountBefore(DateTimeOffset boundary, bool inclusive, bool? validity, ref int visited)
    {
        var count = 0;
        var node = _root;
        while (node is not null)
        {
            visited++;
            var compare = node.Reading.RecordedAtUtc.CompareTo(boundary);
            if (compare < 0 || (inclusive && compare == 0))
            {
                count += Size(node.Left, validity) + (Matches(node, validity) ? 1 : 0);
                node = node.Right;
            }
            else node = node.Left;
        }
        return count;
    }

    private static void CollectDescending(Node? node, bool? validity, ref long skip, int take,
        List<TelemetrySnapshot> result, ref int visited)
    {
        if (node is null || result.Count == take) return;
        visited++;
        var rightCount = Size(node.Right, validity);
        if (skip >= rightCount) skip -= rightCount;
        else CollectDescending(node.Right, validity, ref skip, take, result, ref visited);
        if (result.Count == take) return;
        if (Matches(node, validity))
        {
            if (skip > 0) skip--;
            else result.Add(node.Reading);
        }
        if (result.Count < take && Size(node.Left, validity) > 0)
            CollectDescending(node.Left, validity, ref skip, take, result, ref visited);
    }

    private static Node Insert(Node? node, TelemetrySnapshot reading)
    {
        if (node is null) return new(reading);
        if (Compare(reading, node.Reading) < 0) node.Left = Insert(node.Left, reading);
        else node.Right = Insert(node.Right, reading);
        Update(node);
        var balance = NodeHeight(node.Left) - NodeHeight(node.Right);
        if (balance > 1)
        {
            if (Compare(reading, node.Left!.Reading) > 0) node.Left = RotateLeft(node.Left);
            return RotateRight(node);
        }
        if (balance < -1)
        {
            if (Compare(reading, node.Right!.Reading) < 0) node.Right = RotateRight(node.Right);
            return RotateLeft(node);
        }
        return node;
    }

    private static Node RotateLeft(Node root)
    {
        var pivot = root.Right!;
        root.Right = pivot.Left;
        pivot.Left = root;
        Update(root);
        Update(pivot);
        return pivot;
    }

    private static Node RotateRight(Node root)
    {
        var pivot = root.Left!;
        root.Left = pivot.Right;
        pivot.Right = root;
        Update(root);
        Update(pivot);
        return pivot;
    }

    private static void Update(Node node)
    {
        node.Height = 1 + Math.Max(NodeHeight(node.Left), NodeHeight(node.Right));
        node.Count = 1 + Size(node.Left, null) + Size(node.Right, null);
        node.ValidCount = (node.Reading.IsValid ? 1 : 0) + Size(node.Left, true) + Size(node.Right, true);
    }

    private static int NodeHeight(Node? node) => node?.Height ?? 0;
    private static bool Matches(Node node, bool? validity) => !validity.HasValue || node.Reading.IsValid == validity.Value;
    private static int Size(Node? node, bool? validity) => node is null ? 0 : validity switch
    {
        true => node.ValidCount,
        false => node.Count - node.ValidCount,
        null => node.Count
    };
    private static int Compare(TelemetrySnapshot left, TelemetrySnapshot right)
    {
        var time = left.RecordedAtUtc.CompareTo(right.RecordedAtUtc);
        return time != 0 ? time : StringComparer.Ordinal.Compare(left.Id.ToString("D"), right.Id.ToString("D"));
    }

    /// <summary>Explicit structural check for tests/benchmarks; never used by production queries.</summary>
    public bool ValidateInvariants() => Validate(_root, null, null).Valid && Count == _ids.Count;

    private static (bool Valid, int Height, int Count, int ValidCount) Validate(Node? node,
        TelemetrySnapshot? lower, TelemetrySnapshot? upper)
    {
        if (node is null) return (true, 0, 0, 0);
        var left = Validate(node.Left, lower, node.Reading);
        var right = Validate(node.Right, node.Reading, upper);
        var height = 1 + Math.Max(left.Height, right.Height);
        var count = 1 + left.Count + right.Count;
        var validCount = (node.Reading.IsValid ? 1 : 0) + left.ValidCount + right.ValidCount;
        return (left.Valid && right.Valid && Math.Abs(left.Height - right.Height) <= 1 &&
            node.Height == height && node.Count == count && node.ValidCount == validCount &&
            (lower is null || Compare(node.Reading, lower) > 0) &&
            (upper is null || Compare(node.Reading, upper) < 0), height, count, validCount);
    }
}
