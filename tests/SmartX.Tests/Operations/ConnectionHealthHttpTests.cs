using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Api.Controllers;
using SmartX.Application.Live;
using SmartX.Application.Operations;
using SmartX.Infrastructure.Live;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Tests.Operations;

public sealed class ConnectionHealthHttpTests
{
    [Theory]
    [InlineData(0, "Connected", 2, 0, 0)]
    [InlineData(10, "Stale", 0, 2, 0)]
    [InlineData(20, "Disconnected", 0, 0, 2)]
    public async Task BothModulesAndFleetSummaryUseConfiguredGatewayThresholds(
        int ageSeconds, string expected, int connected, int stale, int disconnected)
    {
        await using var h = new OperationsHarness();
        await h.StartAsync();
        var contact = h.Clock.Now;
        h.Clock.Now += TimeSpan.FromSeconds(ageSeconds);
        var dashboard = await h.DashboardAsync();
        foreach (var row in dashboard.GetProperty("devices").EnumerateArray())
        {
            var id = row.GetProperty("device").GetProperty("id").GetGuid();
            var status = await h.Client.GetFromJsonAsync<JsonElement>($"/api/sensors/{id}/connection-status");
            Assert.Equal(expected, row.GetProperty("connection").GetProperty("state").GetString());
            Assert.Equal(expected, status.GetProperty("status").GetString());
            Assert.Equal(contact, status.GetProperty("lastSeenAtUtc").GetDateTimeOffset());
            Assert.Equal(ageSeconds, status.GetProperty("secondsSinceGatewayContact").GetDouble());
            Assert.Equal(10, status.GetProperty("staleSeconds").GetDouble());
            Assert.Equal(20, status.GetProperty("disconnectedSeconds").GetDouble());
        }
        var summary = await h.Client.GetFromJsonAsync<JsonElement>("/api/telemetry/diagnostics/health-summary");
        Assert.Equal(connected, summary.GetProperty("connectedSensorCount").GetInt32());
        Assert.Equal(stale, summary.GetProperty("staleSensorCount").GetInt32());
        Assert.Equal(disconnected, summary.GetProperty("disconnectedSensorCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("unknownSensorCount").GetInt32());
        Assert.Equal(dashboard.GetProperty("staleSeconds").GetDouble(), summary.GetProperty("staleSeconds").GetDouble());
        Assert.Equal(dashboard.GetProperty("disconnectedSeconds").GetDouble(), summary.GetProperty("disconnectedSeconds").GetDouble());
    }

    [Fact]
    public async Task HeartbeatRestoresBothModulesWithoutChangingTheHistoricalReading()
    {
        await using var h = new OperationsHarness(); await h.StartAsync();
        var before = await h.Client.GetFromJsonAsync<JsonElement>($"/api/sensors/{h.PumpId}/connection-status");
        h.Clock.Now += TimeSpan.FromSeconds(21);
        Assert.Equal("Disconnected", (await h.DashboardAsync()).GetProperty("devices")[0].GetProperty("connection").GetProperty("state").GetString());
        var heartbeat = await h.Client.PostAsJsonAsync($"/api/operations/heartbeat/{h.PumpId}", new { });
        heartbeat.EnsureSuccessStatusCode();
        var after = await h.Client.GetFromJsonAsync<JsonElement>($"/api/sensors/{h.PumpId}/connection-status");
        Assert.Equal("Connected", after.GetProperty("status").GetString());
        Assert.Equal(before.GetProperty("lastRecordedAtUtc").GetDateTimeOffset(), after.GetProperty("lastRecordedAtUtc").GetDateTimeOffset());
        Assert.Equal(h.Clock.Now, after.GetProperty("lastSeenAtUtc").GetDateTimeOffset());
        var dashboard = await h.DashboardAsync();
        Assert.Equal("Connected", dashboard.GetProperty("devices").EnumerateArray().Single(d => d.GetProperty("device").GetProperty("id").GetGuid() == h.PumpId).GetProperty("connection").GetProperty("state").GetString());
    }

    [Fact]
    public async Task FreshDeviceHasUnknownContactAndHeartbeatDoesNotInventTelemetry()
    {
        await using var h = new OperationsHarness(); await h.StartAsync();
        var id = Guid.NewGuid();
        var registration = await h.Client.PostAsJsonAsync("/api/sensors", new {
            id, macAddress = "02:00:00:00:00:09", friendlyName = "Heartbeat only", category = "Actuator",
            measuredProperty = "Pump state", valueKind = "Boolean", unit = "", deploymentNodeId = h.NodeId });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var before = await h.Client.GetFromJsonAsync<JsonElement>($"/api/sensors/{id}/connection-status");
        Assert.Equal("Unknown", before.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, before.GetProperty("lastSeenAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("lastRecordedAtUtc").ValueKind);
        var summary = await h.Client.GetFromJsonAsync<JsonElement>("/api/telemetry/diagnostics/health-summary");
        Assert.Equal(1, summary.GetProperty("unknownSensorCount").GetInt32());
        await h.Client.PostAsJsonAsync($"/api/operations/heartbeat/{id}", new { });
        var after = await h.Client.GetFromJsonAsync<JsonElement>($"/api/sensors/{id}/connection-status");
        Assert.Equal("Connected", after.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, after.GetProperty("lastRecordedAtUtc").ValueKind);
        summary = await h.Client.GetFromJsonAsync<JsonElement>("/api/telemetry/diagnostics/health-summary");
        Assert.Equal(3, summary.GetProperty("connectedSensorCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("noDataSensorCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("unknownSensorCount").GetInt32());
    }

    [Fact]
    public async Task RestartRestoresDurableGatewayContactBeforeAnyHealthResponse()
    {
        await using var h = new OperationsHarness(); await h.StartAsync();
        var receipt = h.Clock.Now;
        h.Clock.Now += TimeSpan.FromSeconds(21);
        await using var scope = h.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartXDbContext>();
        var tracker = new IncidentTracker(h.Clock, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        var live = new LiveTelemetryService(db, new LiveTelemetryStore(), tracker);
        var controller = new SensorConnectionStatusController(db, live, tracker, h.Clock);
        var result = Assert.IsType<OkObjectResult>((await controller.Get(h.PumpId, default)).Result);
        var status = Assert.IsType<SensorConnectionStatusResponse>(result.Value);
        Assert.Equal("Disconnected", status.Status.ToString());
        Assert.Equal(receipt, status.LastSeenAtUtc);
        Assert.Equal(21, status.SecondsSinceGatewayContact);
    }
}
