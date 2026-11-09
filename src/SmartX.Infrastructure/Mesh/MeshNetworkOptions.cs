using SmartX.Domain.Mesh;

namespace SmartX.Infrastructure.Mesh;

public sealed class MeshLinkDefinition
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
    public decimal DistanceMetres { get; set; }
    public decimal DeliveryProbability { get; set; } = 1m;
    public bool IsEnabled { get; set; } = true;
}

public sealed class MeshNetworkOptions
{
    public Guid GatewayId { get; set; } = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public string GatewayName { get; set; } = "Hydroponic gateway";
    public string SubnetId { get; set; } = "hydroponic-main";
    public bool AllowSimulation { get; set; }
    public List<MeshLinkDefinition>? Links { get; set; }

    public static bool IsValid(MeshNetworkOptions options)
    {
        if (options.GatewayId == Guid.Empty || string.IsNullOrWhiteSpace(options.GatewayName) ||
            string.IsNullOrWhiteSpace(options.SubnetId) || options.GatewayName.Length > 100 ||
            options.SubnetId.Length > 64 || options.Links is null || options.Links.Any(link => link is null)) return false;
        try
        {
            var ids = options.Links.SelectMany(e => new[] { e.SourceId, e.TargetId }).Append(options.GatewayId).Distinct();
            var nodes = ids.Select(id => new MeshVertex(id, "Configured node", null, options.SubnetId, id == options.GatewayId, true));
            var edges = options.Links.Select(e => new MeshEdge(e.Id, e.SourceId, e.TargetId,
                WirelessPowerCost.Estimate(e.DistanceMetres, e.DeliveryProbability), e.IsEnabled));
            _ = new WeightedMeshGraph(nodes, edges);
            return true;
        }
        catch (ArgumentException) { return false; }
    }
}

/// <summary>Declared simulated wireless links between the existing twelve seeded sensor IDs.</summary>
public static class MeshDemoTopology
{
    public static Guid SensorId(int number) => Guid.Parse($"20000000-0000-0000-0000-{number:X12}");
    public static List<MeshLinkDefinition> CreateLinks(Guid gatewayId)
    {
        var links = new List<MeshLinkDefinition>();
        void Add(Guid a, Guid b, decimal distance, decimal probability = 1m) => links.Add(new()
        {
            Id = Guid.Parse($"50000000-0000-0000-0000-{links.Count + 1:X12}"),
            SourceId = a, TargetId = b, DistanceMetres = distance, DeliveryProbability = probability
        });
        Add(gatewayId, SensorId(1), 10);
        Add(gatewayId, SensorId(2), 18);
        for (var i = 1; i < 12; i++) Add(SensorId(i), SensorId(i + 1), 8);
        Add(SensorId(12), SensorId(1), 15);
        Add(SensorId(1), SensorId(3), 20);
        Add(SensorId(2), SensorId(4), 18);
        Add(gatewayId, SensorId(4), 30, 0.95m);
        Add(SensorId(6), SensorId(9), 12, 0.9m);
        return links;
    }
}
