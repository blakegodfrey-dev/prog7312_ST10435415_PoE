using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Api.Controllers;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Tests.Api;

public sealed class SensorConnectionStatusControllerTests
{
    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_ReturnsNotFoundForMissingSensor()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var action = await controller.Get(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    [Fact]
    public async Task Get_ReturnsNoDataForSensorWithoutTelemetry()
    {
        await using var context = CreateContext();

        var node = new DeploymentNode(
            Guid.NewGuid(),
            "Test Sensor Node",
            "NODE-STATUS",
            DeploymentNodeType.Node);

        var sensor = new Sensor(
            Guid.NewGuid(),
            "A4:CF:12:8B:50:01",
            "Status Test Sensor",
            SensorCategory.Environmental,
            "Air temperature",
            TelemetryValueKind.Float,
            "°C",
            node.Id,
            18,
            28);

        context.DeploymentNodes.Add(node);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();

        var controller = CreateController(context);

        var action = await controller.Get(
            sensor.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<
            SensorConnectionStatusResponse>(ok.Value);

        Assert.Equal(sensor.Id, response.SensorId);
        Assert.Equal(SensorConnectionStatus.NoData, response.Status);
        Assert.Null(response.LastRecordedAtUtc);
        Assert.Null(response.SecondsSinceLastReading);
        Assert.Equal(CurrentUtc, response.EvaluatedAtUtc);
        Assert.Equal(5, response.ConnectedThresholdMinutes);
        Assert.Equal(15, response.DisconnectedThresholdMinutes);
    }

    private static SensorConnectionStatusController CreateController(
        SmartXDbContext context)
    {
        return new SensorConnectionStatusController(
            context,
            new FixedTimeProvider(CurrentUtc));
    }

    private static SmartXDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SmartXDbContext(options);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
