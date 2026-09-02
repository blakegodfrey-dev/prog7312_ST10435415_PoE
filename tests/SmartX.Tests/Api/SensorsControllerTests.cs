using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Sensors;
using SmartX.Api.Controllers;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Tests.Api;

public sealed class SensorsControllerTests
{
    [Fact]
    public async Task GetAll_FiltersSensorsByCategory()
    {
        await using var context = CreateContext();
        var node = CreateDeploymentNode();
        context.DeploymentNodes.Add(node);
        context.Sensors.AddRange(
            CreateSensor(node.Id, 1, SensorCategory.Environmental),
            CreateSensor(node.Id, 2, SensorCategory.PowerConsumption));
        await context.SaveChangesAsync();
        var controller = new SensorsController(context);

        var action = await controller.GetAll(
            SensorCategory.Environmental,
            null,
            null,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var sensors = Assert.IsAssignableFrom<
            IReadOnlyList<SensorResponse>>(ok.Value);
        var sensor = Assert.Single(sensors);
        Assert.Equal(SensorCategory.Environmental, sensor.Category);
        Assert.Equal(node.Code, sensor.DeploymentLocation.Code);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundForMissingSensor()
    {
        await using var context = CreateContext();
        var controller = new SensorsController(context);

        var action = await controller.GetById(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    [Fact]
    public async Task GetById_ReturnsSensorWithDeploymentLocation()
    {
        await using var context = CreateContext();
        var node = CreateDeploymentNode();
        var sensor = CreateSensor(
            node.Id,
            1,
            SensorCategory.Environmental);
        context.DeploymentNodes.Add(node);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var controller = new SensorsController(context);

        var action = await controller.GetById(
            sensor.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<SensorResponse>(ok.Value);
        Assert.Equal(sensor.Id, response.Id);
        Assert.Equal(node.Id, response.DeploymentLocation.Id);
    }

    [Fact]
    public async Task Register_PersistsAValidSensor()
    {
        await using var context = CreateContext();
        var node = CreateDeploymentNode();
        context.DeploymentNodes.Add(node);
        await context.SaveChangesAsync();
        var controller = new SensorsController(context);
        var request = CreateRequest(node.Id);

        var action = await controller.Register(
            request,
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<SensorResponse>(created.Value);
        Assert.Equal(request.Id, response.Id);
        Assert.Equal(node.Code, response.DeploymentLocation.Code);
        Assert.True(await context.Sensors.AnyAsync(sensor =>
            sensor.Id == request.Id));
    }

    [Fact]
    public async Task Register_RejectsMissingDeploymentNode()
    {
        await using var context = CreateContext();
        var controller = new SensorsController(context);
        var request = CreateRequest(Guid.NewGuid());

        var action = await controller.Register(
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(context.Sensors);
    }

    [Fact]
    public async Task Register_RejectsDuplicateSensorIdentifier()
    {
        await using var context = CreateContext();
        var node = CreateDeploymentNode();
        var existingSensor = CreateSensor(
            node.Id,
            1,
            SensorCategory.Environmental);
        context.DeploymentNodes.Add(node);
        context.Sensors.Add(existingSensor);
        await context.SaveChangesAsync();
        var controller = new SensorsController(context);
        var request = CreateRequest(node.Id) with
        {
            Id = existingSensor.Id
        };

        var action = await controller.Register(
            request,
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Single(context.Sensors);
    }

    [Fact]
    public async Task Register_RejectsDuplicateMacAddress()
    {
        await using var context = CreateContext();
        var node = CreateDeploymentNode();
        var existingSensor = CreateSensor(
            node.Id,
            1,
            SensorCategory.Environmental);
        context.DeploymentNodes.Add(node);
        context.Sensors.Add(existingSensor);
        await context.SaveChangesAsync();
        var controller = new SensorsController(context);
        var request = CreateRequest(node.Id) with
        {
            MacAddress = existingSensor.MacAddress.ToLowerInvariant()
        };

        var action = await controller.Register(
            request,
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Single(context.Sensors);
    }

    [Fact]
    public async Task Register_RejectsLocationAboveNodeLevel()
    {
        await using var context = CreateContext();
        var zone = new DeploymentNode(
            Guid.NewGuid(),
            "Test Zone",
            "ZONE-TEST",
            DeploymentNodeType.Zone);
        context.DeploymentNodes.Add(zone);
        await context.SaveChangesAsync();
        var controller = new SensorsController(context);
        var request = CreateRequest(zone.Id);

        var action = await controller.Register(
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(context.Sensors);
    }

    [Fact]
    public async Task Register_RejectsInvalidExpectedRange()
    {
        await using var context = CreateContext();
        var node = CreateDeploymentNode();
        context.DeploymentNodes.Add(node);
        await context.SaveChangesAsync();
        var controller = new SensorsController(context);
        var request = CreateRequest(node.Id) with
        {
            ExpectedMinimum = 30,
            ExpectedMaximum = 10
        };

        var action = await controller.Register(
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(context.Sensors);
    }

    private static SmartXDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SmartXDbContext(options);
    }

    private static DeploymentNode CreateDeploymentNode()
    {
        return new DeploymentNode(
            Guid.NewGuid(),
            "Test Hydroponic Node",
            ($"NODE-{Guid.NewGuid():N}")[..20],
            DeploymentNodeType.Node);
    }

    private static Sensor CreateSensor(
        Guid deploymentNodeId,
        int number,
        SensorCategory category)
    {
        return new Sensor(
            Guid.NewGuid(),
            $"A4:CF:12:8B:40:{number:X2}",
            $"Test Sensor {number}",
            category,
            "Temperature",
            TelemetryValueKind.Float,
            "°C",
            deploymentNodeId,
            18,
            28);
    }

    private static RegisterSensorRequest CreateRequest(Guid deploymentNodeId)
    {
        return new RegisterSensorRequest(
            Guid.NewGuid(),
            "A4:CF:12:8B:41:01",
            "New Air Temperature Sensor",
            SensorCategory.Environmental,
            "Air temperature",
            TelemetryValueKind.Float,
            "°C",
            deploymentNodeId,
            18,
            28);
    }
}
