using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartX.Application.Mesh;
using SmartX.Infrastructure.Mesh;
using SmartX.Tests.Operations;

namespace SmartX.Tests.Mesh;

public sealed class MeshHttpTests
{
    [Fact]
    public async Task DashboardSelectionAndRouteUseOneAtomicMeshAnalysis()
    {
        await using var s = new Scenario(); await s.StartAsync(); await s.RegisterRelayAsync();
        var snapshot = (await s.Client.GetFromJsonAsync<MeshSnapshot>($"/api/mesh?nodeId={s.RelayId}"))!;
        Assert.Equal(s.RelayId, snapshot.SelectedRoute!.NodeId);
        Assert.Equal(new[] { s.RelayId, s.Host.MoistureId, s.Host.PumpId, s.GatewayId }, snapshot.SelectedRoute.NodeIds);
        Assert.All(snapshot.SelectedRoute.EdgeIds, id => Assert.Contains(snapshot.Forest.Edges, e => e.Id == id));
        var byMac = (await s.Client.GetFromJsonAsync<MeshSnapshot>("/api/mesh?macAddress=02%3a73%3a12%3a00%3a00%3a50"))!;
        Assert.Equal(snapshot.SelectedRoute.NodeIds, byMac.SelectedRoute!.NodeIds);
        await s.Node(s.Host.MoistureId, false);
        var after = (await s.Client.GetFromJsonAsync<MeshSnapshot>($"/api/mesh?nodeId={s.RelayId}"))!;
        Assert.DoesNotContain(s.Host.MoistureId, after.SelectedRoute!.NodeIds);
        Assert.All(after.SelectedRoute.EdgeIds, id => Assert.Contains(after.Forest.Edges, e => e.Id == id));
        Assert.Null((await s.Mesh()).SelectedRoute);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.GetAsync($"/api/mesh?nodeId={s.RelayId}&macAddress=02:73:12:00:00:50")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.GetAsync("/api/mesh?macAddress=bad")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Client.GetAsync($"/api/mesh?nodeId={Guid.NewGuid()}")).StatusCode);
    }

    private sealed class Scenario : IAsyncDisposable
    {
        public OperationsHarness Host { get; } = new();
        public Guid GatewayId { get; } = MeshAlgorithmTests.Id(1);
        public Guid RelayId { get; } = MeshAlgorithmTests.Id(50);
        public Guid UnregisteredId { get; } = MeshAlgorithmTests.Id(99);
        public HttpClient Client => Host.Client;
        public List<MeshLinkDefinition> Links { get; } = [];
        public async Task StartAsync(bool simulation = true)
        {
            void Add(Guid a, Guid b, decimal distance) => Links.Add(new() { Id = MeshAlgorithmTests.Id(2000 + Links.Count),
                SourceId = a, TargetId = b, DistanceMetres = distance, DeliveryProbability = 1 });
            Add(GatewayId, Host.PumpId, 10);
            Add(GatewayId, Host.MoistureId, 18);
            Add(Host.PumpId, Host.MoistureId, 8);
            Add(Host.MoistureId, RelayId, 8);
            Add(Host.PumpId, RelayId, 20);
            Add(GatewayId, UnregisteredId, 5);
            await Host.StartAsync(configure: services => services.PostConfigure<MeshNetworkOptions>(o =>
            {
                o.GatewayId = GatewayId; o.GatewayName = "Test gateway"; o.SubnetId = "test-subnet";
                o.AllowSimulation = simulation; o.Links = Links;
            }));
        }
        public async Task RegisterRelayAsync(bool sendReading = true)
        {
            var result = await Client.PostAsJsonAsync("/api/sensors", new { id = RelayId, macAddress = "02:73:12:00:00:50",
                friendlyName = "ESP32 relay", category = "Environmental", measuredProperty = "Flow", valueKind = "Integer",
                unit = "L/h", deploymentNodeId = Host.NodeId, expectedMinimum = 0, expectedMaximum = 100 });
            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            if (sendReading)
                Assert.Equal(HttpStatusCode.Created, (await Client.PostAsJsonAsync("/api/telemetry/integer", new {
                    id = Guid.NewGuid(), sensorId = RelayId, value = 50, recordedAtUtc = Host.Clock.Now.AddSeconds(-1) })).StatusCode);
        }
        public async Task<MeshSnapshot> Mesh() => (await Client.GetFromJsonAsync<MeshSnapshot>("/api/mesh"))!;
        public async Task<MeshRoute> Route(Guid id) => (await Client.GetFromJsonAsync<MeshRoute>($"/api/mesh/route?nodeId={id}"))!;
        public async Task<MeshSnapshot> Node(Guid id, bool available)
        {
            var response = await Client.PutAsJsonAsync($"/api/mesh/nodes/{id}/availability", new { isAvailable = available });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<MeshSnapshot>())!;
        }
        public async Task<MeshSnapshot> Link(Guid id, bool available)
        {
            var response = await Client.PutAsJsonAsync($"/api/mesh/links/{id}/availability", new { isAvailable = available });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<MeshSnapshot>())!;
        }
        public async ValueTask DisposeAsync() => await Host.DisposeAsync();
    }

    [Fact]
    public async Task ApiUsesRegisteredIdsMacsNativeTelemetryAndTheActualMstPath()
    {
        await using var s = new Scenario(); await s.StartAsync(); await s.RegisterRelayAsync();
        var graph = await s.Mesh();
        Assert.Equal("test-subnet", graph.SubnetId);
        Assert.Equal("Prim", graph.Forest.Algorithm);
        Assert.Equal(0.264m, graph.Forest.TotalPowerCostMilliJoules);
        Assert.Equal(4, graph.Reachability.ReachableNodeIds.Count);
        Assert.False(Assert.Single(graph.Nodes, n => n.Id == s.UnregisteredId).IsAvailable);
        var route = await s.Route(s.RelayId);
        Assert.Equal(new[] { s.RelayId, s.Host.MoistureId, s.Host.PumpId, s.GatewayId }, route.NodeIds);
        var macRoute = await s.Client.GetFromJsonAsync<MeshRoute>("/api/mesh/route?macAddress=02%3a73%3a12%3a00%3a00%3a50");
        Assert.Equal(route.NodeIds, macRoute!.NodeIds);
        Assert.Equal(route.EdgeIds, macRoute.EdgeIds);
        Assert.Equal(0.264m, route.TotalPowerCostMilliJoules);
        Assert.True((await s.Client.GetAsync("/api/mesh?subnetId=test-subnet")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task NodeLinkIsolationGatewayFailureAndRecoveryRecomputeValidRoutes()
    {
        await using var s = new Scenario(); await s.StartAsync(); await s.RegisterRelayAsync();
        var failed = await s.Node(s.Host.MoistureId, false);
        Assert.False(Assert.Single(failed.Nodes, n => n.Id == s.Host.MoistureId).IsAvailable);
        var rerouted = await s.Route(s.RelayId);
        Assert.True(rerouted.IsReachable);
        Assert.Equal(new[] { s.RelayId, s.Host.PumpId, s.GatewayId }, rerouted.NodeIds);
        Assert.Equal(0.35m, rerouted.TotalPowerCostMilliJoules);
        var disconnected = await s.Link(s.Links[4].Id, false);
        Assert.Equal(2, disconnected.Forest.Components.Count);
        var missing = await s.Route(s.RelayId);
        Assert.False(missing.IsReachable);
        Assert.Equal("NoAvailablePathToGateway", missing.Reason);
        Assert.Empty(missing.NodeIds);
        var gatewayFailed = await s.Node(s.GatewayId, false);
        Assert.Empty(gatewayFailed.Reachability.ReachableNodeIds);
        Assert.Equal("GatewayUnavailable", (await s.Route(s.Host.PumpId)).Reason);
        Assert.Equal("GatewayUnavailable", gatewayFailed.NetworkState);
        Assert.True((await s.Client.DeleteAsync("/api/mesh/simulation")).IsSuccessStatusCode);
        Assert.True((await s.Route(s.RelayId)).IsReachable);
        // Routing overrides do not change original command/Undo semantics.
        await s.Node(s.Host.PumpId, false);
        Assert.Equal(HttpStatusCode.OK, (await s.Host.CommandAsync(true)).StatusCode);
    }

    [Fact]
    public async Task RealHeartbeatTimeoutCannotBeOverriddenAndUnseenStaleNodesStayUnavailable()
    {
        await using var s = new Scenario(); await s.StartAsync(); await s.RegisterRelayAsync(sendReading: false);
        s.Host.Clock.Now += TimeSpan.FromSeconds(11);
        var initial = await s.Mesh();
        var unseen = Assert.Single(initial.Nodes, n => n.Id == s.RelayId);
        Assert.Equal("Stale", unseen.ConnectionState);
        Assert.Null(unseen.LastSeenAtUtc);
        Assert.False(unseen.IsAvailable);
        // Sensors with genuine receipts can still route while Stale (<20 s here).
        Assert.True((await s.Route(s.Host.PumpId)).IsReachable);
        s.Host.Clock.Now += TimeSpan.FromSeconds(10);
        Assert.False((await s.Route(s.Host.PumpId)).IsReachable);
        var cleared = await s.Node(s.Host.PumpId, true);
        Assert.False(Assert.Single(cleared.Nodes, n => n.Id == s.Host.PumpId).IsAvailable);
        Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsJsonAsync($"/api/operations/heartbeat/{s.Host.PumpId}", new { })).StatusCode);
        Assert.True((await s.Route(s.Host.PumpId)).IsReachable);
    }

    [Fact]
    public async Task ValidationAndSimulationDisabledResponsesAreStructured()
    {
        await using var s = new Scenario(); await s.StartAsync();
        foreach (var path in new[] { "/api/mesh/route", "/api/mesh/route?macAddress=bad", $"/api/mesh/route?nodeId={Guid.Empty}",
            $"/api/mesh/route?nodeId={s.Host.PumpId}&macAddress=AA:BB:CC:DD:EE:01", "/api/mesh?subnetId=other" })
            Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Client.GetAsync($"/api/mesh/route?nodeId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Client.PutAsJsonAsync($"/api/mesh/nodes/{Guid.NewGuid()}/availability", new { isAvailable = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Client.PutAsJsonAsync($"/api/mesh/links/{Guid.NewGuid()}/availability", new { isAvailable = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.PutAsJsonAsync($"/api/mesh/nodes/{s.Host.PumpId}/availability", new { })).StatusCode);
        s.Host.App.Services.GetRequiredService<IOptions<MeshNetworkOptions>>().Value.AllowSimulation = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Client.DeleteAsync("/api/mesh/simulation")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Client.PutAsJsonAsync($"/api/mesh/nodes/{s.Host.PumpId}/availability", new { isAvailable = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Client.PutAsJsonAsync($"/api/mesh/links/{s.Links[0].Id}/availability", new { isAvailable = false })).StatusCode);
        Assert.True((await s.Client.GetAsync("/api/mesh")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task ConcurrentFailureRequestsKeepEverySnapshotInternallyConsistent()
    {
        await using var s = new Scenario(); await s.StartAsync(); await s.RegisterRelayAsync();
        await Task.WhenAll(Enumerable.Range(0, 30).Select(async i =>
        {
            var snapshot = await s.Node(s.Host.MoistureId, i % 2 == 0);
            var available = snapshot.Nodes.Where(n => n.IsAvailable).Select(n => n.Id).ToHashSet();
            Assert.All(snapshot.Links.Where(e => e.IsAvailable), e => Assert.True(available.Contains(e.SourceId) && available.Contains(e.TargetId)));
            Assert.All(snapshot.Forest.Edges, e => Assert.Contains(snapshot.Links, l => l.Id == e.Id && l.IsAvailable));
            Assert.Equal(snapshot.Forest.Edges.Sum(e => e.PowerCostMilliJoules), snapshot.Forest.TotalPowerCostMilliJoules);
        }));
    }
}
