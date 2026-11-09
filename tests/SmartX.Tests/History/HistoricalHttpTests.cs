using System.Net;
using System.Net.Http.Json;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Tests.Operations;

namespace SmartX.Tests.History;

public sealed class HistoricalHttpTests
{
    [Fact]
    public async Task QueuedIngestionCommandsUndoAndFailedCommitsKeepHistoryConsistent()
    {
        await using var harness = new OperationsHarness();
        await harness.StartAsync();
        var url = $"/api/telemetry/sensors/{harness.PumpId}?pageSize=500";
        var initial = await harness.Client.GetFromJsonAsync<TelemetryHistoryResponse>(url);
        Assert.Equal("AVL", initial!.Tree!.Structure);
        Assert.Equal(1, initial.TotalCount);
        Assert.Equal(HttpStatusCode.OK, (await harness.CommandAsync(true)).StatusCode);
        harness.Clock.Now += TimeSpan.FromSeconds(1);
        Assert.Equal(HttpStatusCode.OK, (await harness.UndoAsync()).StatusCode);
        var commands = await harness.Client.GetFromJsonAsync<TelemetryHistoryResponse>(url);
        Assert.Equal(3, commands!.TotalCount);
        Assert.False(commands.Readings[0].BooleanValue);
        var moistureUrl = $"/api/telemetry/sensors/{harness.MoistureId}?pageSize=500";
        var before = await harness.Client.GetFromJsonAsync<TelemetryHistoryResponse>(moistureUrl);
        harness.Failure.FailTelemetry = true;
        Assert.Equal(HttpStatusCode.Conflict, (await harness.FloatAsync(55)).StatusCode);
        var after = await harness.Client.GetFromJsonAsync<TelemetryHistoryResponse>(moistureUrl);
        Assert.Equal(before!.TotalCount, after!.TotalCount);
        Assert.Equal(HttpStatusCode.Created, (await harness.FloatAsync(56)).StatusCode);
        Assert.Equal(before.TotalCount + 1, (await harness.Client.GetFromJsonAsync<TelemetryHistoryResponse>(moistureUrl))!.TotalCount);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await harness.Client.GetAsync(url + "&fromUtc=not-a-time")).StatusCode);
    }
}
