using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Api.Controllers;
using SmartX.Application.History;
using SmartX.Application.Live;
using SmartX.Infrastructure.Live;
using SmartX.Tests.Live;

namespace SmartX.Tests.History;

public sealed class HistoricalPersistenceTests
{
    [Fact]
    public async Task FullHistorySurvivesRecentCacheEvictionAndFreshIndexReload()
    {
        await using var db = LiveTestData.Context();
        var a = LiveTestData.Sensor();
        var b = LiveTestData.Sensor(2);
        db.Sensors.AddRange(a, b);
        var records = Enumerable.Range(0, 2500).Select(i => LiveTestData.Record(a, i, i)).ToArray();
        db.TelemetryRecords.AddRange(records);
        db.TelemetryRecords.Add(LiveTestData.Record(b, 0));
        await db.SaveChangesAsync();
        var live = new LiveTelemetryService(db, new LiveTelemetryStore(3), history: new HistoricalTelemetryIndex());
        await live.EnsureInitializedAsync(default);
        Assert.Equal(3, live.Store.GetRecentHistory(limit: 500).RetainedCount);
        var controller = new TelemetryController(db, live);
        var action = await controller.GetHistory(a.Id, LiveTestData.Time, LiveTestData.Time.AddMinutes(2));
        var response = Assert.IsType<TelemetryHistoryResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Equal(3, response.TotalCount);
        Assert.Equal("AVL", response.Tree!.Structure);
        Assert.Equal(2500, response.Tree.IndexedReadings);
        Assert.All(response.Readings, r => Assert.Equal(a.Id, r.SensorId));
        var newPacket = new TelemetryIngestionRequest<int>(Guid.NewGuid(), a.Id, 500,
            LiveTestData.Time.AddMinutes(2501), LiveTestData.Time.AddMinutes(2501).AddSeconds(1));
        Assert.IsType<CreatedAtActionResult>((await controller.IngestInteger(newPacket, default)).Result);
        Assert.Equal(2501, (await live.GetHistoricalPageAsync(a.Id, null, null, null, 1, 25, default)).TotalCount);
        var rejected = await controller.IngestInteger(newPacket, default);
        Assert.IsType<ConflictObjectResult>(rejected.Result);
        Assert.Equal(2501, live.History.Query(a.Id).TotalCount);
        // New services and index simulate losing all process memory. Durable data
        // is shared; live SQL Server restart is also checked by the demo script.
        var restarted = new LiveTelemetryService(db, new LiveTelemetryStore(1), history: new HistoricalTelemetryIndex());
        var restored = await restarted.GetHistoricalPageAsync(a.Id, null, null, null, 1, 25, default);
        Assert.Equal(2501, restored.TotalCount);
        Assert.Equal(newPacket.Id, restored.Readings[0].Id);
        Assert.Equal(2501, await db.TelemetryRecords.CountAsync(r => r.SensorId == a.Id));
    }

    [Fact]
    public async Task ApiRangeValidationAndInclusiveOffsetBoundariesArePreserved()
    {
        await using var db = LiveTestData.Context();
        var sensor = LiveTestData.Sensor();
        db.Sensors.Add(sensor);
        db.TelemetryRecords.AddRange(LiveTestData.Record(sensor, 0), LiveTestData.Record(sensor, 0), LiveTestData.Record(sensor, 1));
        await db.SaveChangesAsync();
        var controller = new TelemetryController(db);
        var endpoint = LiveTestData.Time.ToOffset(TimeSpan.FromHours(2));
        var result = await controller.GetHistory(sensor.Id, endpoint, endpoint);
        Assert.Equal(2, Assert.IsType<TelemetryHistoryResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).TotalCount);
        Assert.IsType<BadRequestObjectResult>((await controller.GetHistory(sensor.Id, endpoint.AddMinutes(1), endpoint)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetHistory(sensor.Id, page: 0)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetHistory(sensor.Id, pageSize: 501)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.GetHistory(Guid.NewGuid())).Result);
    }
}
