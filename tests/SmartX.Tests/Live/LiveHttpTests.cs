using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SmartX.Api.Contracts.Live;
using SmartX.Api.Contracts.Sensors;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Api.Controllers;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Infrastructure;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Tests.Live;

public sealed class LiveHttpTests
{
    [Fact]
    public async Task HttpRegistrationAndIngestionFeedTheLiveMacRegistryAndOrderedHistory()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SmartXDatabase"] = "Server=localhost;Database=Test;Integrated Security=true;",
            ["LiveTelemetry:RecentHistoryCapacity"] = "3"
        });
        builder.Services.AddInfrastructure(builder.Configuration, Path.GetTempPath());
        // Keep production service lifetimes and controller activation. Only the
        // persistence provider is replaced for this deterministic HTTP test.
        builder.Services.RemoveAll<DbContextOptions<SmartXDbContext>>();
        builder.Services.RemoveAll<IDbContextOptionsConfiguration<SmartXDbContext>>();
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<SmartXDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddControllers().AddApplicationPart(typeof(TelemetryController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        var sensor = LiveTestData.Sensor();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SmartXDbContext>();
            context.DeploymentNodes.Add(new DeploymentNode(LiveTestData.NodeId, "Reservoir", "RES-1", DeploymentNodeType.Node));
            context.Sensors.Add(sensor);
            await context.SaveChangesAsync();
        }
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var initial = await client.GetFromJsonAsync<LiveDeviceResponse[]>("/api/live/devices");
            Assert.Null(Assert.Single(initial!).LatestReading);
            var actuator = LiveTestData.Sensor(2, TelemetryValueKind.Boolean);
            var registration = await client.PostAsJsonAsync("/api/sensors", new RegisterSensorRequest(
                actuator.Id, actuator.MacAddress.ToLowerInvariant(), actuator.FriendlyName, actuator.Category,
                actuator.MeasuredProperty, actuator.ValueKind, actuator.Unit, actuator.DeploymentNodeId, null, null));
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
            var request = new BulkTelemetryIngestionRequest([
                new(Guid.NewGuid(), sensor.Id, TelemetryValueKind.Integer, null, 30, null, LiveTestData.Time.AddMinutes(3), null),
                new(Guid.NewGuid(), sensor.Id, TelemetryValueKind.Integer, null, 10, null, LiveTestData.Time.AddMinutes(1), null),
                new(Guid.NewGuid(), sensor.Id, TelemetryValueKind.Integer, null, 20, null, LiveTestData.Time.AddMinutes(2), null),
                new(Guid.NewGuid(), actuator.Id, TelemetryValueKind.Boolean, null, null, false, LiveTestData.Time.AddMinutes(2), null)
            ]);
            var ingestion = await client.PostAsJsonAsync("/api/telemetry/bulk", request);
            Assert.Equal(HttpStatusCode.Created, ingestion.StatusCode);
            var history = await client.GetFromJsonAsync<RecentLiveHistoryResponse>("/api/live/history?limit=500");
            Assert.Equal(3, history!.RetainedCount);
            Assert.Equal(3, history.Readings.Count);
            Assert.Equal(new[] { 2, 2, 3 }, history.Readings.Select(r => (int)(r.RecordedAtUtc - LiveTestData.Time).TotalMinutes));
            var current = await client.GetFromJsonAsync<LiveDeviceResponse>("/api/live/devices/" + Uri.EscapeDataString(sensor.MacAddress.ToLowerInvariant()));
            Assert.Equal(30, current!.LatestReading!.IntegerValue);
            var boolean = await client.GetFromJsonAsync<LiveDeviceResponse>("/api/live/devices/" + Uri.EscapeDataString(actuator.MacAddress));
            Assert.False(boolean!.LatestReading!.BooleanValue);
            var full = await client.GetFromJsonAsync<TelemetryHistoryResponse>($"/api/telemetry/sensors/{sensor.Id}?pageSize=500");
            Assert.Equal(3, full!.TotalCount);
            Assert.Equal(2, history.Readings.Count(r => r.SensorId == sensor.Id));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/telemetry/bulk", request)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/live/history?limit=501")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/live/devices/wrong")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/live/devices/A4:CF:12:8B:40:99")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        }
        finally { await app.StopAsync(); }
    }
}
