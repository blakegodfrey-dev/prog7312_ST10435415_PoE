using SmartX.Domain.Mesh;

namespace SmartX.Application.Mesh;

public sealed record MeshTraversal(Guid RootId, IReadOnlyList<Guid> ReachableNodeIds,
    IReadOnlyList<Guid> UnreachableNodeIds, int ExaminedAdjacencyEntries);
public sealed record MeshComponent(Guid RootId, IReadOnlyList<Guid> NodeIds,
    IReadOnlyList<Guid> EdgeIds, decimal TotalPowerCostMilliJoules);
public sealed record MeshForest(IReadOnlyList<MeshEdge> Edges, IReadOnlyList<MeshComponent> Components,
    decimal TotalPowerCostMilliJoules, int AvailableNodeCount, int HeapEnqueues, int HeapDequeues, int MaximumFrontierSize)
{
    public string Algorithm => "Prim";
    public string Frontier => "System.Collections.Generic.PriorityQueue (min-heap)";
    public bool IsSingleTree => AvailableNodeCount > 0 && Components.Count == 1;
    public string Scope => "Available vertices only; disconnected components form a minimum spanning forest.";
}
public sealed record MeshRoute(Guid NodeId, Guid GatewayId, bool IsReachable,
    IReadOnlyList<Guid> NodeIds, IReadOnlyList<Guid> EdgeIds, int? HopCount,
    decimal? TotalPowerCostMilliJoules, string Reason)
{
    public string RouteBasis => "Path in Prim's minimum spanning forest";
    public string Qualification => "The MST minimises total tree cost, not every individual route cost.";
}

public static class MeshAlgorithms
{
    public static MeshTraversal BreadthFirstSearch(WeightedMeshGraph graph, Guid rootId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!graph.Vertices.ContainsKey(rootId)) throw new KeyNotFoundException("Traversal root does not exist.");
        var visited = new HashSet<Guid>();
        var order = new List<Guid>();
        var queue = new Queue<Guid>();
        if (graph.Vertices[rootId].IsAvailable) { visited.Add(rootId); queue.Enqueue(rootId); }
        var examined = 0;
        while (queue.TryDequeue(out var node))
        {
            token.ThrowIfCancellationRequested();
            order.Add(node);
            foreach (var edge in graph.AvailableNeighbours(node))
            {
                examined++;
                var neighbour = edge.Other(node);
                if (visited.Add(neighbour)) queue.Enqueue(neighbour);
            }
        }
        return new(rootId, order.AsReadOnly(), Array.AsReadOnly(graph.Vertices.Keys.Where(id => !visited.Contains(id)).Order().ToArray()), examined);
    }

    /// <summary>Lazy Prim: real cut/frontier edges are enqueued by (cost, stable link ID).</summary>
    public static MeshForest MinimumSpanningForest(WeightedMeshGraph graph, Guid preferredRoot, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var visited = new HashSet<Guid>();
        var selected = new List<MeshEdge>();
        var components = new List<MeshComponent>();
        var frontier = new PriorityQueue<MeshEdge, (decimal Cost, Guid LinkId)>();
        var enqueues = 0;
        var dequeues = 0;
        var maximum = 0;
        var roots = graph.Vertices.Values.Where(n => n.IsAvailable)
            .OrderBy(n => n.Id == preferredRoot ? 0 : 1).ThenBy(n => n.Id).ToArray();
        foreach (var root in roots)
        {
            token.ThrowIfCancellationRequested();
            if (visited.Contains(root.Id)) continue;
            frontier.Clear();
            var nodes = new List<Guid>();
            var edges = new List<Guid>();
            decimal cost = 0;
            void Visit(Guid id)
            {
                visited.Add(id);
                nodes.Add(id);
                foreach (var edge in graph.AvailableNeighbours(id))
                {
                    if (visited.Contains(edge.Other(id))) continue;
                    frontier.Enqueue(edge, (edge.PowerCostMilliJoules, edge.Id));
                    enqueues++;
                    maximum = Math.Max(maximum, frontier.Count);
                }
            }
            Visit(root.Id);
            while (frontier.TryDequeue(out var edge, out _))
            {
                token.ThrowIfCancellationRequested();
                dequeues++;
                if (visited.Contains(edge.SourceId) && visited.Contains(edge.TargetId)) continue; // Stale/cycle edge.
                var next = visited.Contains(edge.SourceId) ? edge.TargetId : edge.SourceId;
                selected.Add(edge);
                edges.Add(edge.Id);
                cost += edge.PowerCostMilliJoules;
                Visit(next);
            }
            components.Add(new(root.Id, nodes.AsReadOnly(), edges.AsReadOnly(), cost));
        }
        return new(selected.AsReadOnly(), components.AsReadOnly(), components.Sum(c => c.TotalPowerCostMilliJoules),
            visited.Count, enqueues, dequeues, maximum);
    }

    public static MeshRoute GatewayRoute(WeightedMeshGraph graph, MeshForest forest, Guid nodeId,
        Guid gatewayId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!graph.Vertices.TryGetValue(nodeId, out var node)) throw new KeyNotFoundException("Selected mesh node does not exist.");
        MeshRoute Missing(string reason) => new(nodeId, gatewayId, false, Array.Empty<Guid>(), Array.Empty<Guid>(), null, null, reason);
        if (!graph.Vertices.TryGetValue(gatewayId, out var gateway) || !gateway.IsGateway || !gateway.IsAvailable)
            return Missing("GatewayUnavailable");
        if (!node.IsAvailable) return Missing("NodeUnavailable");
        var tree = new WeightedMeshGraph(graph.Vertices.Values, forest.Edges);
        var parents = new Dictionary<Guid, MeshEdge>();
        var queue = new Queue<Guid>();
        var seen = new HashSet<Guid> { gatewayId };
        queue.Enqueue(gatewayId);
        while (queue.TryDequeue(out var current))
        {
            token.ThrowIfCancellationRequested();
            if (current == nodeId) break;
            foreach (var edge in tree.AvailableNeighbours(current))
            {
                var next = edge.Other(current);
                if (!seen.Add(next)) continue;
                parents.Add(next, edge);
                queue.Enqueue(next);
            }
        }
        if (!seen.Contains(nodeId)) return Missing("NoAvailablePathToGateway");
        var path = new List<Guid> { nodeId };
        var links = new List<Guid>();
        decimal cost = 0;
        var cursor = nodeId;
        while (cursor != gatewayId)
        {
            var edge = parents[cursor];
            links.Add(edge.Id);
            cost += edge.PowerCostMilliJoules;
            cursor = edge.Other(cursor);
            path.Add(cursor);
        }
        return new(nodeId, gatewayId, true, path.AsReadOnly(), links.AsReadOnly(), links.Count, cost, "Available");
    }
}
