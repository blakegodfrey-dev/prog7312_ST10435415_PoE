using SmartX.Application.Mesh;
using SmartX.Domain.Mesh;

namespace SmartX.Tests.Mesh;

public sealed class MeshAlgorithmTests
{
    internal static Guid Id(int value) => Guid.Parse($"70000000-0000-0000-0000-{value:X12}");
    internal static MeshVertex Node(int n, bool available = true) => new(Id(n), $"ESP32 {n}", null, "test", n == 1, available);
    internal static MeshEdge Link(int id, int a, int b, decimal cost, bool available = true)
        => new(Id(1000 + id), Id(a), Id(b), cost, available);
    internal static WeightedMeshGraph Example() => new(Enumerable.Range(1, 5).Select(i => Node(i)),
    [
        Link(1, 1, 2, 2), Link(2, 1, 3, 4), Link(3, 2, 3, 1), Link(4, 3, 4, 2),
        Link(5, 2, 4, 5), Link(6, 4, 5, 1), Link(7, 3, 5, 3), Link(8, 1, 5, 9)
    ]);

    [Fact]
    public void BfsAndPrimUseRealAdjacencyAndMinimumHeapFrontier()
    {
        var graph = Example();
        var bfs = MeshAlgorithms.BreadthFirstSearch(graph, Id(1));
        Assert.Equal(5, bfs.ReachableNodeIds.Count);
        Assert.Empty(bfs.UnreachableNodeIds);
        Assert.Equal(16, bfs.ExaminedAdjacencyEntries);
        var mst = MeshAlgorithms.MinimumSpanningForest(graph, Id(1));
        Assert.True(mst.IsSingleTree);
        Assert.Single(mst.Components);
        Assert.Equal(4, mst.Edges.Count);
        Assert.Equal(6m, mst.TotalPowerCostMilliJoules);
        Assert.Equal(new[] { Id(1001), Id(1003), Id(1004), Id(1006) }, mst.Edges.Select(e => e.Id));
        Assert.True(mst.HeapEnqueues > mst.Edges.Count);
        Assert.Equal(mst.HeapEnqueues, mst.HeapDequeues);
        Assert.True(mst.MaximumFrontierSize > 1);
        var route = MeshAlgorithms.GatewayRoute(graph, mst, Id(5), Id(1));
        Assert.True(route.IsReachable);
        Assert.Equal(new[] { Id(5), Id(4), Id(3), Id(2), Id(1) }, route.NodeIds);
        Assert.Equal(6m, route.TotalPowerCostMilliJoules);
        Assert.Equal(4, route.HopCount);
        AssertRouteEdgesExist(graph, route);
    }

    [Fact]
    public void MstPathIsNotMisrepresentedAsAnIndividualShortestPath()
    {
        var graph = new WeightedMeshGraph([Node(1), Node(2), Node(3)],
            [Link(1, 1, 2, 2), Link(2, 2, 3, 2), Link(3, 1, 3, 3)]);
        var tree = MeshAlgorithms.MinimumSpanningForest(graph, Id(1));
        var route = MeshAlgorithms.GatewayRoute(graph, tree, Id(3), Id(1));
        Assert.Equal(4m, tree.TotalPowerCostMilliJoules);
        Assert.Equal(4m, route.TotalPowerCostMilliJoules); // Direct link costs 3; the minimum total tree costs 4.
        Assert.Contains("not every individual", route.Qualification);
    }

