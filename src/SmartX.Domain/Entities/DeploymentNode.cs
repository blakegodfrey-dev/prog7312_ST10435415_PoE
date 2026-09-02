using SmartX.Domain.Enums;

namespace SmartX.Domain.Entities;

/// <summary>
/// Represents a physical location in a Smart-X deployment hierarchy,
/// such as a facility, zone, sub-zone or individual node.
/// </summary>
public sealed class DeploymentNode
{
    private readonly List<DeploymentNode> _children = [];

    private DeploymentNode()
    {
        // Required later by Entity Framework Core.
    }

    public DeploymentNode(
        Guid id,
        string name,
        string code,
        DeploymentNodeType nodeType)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "A deployment node must have a valid identifier.",
                nameof(id));
        }

        Id = id;
        Name = RequireText(name, nameof(name));
        Code = RequireText(code, nameof(code)).ToUpperInvariant();
        NodeType = nodeType;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public DeploymentNodeType NodeType { get; private set; }

    public Guid? ParentId { get; private set; }

    public DeploymentNode? Parent { get; private set; }

    public IReadOnlyCollection<DeploymentNode> Children => _children.AsReadOnly();

    /// <summary>
    /// Connects a child location to this deployment node.
    /// Complete hierarchy and cycle validation will be added separately.
    /// </summary>
    public void AddChild(DeploymentNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.Id == Id)
        {
            throw new InvalidOperationException(
                "A deployment node cannot be its own child.");
        }

        if (_children.Any(existingChild => existingChild.Id == child.Id))
        {
            throw new InvalidOperationException(
                $"Deployment node '{child.Code}' is already a child of '{Code}'.");
        }

        if (child.ParentId is not null && child.ParentId != Id)
        {
            throw new InvalidOperationException(
                $"Deployment node '{child.Code}' already belongs to another parent.");
        }

        child.ParentId = Id;
        child.Parent = this;
        _children.Add(child);
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A value is required.",
                parameterName);
        }

        return value.Trim();
    }
}