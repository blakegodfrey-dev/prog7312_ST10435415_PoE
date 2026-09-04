using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.DeploymentNodes;
using SmartX.Api.Controllers;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Tests.Api;

public sealed class DeploymentNodesControllerTests
{
    [Fact]
    public async Task GetAll_FiltersLocationsByNodeType()
    {
        await using var context = CreateContext();

        context.DeploymentNodes.AddRange(
            new DeploymentNode(
                Guid.NewGuid(),
                "Facility",
                "FAC-TEST",
                DeploymentNodeType.Facility),
            new DeploymentNode(
                Guid.NewGuid(),
                "Sensor Node",
                "NODE-TEST",
                DeploymentNodeType.Node));

        await context.SaveChangesAsync();

        var controller = new DeploymentNodesController(context);

        var action = await controller.GetAll(
            DeploymentNodeType.Node,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var nodes = Assert.IsAssignableFrom<
            IReadOnlyList<DeploymentNodeResponse>>(ok.Value);

        var node = Assert.Single(nodes);
        Assert.Equal(DeploymentNodeType.Node, node.NodeType);
        Assert.Equal("NODE-TEST", node.Code);
    }

    [Fact]
    public async Task GetAll_ReturnsAllLocationsInStableOrder()
    {
        await using var context = CreateContext();

        context.DeploymentNodes.AddRange(
            new DeploymentNode(
                Guid.NewGuid(),
                "Zulu Node",
                "NODE-ZULU",
                DeploymentNodeType.Node),
            new DeploymentNode(
                Guid.NewGuid(),
                "Alpha Facility",
                "FAC-ALPHA",
                DeploymentNodeType.Facility),
            new DeploymentNode(
                Guid.NewGuid(),
                "Alpha Node",
                "NODE-ALPHA",
                DeploymentNodeType.Node));

        await context.SaveChangesAsync();

        var controller = new DeploymentNodesController(context);

        var action = await controller.GetAll(
            null,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var nodes = Assert.IsAssignableFrom<
            IReadOnlyList<DeploymentNodeResponse>>(ok.Value);

        Assert.Collection(
            nodes,
            node => Assert.Equal("FAC-ALPHA", node.Code),
            node => Assert.Equal("NODE-ALPHA", node.Code),
            node => Assert.Equal("NODE-ZULU", node.Code));
    }

    private static SmartXDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SmartXDbContext(options);
    }
}
