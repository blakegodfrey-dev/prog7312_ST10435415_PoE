using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Tests.Operations;

public sealed class RequiredTelemetryValueHttpTests
{
    [Theory]
    [InlineData("float")]
    [InlineData("integer")]
    [InlineData("boolean")]
    public async Task MissingAndNullValuesAreRejectedWithoutSavingOrReplacingLiveReadings(string kind)
    {
        await using var h = new OperationsHarness(); await h.StartAsync();
        var before = await h.DashboardAsync();
        await using var scope = h.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartXDbContext>();
        var count = await db.TelemetryRecords.CountAsync();
        var rejected = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var absent = await h.Client.PostAsJsonAsync($"/api/telemetry/{kind}", new {
            id = rejected[0], sensorId = h.PumpId, recordedAtUtc = h.Clock.Now });
        var explicitNull = await h.Client.PostAsJsonAsync($"/api/telemetry/{kind}", new {
            id = rejected[1], sensorId = h.PumpId, value = (object?)null, recordedAtUtc = h.Clock.Now });
        Assert.Equal(HttpStatusCode.BadRequest, absent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, explicitNull.StatusCode);
        Assert.Equal(count, await db.TelemetryRecords.CountAsync());
        Assert.False(await db.TelemetryRecords.AnyAsync(r => rejected.Contains(r.Id)));
        var after = await h.DashboardAsync();
        Assert.Equal(before.GetProperty("devices").EnumerateArray().Select(d => d.GetProperty("device").GetProperty("latestReading").GetProperty("id").GetGuid()),
            after.GetProperty("devices").EnumerateArray().Select(d => d.GetProperty("device").GetProperty("latestReading").GetProperty("id").GetGuid()));
    }

    [Theory]
    [InlineData("float", "floatValue")]
    [InlineData("integer", "integerValue")]
    [InlineData("boolean", "booleanValue")]
    public async Task ExplicitZeroAndFalseRemainValidPayloadsAndPreserveTheirNativeType(string kind, string column)
    {
        await using var h = new OperationsHarness(); await h.StartAsync();
        var sensorId = kind == "boolean" ? h.PumpId : h.MoistureId;
        if (kind == "integer")
        {
            sensorId = Guid.NewGuid();
            var registration = await h.Client.PostAsJsonAsync("/api/sensors", new {
                id = sensorId, macAddress = "02:00:00:00:00:08", friendlyName = "Zero meter", category = "PowerConsumption",
                measuredProperty = "Electrical load", valueKind = "Integer", unit = "W", deploymentNodeId = h.NodeId,
                expectedMinimum = 0, expectedMaximum = 500 });
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        }
        object value = kind == "boolean" ? false : 0;
        var response = await h.Client.PostAsJsonAsync($"/api/telemetry/{kind}", new {
            id = Guid.NewGuid(), sensorId, value, recordedAtUtc = h.Clock.Now });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reading = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (kind == "boolean") Assert.False(reading.GetProperty(column).GetBoolean());
        else Assert.Equal(0, reading.GetProperty(column).GetDouble());
        var persisted = await h.Client.GetFromJsonAsync<JsonElement>($"/api/telemetry/{reading.GetProperty("id").GetGuid()}");
        Assert.Equal(reading.GetProperty(column).GetRawText(), persisted.GetProperty(column).GetRawText());
    }
}
