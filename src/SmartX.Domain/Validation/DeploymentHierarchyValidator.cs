using SmartX.Domain.Entities;
using SmartX.Domain.Enums;

namespace SmartX.Domain.Validation;

/// <summary>
/// Recursively validates a Smart-X physical deployment hierarchy.
/// </summary>
public static class DeploymentHierarchyValidator
{
    public const int DefaultMaximumDepth = 4;

    public static DeploymentHierarchyValidationResult Validate(
        DeploymentNode root,
        int maximumDepth = DefaultMaximumDepth)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (maximumDepth < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDepth),
                "Maximum depth must be at least one.");
        }

        var errors = new List<string>();
        var activePath = new HashSet<Guid>();
        var visitedNodes = new HashSet<Guid>();

        var maximumDepthReached = 0;

        if (root.ParentId is not null)
        {
            errors.Add(
                $"Root deployment node '{root.Code}' cannot have a parent.");
        }

        ValidateNode(
            root,
            DeploymentNodeType.Facility,
            currentDepth: 1,
            maximumDepth,
            activePath,
            visitedNodes,
            errors,
            ref maximumDepthReached);

        return new DeploymentHierarchyValidationResult(
            errors,
            visitedNodes.Count,
            maximumDepthReached);
    }

    private static void ValidateNode(
        DeploymentNode node,
        DeploymentNodeType expectedType,
        int currentDepth,
        int maximumDepth,
        HashSet<Guid> activePath,
        HashSet<Guid> visitedNodes,
        List<string> errors,
        ref int maximumDepthReached)
    {
        maximumDepthReached = Math.Max(
            maximumDepthReached,
            currentDepth);

        // Cycle guard: this node already exists in the current
        // recursive path.
        if (!activePath.Add(node.Id))
        {
            errors.Add(
                $"A deployment cycle was detected at node '{node.Code}'.");

            return;
        }

        try
        {
            // Maximum-depth guard prevents uncontrolled recursion.
            if (currentDepth > maximumDepth)
            {
                errors.Add(
                    $"Deployment node '{node.Code}' exceeds the maximum " +
                    $"allowed depth of {maximumDepth}.");

                return;
            }

            // A previously visited node outside the active path means
            // the same node is being reused in more than one branch.
            if (!visitedNodes.Add(node.Id))
            {
                errors.Add(
                    $"Deployment node '{node.Code}' appears more than once.");

                return;
            }

            if (node.NodeType != expectedType)
            {
                errors.Add(
                    $"Deployment node '{node.Code}' must be " +
                    $"'{expectedType}', but is '{node.NodeType}'.");
            }

            // Clear recursive base case: there are no children left
            // to traverse.
            if (node.Children.Count == 0)
            {
                if (node.NodeType != DeploymentNodeType.Node)
                {
                    errors.Add(
                        $"Deployment node '{node.Code}' ends at " +
                        $"'{node.NodeType}'. A complete hierarchy must " +
                        "end at a Node.");
                }

                return;
            }

            var expectedChildType =
                GetExpectedChildType(node.NodeType);

            if (expectedChildType is null)
            {
                errors.Add(
                    $"Node '{node.Code}' cannot contain child " +
                    "deployment nodes.");
            }

            foreach (var child in node.Children)
            {
                if (child.ParentId != node.Id)
                {
                    errors.Add(
                        $"Child '{child.Code}' does not contain the " +
                        $"correct parent identifier for '{node.Code}'.");
                }

                ValidateNode(
                    child,
                    expectedChildType ?? child.NodeType,
                    currentDepth + 1,
                    maximumDepth,
                    activePath,
                    visitedNodes,
                    errors,
                    ref maximumDepthReached);
            }
        }
        finally
        {
            activePath.Remove(node.Id);
        }
    }

    private static DeploymentNodeType? GetExpectedChildType(
        DeploymentNodeType parentType)
    {
        return parentType switch
        {
            DeploymentNodeType.Facility =>
                DeploymentNodeType.Zone,

            DeploymentNodeType.Zone =>
                DeploymentNodeType.SubZone,

            DeploymentNodeType.SubZone =>
                DeploymentNodeType.Node,

            DeploymentNodeType.Node => null,

            _ => null
        };
    }
}