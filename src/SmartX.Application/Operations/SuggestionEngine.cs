using SmartX.Application.Live;
using SmartX.Domain.Enums;

namespace SmartX.Application.Operations;

public sealed record BehaviourEvidence(Guid Id, string Context, Guid TargetId, string Action, bool? DesiredState, bool Successful);
public sealed record SuggestedAction(Guid TargetId, string Action, bool? DesiredState, string Context,
    int Support, int ContextObservations, double Confidence, double Rank, string Reason);

/// <summary>Conditional frequency learning: count distinct evidence per context/action,
/// divide by observations of that context in its command or inspection channel, rank by confidence * log(1+support).
/// Dictionary aggregation O(n); ranking O(k log k). No threshold-only recommendations.</summary>
public static class SuggestionEngine
{
    public static IReadOnlyList<SuggestedAction> Learn(IEnumerable<BehaviourEvidence> evidence,
        IReadOnlyList<DeviceSnapshot> devices, IReadOnlySet<Guid> unavailable, IReadOnlySet<string> contexts,
        int minimumSupport = 3, double minimumConfidence = .6)
    {
        var denominators = new Dictionary<(string Context, string Channel), int>();
        var counts = new Dictionary<(string Context, Guid Target, string Action, bool? State), int>();
        var distinct = new HashSet<Guid>();
        foreach (var e in evidence)
        {
            if (!distinct.Add(e.Id) || !e.Successful) continue;
            if (e.Action is not ("command" or "view" or "search")) continue;
            var action = e.Action == "command" ? "command" : "view";
            var denominator = (e.Context, action);
            denominators[denominator] = denominators.GetValueOrDefault(denominator) + 1;
            var key = (e.Context, e.TargetId, action, e.DesiredState);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        var byId = devices.ToDictionary(d => d.Id);
        var result = new List<SuggestedAction>();
        foreach (var (key, support) in counts)
        {
            if (!contexts.Contains(key.Context) || support < minimumSupport || unavailable.Contains(key.Target) || !byId.TryGetValue(key.Target, out var target)) continue;
            var total = denominators[(key.Context, key.Action)]; var confidence = (double)support / total;
            if (confidence < minimumConfidence) continue;
            if (key.Action == "command" && (target.Category != SensorCategory.Actuator || target.ValueKind != TelemetryValueKind.Boolean ||
                target.LatestReading is not { IsValid: true, BooleanValue: not null } || target.LatestReading.BooleanValue == key.State)) continue;
            if (key.Action is not ("command" or "view" or "search")) continue;
            var parts = key.Context.Split(':', 2);
            var contextDescription = parts.Length == 2 && Guid.TryParse(parts[0], out var sourceId) && byId.TryGetValue(sourceId, out var source)
                ? $"{source.FriendlyName} is {(parts[1] == "BelowMinimum" ? "below its minimum" : "above its maximum")} ({source.LatestReading?.FloatValue?.ToString() ?? source.LatestReading?.IntegerValue?.ToString()} {source.Unit})."
                : key.Context;
            result.Add(new(key.Target, key.Action, key.State, key.Context, support, total, confidence,
                confidence * Math.Log(1 + support), $"In {support} of {total} similar {(key.Action == "command" ? "manual command" : "search / view")} observations, operators {(key.Action == "command" ? $"set {target.FriendlyName} {(key.State == true ? "ON" : "OFF")}" : $"inspected {target.FriendlyName}")}. Current context: {contextDescription}"));
        }
        // One highest-ranked action per device prevents conflicting ON/OFF suggestions.
        return result.OrderByDescending(s => s.Rank).ThenBy(s => s.TargetId).ThenBy(s => s.Action, StringComparer.Ordinal)
            .ThenBy(s => s.DesiredState).GroupBy(s => s.TargetId).Select(g => g.First()).Take(10).ToArray();
    }
}
