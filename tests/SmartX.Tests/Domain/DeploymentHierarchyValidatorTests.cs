using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Domain.Validation;
using Xunit;

namespace SmartX.Tests.Domain;

public sealed class DeploymentHierarchyValidatorTests
{
    [Fact]
    public void Validate_AcceptsCompleteDeploymentHierarchy()
    {
        // Arrange
        var facility = CreateNode(
            "Smart Hydroponic Facility",
            "FACILITY-A",
            DeploymentNodeType.Facility);

        var zone = CreateNode(
            "Growing Zone One",
            "ZONE-1",
            DeploymentNodeType.Zone);

        var subZone = CreateNode(
            "Reservoir Sub-zone",
            "SUBZONE-R",
            DeploymentNodeType.SubZone);

        var node = CreateNode(
            "Temperature Node",
            "NODE-TEMP-04",
            DeploymentNodeType.Node);

        facility.AddChild(zone);
        zone.AddChild(subZone);
        subZone.AddChild(node);

        // Act
        var result = DeploymentHierarchyValidator.Validate(facility);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(4, result.NodesVisited);
        Assert.Equal(4, result.MaximumDepthReached);
    }

    [Fact]
    public void Validate_RejectsIncorrectHierarchyOrder()
    {
        // Arrange
        var facility = CreateNode(
            "Smart Hydroponic Facility",
            "FACILITY-A",
            DeploymentNodeType.Facility);

        var subZone = CreateNode(
            "Reservoir Sub-zone",
            "SUBZONE-R",
            DeploymentNodeType.SubZone);

        facility.AddChild(subZone);

        // Act
        var result = DeploymentHierarchyValidator.Validate(facility);

        // Assert
        Assert.False(result.IsValid);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "must be 'Zone', but is 'SubZone'"));
    }

    [Fact]
    public void Validate_RejectsHierarchyEndingBeforeNode()
    {
        // Arrange
        var facility = CreateNode(
            "Smart Hydroponic Facility",
            "FACILITY-A",
            DeploymentNodeType.Facility);

        var zone = CreateNode(
            "Growing Zone One",
            "ZONE-1",
            DeploymentNodeType.Zone);

        facility.AddChild(zone);

        // Act
        var result = DeploymentHierarchyValidator.Validate(facility);

        // Assert
        Assert.False(result.IsValid);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "complete hierarchy must end at a Node"));
    }

    [Fact]
    public void Validate_RejectsNodeContainingChildren()
    {
        // Arrange
        var facility = CreateCompleteHierarchy(
            out var finalNode);

        var invalidChild = CreateNode(
            "Unexpected Child",
            "NODE-CHILD",
            DeploymentNodeType.Node);

        finalNode.AddChild(invalidChild);

        // Act
        var result = DeploymentHierarchyValidator.Validate(facility);

        // Assert
        Assert.False(result.IsValid);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "cannot contain child deployment nodes"));
    }

    [Fact]
    public void Validate_DetectsCycle()
    {
        // Arrange
        var facility = CreateNode(
            "Smart Hydroponic Facility",
            "FACILITY-A",
            DeploymentNodeType.Facility);

        var zone = CreateNode(
            "Growing Zone One",
            "ZONE-1",
            DeploymentNodeType.Zone);

        facility.AddChild(zone);

        // Creates Facility -> Zone -> Facility.
        zone.AddChild(facility);

        // Act
        var result = DeploymentHierarchyValidator.Validate(facility);

        // Assert
        Assert.False(result.IsValid);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "deployment cycle was detected"));
    }

    [Fact]
    public void Validate_StopsWhenMaximumDepthIsExceeded()
    {
        // Arrange
        var facility = CreateCompleteHierarchy(
            out _);

        // Act
        var result = DeploymentHierarchyValidator.Validate(
            facility,
            maximumDepth: 3);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(4, result.MaximumDepthReached);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "exceeds the maximum allowed depth of 3"));
    }

    private static DeploymentNode CreateCompleteHierarchy(
        out DeploymentNode finalNode)
    {
        var facility = CreateNode(
            "Smart Hydroponic Facility",
            "FACILITY-A",
            DeploymentNodeType.Facility);

        var zone = CreateNode(
            "Growing Zone One",
            "ZONE-1",
            DeploymentNodeType.Zone);

        var subZone = CreateNode(
            "Reservoir Sub-zone",
            "SUBZONE-R",
            DeploymentNodeType.SubZone);

        finalNode = CreateNode(
            "Temperature Node",
            "NODE-TEMP-04",
            DeploymentNodeType.Node);

        facility.AddChild(zone);
        zone.AddChild(subZone);
        subZone.AddChild(finalNode);

        return facility;
    }

    private static DeploymentNode CreateNode(
        string name,
        string code,
        DeploymentNodeType nodeType)
    {
        return new DeploymentNode(
            Guid.NewGuid(),
            name,
            code,
            nodeType);
    }
}