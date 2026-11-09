using SmartX.Application.Mesh;
using SmartX.Domain.Mesh;
using SmartX.Infrastructure.Mesh;

namespace SmartX.Tests.Mesh;

public sealed class MeshConfigurationTests
{
    [Theory]
    [InlineData(10, 1, 0.1)]
    [InlineData(10, 0.5, 0.2)]
    [InlineData(20, 1, 0.25)]
    public void WirelessCostUsesDeclaredDistanceAndExpectedRetryAssumptions(double distance, double success, double expected)
        => Assert.Equal((decimal)expected, WirelessPowerCost.Estimate((decimal)distance, (decimal)success));

    [Fact]
    public void InvalidCostsAndTopologyConfigurationsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WirelessPowerCost.Estimate(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WirelessPowerCost.Estimate(1001, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WirelessPowerCost.Estimate(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WirelessPowerCost.Estimate(10, 1.1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => WirelessPowerCost.Estimate(1000, 0.0000000000000000000000000001m));
        var defaults = new MeshNetworkOptions();
        defaults.Links = MeshDemoTopology.CreateLinks(defaults.GatewayId);
        Assert.True(MeshNetworkOptions.IsValid(defaults));
        Assert.Equal(18, defaults.Links.Count);
        Assert.Equal(defaults.Links.Select(e => e.Id), MeshDemoTopology.CreateLinks(defaults.GatewayId).Select(e => e.Id));
        defaults.Links[0].DeliveryProbability = 0;
        Assert.False(MeshNetworkOptions.IsValid(defaults));
        defaults.Links = [];
        Assert.True(MeshNetworkOptions.IsValid(defaults)); // A gateway can exist without any links.
        defaults.GatewayId = Guid.Empty;
        Assert.False(MeshNetworkOptions.IsValid(defaults));
    }

    [Fact]
    public async Task ConcurrentSimulationChangesUseIndependentSnapshotsAndResetToRealHealth()
    {
        var state = new MeshSimulationState();
        var id = MeshAlgorithmTests.Id(1);
        state.SetNodeAvailable(id, false);
        var old = state.Snapshot();
        state.SetNodeAvailable(id, true);
        Assert.Contains(id, old.FailedNodes);
        Assert.DoesNotContain(id, state.Snapshot().FailedNodes);
        await Task.WhenAll(Enumerable.Range(1, 100).Select(i => Task.Run(() =>
        {
            state.SetNodeAvailable(MeshAlgorithmTests.Id(i), false);
            state.SetLinkAvailable(MeshAlgorithmTests.Id(i + 100), false);
            Assert.True(state.Snapshot().Revision > 0);
        })));
        Assert.Equal(100, state.Snapshot().FailedNodes.Count);
        state.Reset();
        Assert.Empty(state.Snapshot().FailedNodes);
        Assert.Empty(state.Snapshot().FailedLinks);
        Assert.Empty(new MeshSimulationState().Snapshot().FailedNodes); // Overrides are intentionally process-local.
    }

    [Fact]
    public void BundledTwelveSensorMeshHasKnownMinimumCostAndAnAlternativeAroundItsMainRelay()
    {
        var options = new MeshNetworkOptions();
        var definitions = MeshDemoTopology.CreateLinks(options.GatewayId);
        var nodes = definitions.SelectMany(e => new[] { e.SourceId, e.TargetId }).Distinct()
            .Select(id => new MeshVertex(id, "Test ESP32", null, options.SubnetId, id == options.GatewayId, true)).ToArray();
        var links = definitions.Select(e => new MeshEdge(e.Id, e.SourceId, e.TargetId,
            WirelessPowerCost.Estimate(e.DistanceMetres, e.DeliveryProbability))).ToArray();
        var graph = new WeightedMeshGraph(nodes, links);
        var mst = MeshAlgorithms.MinimumSpanningForest(graph, options.GatewayId);
        Assert.Equal(13, mst.AvailableNodeCount);
        Assert.Equal(12, mst.Edges.Count);
        Assert.Equal(1.002m, mst.TotalPowerCostMilliJoules);
        Assert.Equal(13, MeshAlgorithms.BreadthFirstSearch(graph, options.GatewayId).ReachableNodeIds.Count);
        var failed = new WeightedMeshGraph(nodes.Select(n => n.Id == MeshDemoTopology.SensorId(1) ? n with { IsAvailable = false } : n), links);
        var forest = MeshAlgorithms.MinimumSpanningForest(failed, options.GatewayId);
        var rerouted = MeshAlgorithms.GatewayRoute(failed, forest, MeshDemoTopology.SensorId(12), options.GatewayId);
        Assert.True(rerouted.IsReachable);
        Assert.DoesNotContain(MeshDemoTopology.SensorId(1), rerouted.NodeIds);
        Assert.Equal(1.032m, forest.TotalPowerCostMilliJoules);
    }
}
