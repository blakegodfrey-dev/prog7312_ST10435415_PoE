using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Api.Contracts.Live;
using SmartX.Api.Contracts.Sensors;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Api.Controllers;
using SmartX.Application.Live;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Infrastructure;
using SmartX.Infrastructure.Live;

namespace SmartX.Tests.Live;

public sealed class LiveTelemetryIntegrationTests
{
    [Fact]
    public async Task HydrationIncludesNoDataDevicesAndLatestReadingsOutsideTheRecentWindow()
    {
        await using var context = LiveTestData.Context();
        var quiet = LiveTestData.Sensor(1, TelemetryValueKind.Boolean);
        var busy = LiveTestData.Sensor(2);
        var noData = LiveTestData.Sensor(3);
        context.Sensors.AddRange(quiet, busy, noData);
        context.TelemetryRecords.Add(LiveTestData.Record(quiet, -10, 0));
        context.TelemetryRecords.AddRange(Enumerable.Range(1, 6).Select(i => LiveTestData.Record(busy, i, i)));
        await context.SaveChangesAsync();
        var store = new LiveTelemetryStore(2);
        await new LiveTelemetryService(context, store).EnsureInitializedAsync(CancellationToken.None);
        Assert.Equal(3, store.GetDevices().Count);
        Assert.False(store.FindById(quiet.Id)!.LatestReading!.BooleanValue);
        Assert.Null(store.FindById(noData.Id)!.LatestReading);
        Assert.Null(store.FindById(noData.Id)!.LastReceivedAtUtc);
        Assert.Equal(new[] { 5, 6 }, store.GetRecentHistory().Readings.Select(r => r.IntegerValue!.Value));
        Assert.Equal(7, await context.TelemetryRecords.CountAsync());
    }

    [Fact]
    public async Task HydrationSeparatesLatestRecordedValueFromMaximumPersistedReceivedTime()
    {
        await using var context = LiveTestData.Context();
        var sensor = LiveTestData.Sensor();
        context.Sensors.Add(sensor);
        context.TelemetryRecords.AddRange(LiveTestData.Record(sensor, 3, 30, received: LiveTestData.Time.AddMinutes(4)),
            LiveTestData.Record(sensor, 1, 10, received: LiveTestData.Time.AddMinutes(9)));
        await context.SaveChangesAsync();
        var store = new LiveTelemetryStore(1);
        await new LiveTelemetryService(context, store).EnsureInitializedAsync(CancellationToken.None);
        Assert.Equal(30, store.FindById(sensor.Id)!.LatestReading!.IntegerValue);
        Assert.Equal(LiveTestData.Time.AddMinutes(9), store.FindById(sensor.Id)!.LastReceivedAtUtc);
    }

    [Fact]
    public async Task TypedIngestionUsesTheSharedRegistryAndPublishesAfterPersistence()
    {
        await using var context = LiveTestData.Context();
        var sensor = LiveTestData.Sensor(1, TelemetryValueKind.Float);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var store = new LiveTelemetryStore();
        var live = new LiveTelemetryService(context, store);
        var controller = new TelemetryController(context, live);
        var id = Guid.NewGuid();
        var result = await controller.IngestFloat(new TelemetryIngestionRequest<float>(
            id, sensor.Id, 22.5f, LiveTestData.Time, LiveTestData.Time.AddSeconds(1)), CancellationToken.None);
        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(id, (await context.TelemetryRecords.SingleAsync()).Id);
        var device = store.FindByMac(" a4:cf:12:8b:40:01 ")!;
        Assert.Equal(id, device.LatestReading!.Id);
        Assert.Equal(22.5f, device.LatestReading.FloatValue);
        Assert.Null(device.LatestReading.IntegerValue);
        Assert.Null(device.LatestReading.BooleanValue);
        Assert.Equal(id, Assert.Single(store.GetRecentHistory().Readings).Id);
    }

