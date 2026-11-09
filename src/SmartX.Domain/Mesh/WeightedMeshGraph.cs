using System.Collections.ObjectModel;

namespace SmartX.Domain.Mesh;

public sealed record MeshVertex(Guid Id, string Name, string? MacAddress,
    string SubnetId, bool IsGateway, bool IsAvailable);

public sealed record MeshEdge(Guid Id, Guid SourceId, Guid TargetId,
    decimal PowerCostMilliJoules, bool IsAvailable = true)
{
    public Guid Other(Guid id) => id == SourceId ? TargetId : id == TargetId ? SourceId
        : throw new ArgumentException("The vertex is not an endpoint of this link.", nameof(id));
}

/// <summary>An immutable, undirected adjacency-list graph of a single mesh snapshot.</summary>
public sealed class WeightedMeshGraph
{
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<MeshEdge>> _adjacency;
    public IReadOnlyDictionary<Guid, MeshVertex> Vertices { get; }
    public IReadOnlyList<MeshEdge> Edges { get; }

    public WeightedMeshGraph(IEnumerable<MeshVertex> vertices, IEnumerable<MeshEdge> edges)
    {
        var nodes = vertices.ToArray();
        var links = edges.ToArray();
        if (nodes.Any(n => n.Id == Guid.Empty || string.IsNullOrWhiteSpace(n.Name) || string.IsNullOrWhiteSpace(n.SubnetId)) ||
            nodes.Select(n => n.Id).Distinct().Count() != nodes.Length)
            throw new ArgumentException("Mesh vertices require unique non-empty IDs, names and subnet IDs.");
        var byId = nodes.ToDictionary(n => n.Id);
        var ids = new HashSet<Guid>();
        var pairs = new HashSet<(Guid, Guid)>();
        decimal totalCostBudget = 0;
        foreach (var edge in links)
        {
            var pair = edge.SourceId.CompareTo(edge.TargetId) < 0 ? (edge.SourceId, edge.TargetId) : (edge.TargetId, edge.SourceId);
            if (edge.Id == Guid.Empty || !ids.Add(edge.Id) || edge.SourceId == edge.TargetId ||
                !byId.ContainsKey(edge.SourceId) || !byId.ContainsKey(edge.TargetId) ||
                edge.PowerCostMilliJoules < 0 || !pairs.Add(pair))
                throw new ArgumentException("Links require unique IDs/pairs, two known distinct endpoints and non-negative costs.");
            try { totalCostBudget += edge.PowerCostMilliJoules; }
            catch (OverflowException) { throw new ArgumentException("Combined link costs exceed the supported decimal range."); }
        }
        Vertices = new ReadOnlyDictionary<Guid, MeshVertex>(byId);
        Edges = Array.AsReadOnly(links.OrderBy(e => e.Id).ToArray());
        var adjacency = nodes.ToDictionary(n => n.Id, _ => new List<MeshEdge>());
        foreach (var edge in Edges)
        {
            if (!edge.IsAvailable || !byId[edge.SourceId].IsAvailable || !byId[edge.TargetId].IsAvailable) continue;
            adjacency[edge.SourceId].Add(edge);
            adjacency[edge.TargetId].Add(edge);
        }
        _adjacency = new ReadOnlyDictionary<Guid, IReadOnlyList<MeshEdge>>(adjacency.ToDictionary(
            p => p.Key, p => (IReadOnlyList<MeshEdge>)Array.AsReadOnly(p.Value.OrderBy(e => e.Other(p.Key)).ThenBy(e => e.Id).ToArray())));
    }

    public IReadOnlyList<MeshEdge> AvailableNeighbours(Guid id) => _adjacency.TryGetValue(id, out var links)
        ? links : throw new KeyNotFoundException("Mesh vertex does not exist.");
}

public static class WirelessPowerCost
{
    /// <summary>
    /// Simulated expected transmit energy per delivered packet: a fixed radio
    /// overhead plus a distance-squared term, divided by delivery probability.
    /// These declared assumptions are not measured ESP32 radio characteristics.
    /// </summary>
    public static decimal Estimate(decimal distanceMetres, decimal deliveryProbability)
    {
        if (distanceMetres is <= 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(distanceMetres));
        if (deliveryProbability is <= 0 or > 1) throw new ArgumentOutOfRangeException(nameof(deliveryProbability));
        try
        {
            return decimal.Round((0.05m + 0.0005m * distanceMetres * distanceMetres) / deliveryProbability,
                6, MidpointRounding.AwayFromZero);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryProbability), "Expected delivery cost exceeds the supported decimal range.");
        }
    }
}