    [Fact]
    public void IsolatesDisconnectedComponentsAndUnavailableGatewayNeverProduceFabricatedRoutes()
    {
        var graph = new WeightedMeshGraph(Enumerable.Range(1, 6).Select(i => Node(i, i != 6)),
            [Link(1, 1, 2, 1), Link(2, 3, 4, 2), Link(3, 4, 6, 0)]);
        var forest = MeshAlgorithms.MinimumSpanningForest(graph, Id(1));
        Assert.False(forest.IsSingleTree);
        Assert.Equal(3, forest.Components.Count); // {1,2}, {3,4}, {5}; 6 is unavailable.
        Assert.Equal(5, forest.AvailableNodeCount);
        Assert.Equal(3m, forest.TotalPowerCostMilliJoules);
        Assert.Equal(new[] { Id(1), Id(2) }, MeshAlgorithms.BreadthFirstSearch(graph, Id(1)).ReachableNodeIds);
        foreach (var id in new[] { 3, 5, 6 })
        {
            var route = MeshAlgorithms.GatewayRoute(graph, forest, Id(id), Id(1));
            Assert.False(route.IsReachable);
            Assert.Empty(route.NodeIds);
            Assert.Empty(route.EdgeIds);
            Assert.Null(route.HopCount);
            Assert.Null(route.TotalPowerCostMilliJoules);
        }
        var failed = new WeightedMeshGraph(graph.Vertices.Values.Select(n => n.Id == Id(1) ? n with { IsAvailable = false } : n), graph.Edges);
        var recomputed = MeshAlgorithms.MinimumSpanningForest(failed, Id(1));
        Assert.Empty(MeshAlgorithms.BreadthFirstSearch(failed, Id(1)).ReachableNodeIds);
        Assert.Equal("GatewayUnavailable", MeshAlgorithms.GatewayRoute(failed, recomputed, Id(2), Id(1)).Reason);
    }

    [Fact]
    public void NodeAndLinkFailuresRecomputeThroughAvailableNeighbours()
    {
        var graph = Example();
        var before = MeshAlgorithms.GatewayRoute(graph, MeshAlgorithms.MinimumSpanningForest(graph, Id(1)), Id(5), Id(1));
        var nodeFailed = new WeightedMeshGraph(graph.Vertices.Values.Select(n => n.Id == Id(3) ? n with { IsAvailable = false } : n), graph.Edges);
        var after = MeshAlgorithms.GatewayRoute(nodeFailed, MeshAlgorithms.MinimumSpanningForest(nodeFailed, Id(1)), Id(5), Id(1));
        Assert.True(after.IsReachable);
        Assert.DoesNotContain(Id(3), after.NodeIds);
        Assert.NotEqual(string.Join(",", before.NodeIds), string.Join(",", after.NodeIds));
        AssertRouteEdgesExist(nodeFailed, after);
        var linkFailed = new WeightedMeshGraph(graph.Vertices.Values,
            graph.Edges.Select(e => e.Id == before.EdgeIds[0] ? e with { IsAvailable = false } : e));
        var rerouted = MeshAlgorithms.GatewayRoute(linkFailed, MeshAlgorithms.MinimumSpanningForest(linkFailed, Id(1)), Id(5), Id(1));
        Assert.True(rerouted.IsReachable);
        Assert.DoesNotContain(before.EdgeIds[0], rerouted.EdgeIds);
        AssertRouteEdgesExist(linkFailed, rerouted);
    }

    [Fact]
    public void TiesAreStableAndZeroCostSingletonAndEmptyGraphsAreHandled()
    {
        var nodes = new[] { Node(1), Node(2), Node(3), Node(4) };
        var links = new[] { Link(4, 1, 4, 0), Link(3, 1, 3, 0), Link(2, 1, 2, 0), Link(1, 2, 3, 0), Link(5, 3, 4, 0) };
        var one = MeshAlgorithms.MinimumSpanningForest(new(nodes, links), Id(1));
        var two = MeshAlgorithms.MinimumSpanningForest(new(nodes.Reverse(), links.Reverse()), Id(1));
        Assert.Equal(one.Edges.Select(e => e.Id), two.Edges.Select(e => e.Id));
        Assert.Equal(0m, one.TotalPowerCostMilliJoules);
        var singleton = new WeightedMeshGraph([Node(1)], []);
        var forest = MeshAlgorithms.MinimumSpanningForest(singleton, Id(1));
        Assert.True(forest.IsSingleTree);
        var route = MeshAlgorithms.GatewayRoute(singleton, forest, Id(1), Id(1));
        Assert.Equal(new[] { Id(1) }, route.NodeIds);
        Assert.Equal(0, route.HopCount);
        Assert.False(MeshAlgorithms.MinimumSpanningForest(new([], []), Id(1)).IsSingleTree);
    }

