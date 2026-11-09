namespace SmartX.Application.Mesh;

public sealed record MeshNodeStatus(Guid Id, Guid? SensorId, string Name, string? MacAddress,
    string SubnetId, bool IsGateway, bool IsRegistered, bool IsAvailable,
    string ConnectionState, DateTimeOffset? LastSeenAtUtc, string AvailabilityReason);
public sealed record MeshLinkStatus(Guid Id, Guid SourceId, Guid TargetId, decimal DistanceMetres,
    decimal DeliveryProbability, decimal PowerCostMilliJoules, bool IsAvailable, string AvailabilityReason);
public sealed record MeshSnapshot(DateTimeOffset GeneratedAtUtc, Guid GatewayId, string SubnetId,
    long SimulationRevision, bool SimulationEnabled, IReadOnlyList<MeshNodeStatus> Nodes,
    IReadOnlyList<MeshLinkStatus> Links, MeshTraversal Reachability, MeshForest Forest)
{
    public string NetworkState => !Nodes.Single(n => n.IsGateway).IsAvailable ? "GatewayUnavailable"
        : Reachability.UnreachableNodeIds.Count > 0 ? "Degraded" : "Connected";
    public string CostModel => "Simulated expected mJ/delivered packet: (0.05 + 0.0005 * distanceMetres^2) / deliveryProbability. Not measured radio energy.";
    public string HealthPolicy => "Routing requires a registered sensor with a gateway receipt; Connected and Stale are available, Disconnected/Unknown are unavailable. Simulated failures can only remove availability.";
}
