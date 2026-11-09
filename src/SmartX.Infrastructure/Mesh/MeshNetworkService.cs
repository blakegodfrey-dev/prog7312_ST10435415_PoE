using Microsoft.Extensions.Options;
using SmartX.Application.Mesh;
using SmartX.Application.Operations;
using SmartX.Domain.Mesh;
using SmartX.Infrastructure.Live;

namespace SmartX.Infrastructure.Mesh;

public sealed record MeshAnalysis(WeightedMeshGraph Graph, MeshSnapshot Snapshot);

/// <summary>Builds a consistent routing snapshot from registered devices, declared links and real gateway receipts.</summary>
public sealed class MeshNetworkService(LiveTelemetryService live, IncidentTracker incidents,
    MeshSimulationState simulation, IOptions<MeshNetworkOptions> configuration, TimeProvider clock)
{
    public Guid? FindSensorIdByMac(string canonicalMac) => live.Store.FindByMac(canonicalMac)?.Id;

    public async Task<MeshAnalysis> AnalyseAsync(CancellationToken token)
    {
        await live.EnsureInitializedAsync(token);
        token.ThrowIfCancellationRequested();
        var options = configuration.Value;
        var definitions = options.Links ?? throw new InvalidOperationException("Mesh links have not been configured.");
        var failures = simulation.Snapshot();
        var devices = live.Store.GetDevices().ToDictionary(d => d.Id);
        if (devices.ContainsKey(options.GatewayId))
            throw new InvalidOperationException("Configured gateway ID conflicts with a registered sensor ID.");
        var configured = definitions.SelectMany(e => new[] { e.SourceId, e.TargetId })
            .Concat(devices.Keys).Append(options.GatewayId).Distinct().Order().ToArray();
        var nodes = new List<MeshNodeStatus>(configured.Length);
        foreach (var id in configured)
        {
            token.ThrowIfCancellationRequested();
            if (id == options.GatewayId)
            {
                var enabled = !failures.FailedNodes.Contains(id);
                nodes.Add(new(id, null, options.GatewayName, null, options.SubnetId, true, false,
                    enabled, enabled ? "GatewayReady" : "SimulatedFailure", clock.GetUtcNow(),
                    enabled ? "GatewayReady" : "SimulatedNodeFailure"));
                continue;
            }
            if (!devices.TryGetValue(id, out var device))
            {
                nodes.Add(new(id, null, $"Unregistered ESP32 {id.ToString("D")[..8]}-{id.ToString("D")[^4..]}", null,
                    options.SubnetId, false, false, false, "Unknown", null, "NotRegistered"));
                continue;
            }
            var connection = incidents.GetConnection(id);
            var contact = connection.LastSeenAtUtc.HasValue && (connection.State is "Connected" or "Stale");
            var failed = failures.FailedNodes.Contains(id);
            nodes.Add(new(id, id, device.FriendlyName, device.MacAddress, options.SubnetId, false, true,
                contact && !failed, connection.State, connection.LastSeenAtUtc,
                failed ? "SimulatedNodeFailure" : contact ? "Available" : "NoRecentGatewayContact"));
        }
        var byId = nodes.ToDictionary(n => n.Id);
        var links = definitions.Select(link =>
        {
            var reason = !link.IsEnabled ? "ConfiguredDisabled" : failures.FailedLinks.Contains(link.Id) ? "SimulatedLinkFailure"
                : !byId[link.SourceId].IsAvailable || !byId[link.TargetId].IsAvailable ? "EndpointUnavailable" : "Available";
            return new MeshLinkStatus(link.Id, link.SourceId, link.TargetId, link.DistanceMetres,
                link.DeliveryProbability, WirelessPowerCost.Estimate(link.DistanceMetres, link.DeliveryProbability),
                reason == "Available", reason);
        }).OrderBy(e => e.Id).ToArray();
        var graph = new WeightedMeshGraph(nodes.Select(n => new MeshVertex(n.Id, n.Name, n.MacAddress,
            n.SubnetId, n.IsGateway, n.IsAvailable)), links.Select(e => new MeshEdge(e.Id, e.SourceId, e.TargetId,
            e.PowerCostMilliJoules, e.IsAvailable)));
        var reachable = MeshAlgorithms.BreadthFirstSearch(graph, options.GatewayId, token);
        var forest = MeshAlgorithms.MinimumSpanningForest(graph, options.GatewayId, token);
        var snapshot = new MeshSnapshot(clock.GetUtcNow(), options.GatewayId, options.SubnetId,
            failures.Revision, options.AllowSimulation, nodes.AsReadOnly(), Array.AsReadOnly(links), reachable, forest);
        return new(graph, snapshot);
    }
}