    [Fact]
    public void RandomGraphsMatchIndependentKruskalCostAndForestSize()
    {
        var random = new Random(7312);
        for (var sample = 0; sample < 80; sample++)
        {
            var size = random.Next(2, 25);
            var nodes = Enumerable.Range(1, size).Select(i => Node(i, random.Next(5) != 0)).ToArray();
            var links = new List<MeshEdge>();
            for (var a = 1; a <= size; a++)
            for (var b = a + 1; b <= size; b++)
                if (random.Next(3) == 0) links.Add(Link(links.Count + 1, a, b, random.Next(20), random.Next(6) != 0));
            var graph = new WeightedMeshGraph(nodes, links);
            var parents = nodes.Where(n => n.IsAvailable).ToDictionary(n => n.Id, n => n.Id);
            Guid Root(Guid n) { while (parents[n] != n) n = parents[n]; return n; }
            decimal expectedCost = 0;
            var expectedEdges = 0;
            foreach (var link in links.Where(e => e.IsAvailable && parents.ContainsKey(e.SourceId) && parents.ContainsKey(e.TargetId))
                .OrderBy(e => e.PowerCostMilliJoules).ThenBy(e => e.Id))
            {
                var a = Root(link.SourceId);
                var b = Root(link.TargetId);
                if (a == b) continue;
                parents[a] = b;
                expectedEdges++;
                expectedCost += link.PowerCostMilliJoules;
            }
            var forest = MeshAlgorithms.MinimumSpanningForest(graph, Id(1));
            Assert.Equal(expectedCost, forest.TotalPowerCostMilliJoules);
            Assert.Equal(expectedEdges, forest.Edges.Count);
            Assert.Equal(parents.Count - expectedEdges, forest.Components.Count);
            Assert.Equal(forest.TotalPowerCostMilliJoules, forest.Components.Sum(c => c.TotalPowerCostMilliJoules));
        }
    }

    [Fact]
    public void InvalidGraphsAndCancelledWorkAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new WeightedMeshGraph([Node(1), Node(1)], []));
        Assert.Throws<ArgumentException>(() => new WeightedMeshGraph([Node(1)], [Link(1, 1, 2, 1)]));
        Assert.Throws<ArgumentException>(() => new WeightedMeshGraph([Node(1)], [Link(1, 1, 1, 1)]));
        Assert.Throws<ArgumentException>(() => new WeightedMeshGraph([Node(1), Node(2)], [Link(1, 1, 2, -1)]));
        Assert.Throws<ArgumentException>(() => new WeightedMeshGraph([Node(1), Node(2)], [Link(1, 1, 2, 1), Link(2, 2, 1, 2)]));
        Assert.Throws<ArgumentException>(() => new WeightedMeshGraph([Node(1), Node(2), Node(3)], [Link(1, 1, 2, 1), Link(1, 2, 3, 2)]));
        Assert.Throws<ArgumentException>(() => new WeightedMeshGraph([Node(1), Node(2), Node(3)], [Link(1, 1, 2, decimal.MaxValue), Link(2, 2, 3, 1)]));
        var graph = Example();
        Assert.Throws<KeyNotFoundException>(() => MeshAlgorithms.BreadthFirstSearch(graph, Guid.NewGuid()));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => MeshAlgorithms.BreadthFirstSearch(graph, Id(1), cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => MeshAlgorithms.MinimumSpanningForest(graph, Id(1), cancelled.Token));
    }

    internal static void AssertRouteEdgesExist(WeightedMeshGraph graph, MeshRoute route)
    {
        Assert.Equal(route.EdgeIds.Count + 1, route.NodeIds.Count);
        Assert.Equal(route.NodeIds.Count, route.NodeIds.Distinct().Count());
        for (var i = 0; i < route.EdgeIds.Count; i++)
        {
            var edge = Assert.Single(graph.AvailableNeighbours(route.NodeIds[i]), e => e.Id == route.EdgeIds[i]);
            Assert.Equal(route.NodeIds[i + 1], edge.Other(route.NodeIds[i]));
        }
    }
}
