using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SmartX.Api.Controllers;
using SmartX.Api.Filters;
using SmartX.Application.Live;
using SmartX.Application.Operations;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Infrastructure;
using SmartX.Infrastructure.Operations;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;
namespace SmartX.Tests.Operations;

public sealed class FailureSwitch : SaveChangesInterceptor
{
    public bool FailTelemetry { get; set; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (FailTelemetry && eventData.Context!.ChangeTracker.Entries<TelemetryRecord>().Any(e => e.State == EntityState.Added))
        { FailTelemetry = false; throw new DbUpdateException("Injected persistence failure"); }
        return ValueTask.FromResult(result);
    }
}

public sealed class OperationsHarness : IAsyncDisposable
{
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public ManualClock Clock { get; } = new();
    public FailureSwitch Failure { get; } = new();
    public Guid NodeId { get; } = Guid.NewGuid();
    public Guid PumpId { get; } = Guid.NewGuid();
    public Guid MoistureId { get; } = Guid.NewGuid();
    public async Task StartAsync(int capacity = 5000, int delay = 0, string? database = null, bool useSystemClock = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:SmartXDatabase"] = "Server=localhost;Database=Unused;Integrated Security=true;",
            ["Operations:QueueCapacity"] = capacity.ToString(), ["Operations:ProcessingDelayMilliseconds"] = delay.ToString(),
            ["Operations:StaleSeconds"] = "10", ["Operations:DisconnectedSeconds"] = "20", ["Operations:ScanSeconds"] = "1",
            ["Operations:AllowFaultInjection"] = "true" });
        builder.Services.AddInfrastructure(builder.Configuration, Path.GetTempPath());
        if (!useSystemClock) { builder.Services.RemoveAll<TimeProvider>(); builder.Services.AddSingleton<TimeProvider>(Clock); }
        builder.Services.RemoveAll<DbContextOptions<SmartXDbContext>>(); builder.Services.RemoveAll<IDbContextOptionsConfiguration<SmartXDbContext>>();
        var name = database ?? Guid.NewGuid().ToString();
        builder.Services.AddDbContext<SmartXDbContext>(o => o.UseInMemoryDatabase(name).AddInterceptors(Failure));
        builder.Services.AddControllers(o => o.Filters.Add<InvalidTelemetryFilter>()).AddApplicationPart(typeof(TelemetryController).Assembly);
        builder.Services.AddProblemDetails();
        App = builder.Build(); App.UseExceptionHandler(); App.MapControllers();
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartXDbContext>();
            db.DeploymentNodes.Add(new DeploymentNode(NodeId, "Reservoir", "RES-OPS", DeploymentNodeType.Node));
            db.Sensors.Add(new Sensor(PumpId, "AA:BB:CC:DD:EE:01", "Pump A", SensorCategory.Actuator, "Pump state", TelemetryValueKind.Boolean, "", NodeId));
            db.Sensors.Add(new Sensor(MoistureId, "AA:BB:CC:DD:EE:02", "Moisture B", SensorCategory.Environmental, "Soil moisture", TelemetryValueKind.Float, "%", NodeId, 30, 70));
            await db.SaveChangesAsync();
        }
        await App.StartAsync();
        Client = new HttpClient { BaseAddress = new Uri(App.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        await BooleanAsync(false); await FloatAsync(50);
    }
    public Task<HttpResponseMessage> BooleanAsync(bool value) => Client.PostAsJsonAsync("/api/telemetry/boolean", new {
        id = Guid.NewGuid(), sensorId = PumpId, value, recordedAtUtc = Clock.Now.AddSeconds(-1) });
    public Task<HttpResponseMessage> FloatAsync(float value, Guid? id = null) => Client.PostAsJsonAsync("/api/telemetry/float", new {
        id = id ?? Guid.NewGuid(), sensorId = MoistureId, value, recordedAtUtc = Clock.Now.AddSeconds(-1) });
    public Task<HttpResponseMessage> CommandAsync(bool desired, bool fail = false, Guid? id = null) => Client.PostAsJsonAsync("/api/operations/commands", new { id = id ?? Guid.NewGuid(), sensorId = PumpId, desiredState = desired, simulateFailure = fail });
    public Task<HttpResponseMessage> UndoAsync(bool fail = false, Guid? id = null) => Client.PostAsJsonAsync("/api/operations/undo", new { id = id ?? Guid.NewGuid(), simulateFailure = fail });
    public async Task<JsonElement> DashboardAsync()
    {
        var response = await Client.GetAsync("/api/operations/dashboard"); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>());
    }
    public async ValueTask DisposeAsync() { Client?.Dispose(); if (App is not null) { await App.StopAsync(); await App.DisposeAsync(); } }
}

