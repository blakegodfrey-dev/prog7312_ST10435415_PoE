namespace SmartX.Domain.Validation;

/// <summary>
/// Contains the outcome of deployment hierarchy validation.
/// </summary>
public sealed class DeploymentHierarchyValidationResult
{
    private readonly IReadOnlyList<string> _errors;

    internal DeploymentHierarchyValidationResult(
        List<string> errors,
        int nodesVisited,
        int maximumDepthReached)
    {
        ArgumentNullException.ThrowIfNull(errors);

        _errors = errors.AsReadOnly();
        NodesVisited = nodesVisited;
        MaximumDepthReached = maximumDepthReached;
    }

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyList<string> Errors => _errors;

    public int NodesVisited { get; }

    public int MaximumDepthReached { get; }
}