namespace SmartX.Application.Operations;

public sealed record ProcessingStatus(int NormalCount, int PriorityCount, int PendingReadings,
    string WorkerState, long Completed, long Failed, long Rejected);

/// <summary>FIFO O(1), critical insertion/removal O(log n). A batch is one atomic work item.
/// Critical work bypasses waiting normal work, never an in-flight database transaction.</summary>
public sealed class TelemetryWorkQueue<T>(int capacity = 5000)
{
    private readonly object sync = new();
    private readonly Queue<(T Item, int Size)> normal = new();
    private readonly PriorityQueue<(T Item, int Size), (int Severity, long Sequence)> priority = new();
    private readonly SemaphoreSlim ready = new(0);
    private long sequence, completed, failed, rejected;
    private int pending;
    private bool accepting = true;
    private string workerState = "Starting";

    public bool TryEnqueue(T item, int size, int severity)
    {
        if (size < 1 || capacity < 1) throw new ArgumentOutOfRangeException(nameof(size));
        lock (sync)
        {
            if (!accepting || pending + size > capacity) { rejected++; return false; }
            pending += size;
            if (severity > 0) priority.Enqueue((item, size), (-severity, sequence++));
            else normal.Enqueue((item, size));
            ready.Release();
            return true;
        }
    }

    public async Task<T> TakeAsync(CancellationToken cancellationToken)
    {
        await ready.WaitAsync(cancellationToken);
        lock (sync)
        {
            var entry = priority.Count > 0 ? priority.Dequeue() : normal.Dequeue();
            pending -= entry.Size;
            workerState = "Processing";
            return entry.Item;
        }
    }
    public void Finish(bool success)
    {
        lock (sync) { if (success) completed++; else failed++; workerState = "Waiting"; }
    }
    public void StopAccepting() { lock (sync) accepting = false; }
    public ProcessingStatus Status() { lock (sync) return new(normal.Count, priority.Count, pending, workerState, completed, failed, rejected); }
}