public sealed class OperationsHttpTests
{
    [Fact]
    public async Task CommandsUndoFailuresConcurrencyAndRestartAreConsistent()
    {
        await using var h = new OperationsHarness(); await h.StartAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await h.UndoAsync()).StatusCode);
        var fail = await (await h.CommandAsync(true, true)).Content.ReadFromJsonAsync<CommandHistoryEntry>(); Assert.False(fail!.Successful);
        Assert.Equal(0, (await h.DashboardAsync()).GetProperty("undoCount").GetInt32());
        var id = Guid.NewGuid();
        var attempts = await Task.WhenAll(h.CommandAsync(true, id: id), h.CommandAsync(true, id: id));
        Assert.All(attempts, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var dashboard = await h.DashboardAsync(); Assert.Equal(1, dashboard.GetProperty("undoCount").GetInt32());
        var replay = await h.CommandAsync(false, id: id); Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        h.Clock.Now += TimeSpan.FromSeconds(1);
        var failedUndo = await (await h.UndoAsync(true)).Content.ReadFromJsonAsync<CommandHistoryEntry>(); Assert.False(failedUndo!.Successful);
        Assert.Equal(1, (await h.DashboardAsync()).GetProperty("undoCount").GetInt32());
        // A new command service instance rebuilds its real Stack from SQL history.
        var restarted = ActivatorUtilities.CreateInstance<CommandService>(h.App.Services);
        Assert.Equal(1, await restarted.UndoCountAsync(default));
        var undoId = Guid.NewGuid(); var undone = await restarted.UndoAsync(undoId, false, default); Assert.True(undone!.Successful);
        Assert.Equal(0, await restarted.UndoCountAsync(default));
        Assert.True((await restarted.UndoAsync(undoId, false, default))!.Successful);
        Assert.False(h.App.Services.GetRequiredService<LiveTelemetryStore>().FindById(h.PumpId)!.LatestReading!.BooleanValue);
        await using var scope = h.App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SmartXDbContext>();
        Assert.True((await db.CommandHistory.SingleAsync(c => c.Id == id)).Undone);
        Assert.Equal(4, await db.CommandHistory.CountAsync());
    }
    [Fact]
    public async Task PersistenceFailureDoesNotPublishGhostReadingsOrPushUndo()
    {
        await using var h = new OperationsHarness(); await h.StartAsync();
        var live = h.App.Services.GetRequiredService<LiveTelemetryStore>(); var before = live.FindById(h.PumpId)!.LatestReading!.Id;
        h.Failure.FailTelemetry = true;
        var result = await (await h.CommandAsync(true)).Content.ReadFromJsonAsync<CommandHistoryEntry>(); Assert.False(result!.Successful);
        Assert.Equal(before, live.FindById(h.PumpId)!.LatestReading!.Id); Assert.Equal(0, (await h.DashboardAsync()).GetProperty("undoCount").GetInt32());
        h.Failure.FailTelemetry = true;
        var failedId = Guid.NewGuid(); Assert.Equal(HttpStatusCode.Conflict, (await h.FloatAsync(5, failedId)).StatusCode);
        Assert.DoesNotContain(live.GetRecentHistory(limit: 500).Readings, r => r.Id == failedId);
        Assert.Equal(HttpStatusCode.Created, (await h.FloatAsync(5, failedId)).StatusCode);
        Assert.Single(live.GetRecentHistory(limit: 500).Readings, r => r.Id == failedId);
    }
    [Fact]
    public async Task BackgroundDetectsMissingPacketsAndRecoveryRetainsIndependentIncidents()
    {
        await using var h = new OperationsHarness(); await h.StartAsync();
        await h.FloatAsync(5); var tracker = h.App.Services.GetRequiredService<IncidentTracker>();
        var first = Assert.Single(tracker.Active()); await h.FloatAsync(5); Assert.Equal(first.Id, Assert.Single(tracker.Active()).Id);
        h.Clock.Now += TimeSpan.FromSeconds(21);
        // Wait for the production background monitor, without polling a dashboard/sending packets.
        for (var i = 0; i < 30 && !tracker.Active().Any(a => a.Type == "Disconnected"); i++) await Task.Delay(100);
        Assert.Equal(2, tracker.Active().Count(a => a.Type == "Disconnected"));
        await h.Client.PostAsJsonAsync($"/api/operations/heartbeat/{h.MoistureId}", new { });
        Assert.Equal("Connected", tracker.GetConnection(h.MoistureId).State);
        Assert.Contains(tracker.Active(), i => i.DeviceId == h.MoistureId && i.Type == "OutOfRange");
        await h.FloatAsync(50); Assert.DoesNotContain(tracker.Active(), i => i.DeviceId == h.MoistureId);
        h.Clock.Now += TimeSpan.FromSeconds(1); await h.FloatAsync(5);
        Assert.NotEqual(first.Id, tracker.Active().Single(i => i.Type == "OutOfRange").Id);
    }
    [Fact]
    public async Task RealHttpBacklogShowsCriticalBypassAndBackpressure()
    {
        await using var h = new OperationsHarness(); await h.StartAsync(capacity: 12, delay: 70);
        var completion = new List<(int Index, Task<HttpResponseMessage> Task)>();
        for (var i = 0; i < 10; i++) completion.Add((i, h.FloatAsync(50)));
        var queue = h.App.Services.GetRequiredService<TelemetryWorkQueue<TelemetryWork>>();
        for (var i = 0; i < 50 && queue.Status().NormalCount < 5; i++) await Task.Delay(5);
        Assert.True(queue.Status().NormalCount >= 5);
        var critical = h.FloatAsync(5); await critical;
        Assert.Contains(completion, item => !item.Task.IsCompleted);
        Assert.Equal(HttpStatusCode.Created, (await critical).StatusCode);
        await Task.WhenAll(completion.Select(c => c.Task));
        var bulk = await h.Client.PostAsJsonAsync("/api/telemetry/bulk", new { readings = Enumerable.Range(0, 13).Select(_ => new {
            id = Guid.NewGuid(), sensorId = h.MoistureId, valueKind = "Float", floatValue = 50, recordedAtUtc = h.Clock.Now.AddSeconds(-1) }).ToArray() });
        Assert.Equal((HttpStatusCode)429, bulk.StatusCode); Assert.Equal(1, queue.Status().Rejected);
    }
    [Fact]
    public async Task InvalidPayloadDoesNotCoerceValueAndCreatesOneRecoverableIncident()
    {
        await using var h = new OperationsHarness(); await h.StartAsync();
        for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.BadRequest, (await h.Client.PostAsJsonAsync("/api/telemetry/boolean", new { id = Guid.NewGuid(), sensorId = h.MoistureId, value = false, recordedAtUtc = h.Clock.Now })).StatusCode);
        var tracker = h.App.Services.GetRequiredService<IncidentTracker>(); Assert.Single(tracker.Active(), i => i.Type == "InvalidPayload");
        Assert.Equal(50, h.App.Services.GetRequiredService<LiveTelemetryStore>().FindById(h.MoistureId)!.LatestReading!.FloatValue);
        await h.FloatAsync(50); Assert.DoesNotContain(tracker.Active(), i => i.Type == "InvalidPayload");
    }
    [Fact]
    public async Task PersistedManualHistoryLearnsAndNewEvidenceChangesConfidence()
    {
        await using var h = new OperationsHarness(); await h.StartAsync(); await h.FloatAsync(5);
        Assert.Empty((await h.DashboardAsync()).GetProperty("suggestions").EnumerateArray());
        for (var i = 0; i < 3; i++)
        {
            h.Clock.Now += TimeSpan.FromSeconds(1);
            Assert.True((await (await h.CommandAsync(true)).Content.ReadFromJsonAsync<CommandHistoryEntry>())!.Successful);
            h.Clock.Now += TimeSpan.FromSeconds(1); Assert.True((await (await h.UndoAsync()).Content.ReadFromJsonAsync<CommandHistoryEntry>())!.Successful);
        }
        var learned = Assert.Single((await h.DashboardAsync()).GetProperty("suggestions").EnumerateArray());
        Assert.Equal(3, learned.GetProperty("support").GetInt32()); Assert.Equal(1, learned.GetProperty("confidence").GetDouble());
        h.Clock.Now += TimeSpan.FromSeconds(1); await h.CommandAsync(true);
        h.Clock.Now += TimeSpan.FromSeconds(1); await h.CommandAsync(false);
        var changed = Assert.Single((await h.DashboardAsync()).GetProperty("suggestions").EnumerateArray()); Assert.Equal(.8, changed.GetProperty("confidence").GetDouble());
        var request = new { id = Guid.NewGuid(), targetId = h.PumpId, kind = "search", query = "Pump A" };
        await h.Client.PostAsJsonAsync("/api/operations/interactions", request); await h.Client.PostAsJsonAsync("/api/operations/interactions", request);
        await using var scope = h.App.Services.CreateAsyncScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<SmartXDbContext>().Interactions.CountAsync());
    }
}
