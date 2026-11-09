namespace SmartX.Application.Mesh;

public sealed record MeshFailures(long Revision, IReadOnlySet<Guid> FailedNodes, IReadOnlySet<Guid> FailedLinks);

/// <summary>Process-local routing demonstration overrides; never changes actual sensor/command state.</summary>
public sealed class MeshSimulationState
{
    private readonly object _sync = new();
    private readonly HashSet<Guid> _nodes = [];
    private readonly HashSet<Guid> _links = [];
    private long _revision;

    public MeshFailures Snapshot()
    {
        lock (_sync) return new(_revision, _nodes.ToHashSet(), _links.ToHashSet());
    }

    public void SetNodeAvailable(Guid id, bool available) { lock (_sync) Set(_nodes, id, available); }
    public void SetLinkAvailable(Guid id, bool available) { lock (_sync) Set(_links, id, available); }
    private void Set(HashSet<Guid> ids, Guid id, bool available)
    {
        if (id == Guid.Empty) throw new ArgumentException("A stable mesh identifier is required.");
        if (available ? ids.Remove(id) : ids.Add(id)) _revision++;
    }
    public void Reset()
    {
        lock (_sync)
        {
            if (_nodes.Count == 0 && _links.Count == 0) return;
            _nodes.Clear();
            _links.Clear();
            _revision++;
        }
    }
}