    [Fact]
    public async Task BulkIngestionPreservesMixedNativeTypesValidationAndSameTimestampReadings()
    {
        await using var context = LiveTestData.Context();
        var sensors = new[] { LiveTestData.Sensor(1, TelemetryValueKind.Float), LiveTestData.Sensor(2), LiveTestData.Sensor(3, TelemetryValueKind.Boolean) };
        context.Sensors.AddRange(sensors);
        await context.SaveChangesAsync();
        var live = new LiveTelemetryService(context, new LiveTelemetryStore());
        var action = await new TelemetryController(context, live).IngestBulk(new BulkTelemetryIngestionRequest([
            new(Guid.NewGuid(), sensors[0].Id, TelemetryValueKind.Float, 1200f, null, null, LiveTestData.Time, LiveTestData.Time.AddSeconds(1)),
            new(Guid.NewGuid(), sensors[1].Id, TelemetryValueKind.Integer, null, 350, null, LiveTestData.Time, LiveTestData.Time.AddSeconds(1)),
            new(Guid.NewGuid(), sensors[2].Id, TelemetryValueKind.Boolean, null, null, false, LiveTestData.Time, LiveTestData.Time.AddSeconds(1))
        ]), CancellationToken.None);
        Assert.Equal(201, Assert.IsType<ObjectResult>(action.Result).StatusCode);
        Assert.Equal(3, await context.TelemetryRecords.CountAsync());
        Assert.Equal(3, live.Store.GetRecentHistory().Readings.Count);
        Assert.False(live.Store.FindById(sensors[0].Id)!.LatestReading!.IsValid);
        Assert.Contains("outside the expected range", live.Store.FindById(sensors[0].Id)!.LatestReading!.ValidationMessage!);
        Assert.False(live.Store.FindById(sensors[2].Id)!.LatestReading!.BooleanValue);
    }

    [Fact]
    public async Task InvalidBulkBatchDoesNotPublishAnyOfItsOtherwiseValidReadings()
    {
        await using var context = LiveTestData.Context();
        var sensor = LiveTestData.Sensor();
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var live = new LiveTelemetryService(context, new LiveTelemetryStore());
        var result = await new TelemetryController(context, live).IngestBulk(new BulkTelemetryIngestionRequest([
            new(Guid.NewGuid(), sensor.Id, TelemetryValueKind.Integer, null, 20, null, LiveTestData.Time, null),
            new(Guid.NewGuid(), sensor.Id, TelemetryValueKind.Boolean, null, null, true, LiveTestData.Time, null)
        ]), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(context.TelemetryRecords);
        Assert.Empty(live.Store.GetRecentHistory().Readings);
        Assert.Null(live.Store.FindById(sensor.Id)!.LatestReading);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSingleOrBulkSaveLeavesCommittedCacheUnchanged(bool bulk)
    {
        var failure = new FailedSaveInterceptor();
        await using var context = LiveTestData.Context(failure);
        var sensor = LiveTestData.Sensor(1, TelemetryValueKind.Boolean);
        context.Sensors.Add(sensor);
        var original = LiveTestData.Record(sensor, 1, 0);
        context.TelemetryRecords.Add(original);
        await context.SaveChangesAsync();
        var live = new LiveTelemetryService(context, new LiveTelemetryStore());
        await live.EnsureInitializedAsync(CancellationToken.None);
        failure.Enabled = true;
        var controller = new TelemetryController(context, live);
        if (bulk)
        {
            var result = await controller.IngestBulk(new BulkTelemetryIngestionRequest([
                new(Guid.NewGuid(), sensor.Id, TelemetryValueKind.Boolean, null, null, true, LiveTestData.Time.AddMinutes(2), null)
            ]), CancellationToken.None);
            Assert.IsType<ConflictObjectResult>(result.Result);
        }
        else
        {
            var result = await controller.IngestBoolean(new TelemetryIngestionRequest<bool>(
                Guid.NewGuid(), sensor.Id, true, LiveTestData.Time.AddMinutes(2), null), CancellationToken.None);
            Assert.IsType<ConflictObjectResult>(result.Result);
        }
        Assert.Equal(original.Id, live.Store.FindById(sensor.Id)!.LatestReading!.Id);
        Assert.False(live.Store.FindById(sensor.Id)!.LatestReading!.BooleanValue);
        Assert.Equal(original.Id, Assert.Single(live.Store.GetRecentHistory().Readings).Id);
        Assert.Single(await context.TelemetryRecords.ToListAsync());
    }

    [Fact]
    public async Task RegistrationIsVisibleInAnAlreadyHydratedRegistry()
    {
        await using var context = LiveTestData.Context();
        context.DeploymentNodes.Add(new DeploymentNode(LiveTestData.NodeId, "Reservoir", "RES-1", DeploymentNodeType.Node));
        await context.SaveChangesAsync();
        var live = new LiveTelemetryService(context, new LiveTelemetryStore());
        await live.EnsureInitializedAsync(CancellationToken.None);
        var sensor = LiveTestData.Sensor(2, TelemetryValueKind.Boolean);
        var request = new RegisterSensorRequest(sensor.Id, sensor.MacAddress.ToLowerInvariant(), sensor.FriendlyName,
            sensor.Category, sensor.MeasuredProperty, sensor.ValueKind, sensor.Unit, sensor.DeploymentNodeId, null, null);
        var controller = new SensorsController(context, live);
        Assert.IsType<CreatedAtActionResult>((await controller.Register(request, CancellationToken.None)).Result);
        Assert.Equal(sensor.Id, live.Store.FindByMac(sensor.MacAddress)!.Id);
        Assert.Null(live.Store.FindByMac(sensor.MacAddress)!.LatestReading);
        Assert.IsType<ConflictObjectResult>((await controller.Register(request, CancellationToken.None)).Result);
        Assert.Single(live.Store.GetDevices());
    }

    [Fact]
    public async Task FailedRegistrationDoesNotCreateAGhostDevice()
    {
        var failure = new FailedSaveInterceptor();
        await using var context = LiveTestData.Context(failure);
        context.DeploymentNodes.Add(new DeploymentNode(LiveTestData.NodeId, "Reservoir", "RES-1", DeploymentNodeType.Node));
        await context.SaveChangesAsync();
        var live = new LiveTelemetryService(context, new LiveTelemetryStore());
        await live.EnsureInitializedAsync(CancellationToken.None);
        failure.Enabled = true;
        var sensor = LiveTestData.Sensor(2, TelemetryValueKind.Boolean);
        var request = new RegisterSensorRequest(sensor.Id, sensor.MacAddress, sensor.FriendlyName,
            sensor.Category, sensor.MeasuredProperty, sensor.ValueKind, sensor.Unit, sensor.DeploymentNodeId, null, null);
        Assert.IsType<ConflictObjectResult>((await new SensorsController(context, live).Register(request, CancellationToken.None)).Result);
        Assert.Empty(live.Store.GetDevices());
        Assert.Empty(await context.Sensors.ToListAsync());
    }

    [Fact]
    public async Task ANewProcessCacheRebuildsTheSameLatestValueAndOrderedWindowFromSqlData()
    {
        await using var context = LiveTestData.Context();
        var sensor = LiveTestData.Sensor();
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var first = new LiveTelemetryService(context, new LiveTelemetryStore(2));
        var controller = new TelemetryController(context, first);
        foreach (var minute in new[] { 3, 1, 2 })
            await controller.IngestInteger(new TelemetryIngestionRequest<int>(Guid.NewGuid(), sensor.Id,
                minute, LiveTestData.Time.AddMinutes(minute), LiveTestData.Time.AddMinutes(minute).AddSeconds(1)), CancellationToken.None);
        var restarted = new LiveTelemetryService(context, new LiveTelemetryStore(2));
        await restarted.EnsureInitializedAsync(CancellationToken.None);
        Assert.Equal(first.Store.FindById(sensor.Id), restarted.Store.FindById(sensor.Id));
        Assert.Equal(first.Store.GetRecentHistory().Readings, restarted.Store.GetRecentHistory().Readings);
        Assert.Equal(3, await context.TelemetryRecords.CountAsync());
    }

    [Fact]
    public async Task TheExistingSqlHistoryEndpointReturnsReadingsEvictedFromTheLiveWindow()
    {
        await using var context = LiveTestData.Context();
        var sensor = LiveTestData.Sensor();
        context.Sensors.Add(sensor);
        context.TelemetryRecords.AddRange(Enumerable.Range(1, 5).Select(i => LiveTestData.Record(sensor, i, i)));
        await context.SaveChangesAsync();
        var live = new LiveTelemetryService(context, new LiveTelemetryStore(2));
        await live.EnsureInitializedAsync(CancellationToken.None);
        var result = await new TelemetryController(context, live).GetHistory(sensor.Id, pageSize: 10);
        var history = Assert.IsType<TelemetryHistoryResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(5, history.TotalCount);
        Assert.Equal(5, history.Readings.Count);
        Assert.Equal(2, live.Store.GetRecentHistory().Readings.Count);
    }

    [Fact]
    public void DependencyInjectionSharesTheStoreAcrossRequestScopesAndActivatesBothControllers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SmartXDatabase"] = "Server=localhost;Database=Test;Integrated Security=true;TrustServerCertificate=true;",
            ["LiveTelemetry:RecentHistoryCapacity"] = "7"
        }).Build();
        services.AddInfrastructure(configuration, Path.GetTempPath());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<LiveTelemetryService>();
        var b = second.ServiceProvider.GetRequiredService<LiveTelemetryService>();
        Assert.NotSame(a, b);
        Assert.Same(a.Store, b.Store);
        Assert.Equal(7, a.Store.HistoryCapacity);
        Assert.NotNull(ActivatorUtilities.CreateInstance<TelemetryController>(first.ServiceProvider));
        Assert.NotNull(ActivatorUtilities.CreateInstance<SensorsController>(first.ServiceProvider));
    }

    private sealed class FailedSaveInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Enabled ? throw new DbUpdateException("Simulated SQL save failure.") : ValueTask.FromResult(result);
    }
}
