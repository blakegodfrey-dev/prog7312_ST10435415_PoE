using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Contracts.Live;
using SmartX.Api.Controllers;
using SmartX.Application.Live;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Live;

namespace SmartX.Tests.Live;

public sealed class LiveDevicesControllerTests
{
    [Fact]
    public async Task LiveApiHydratesExistingDevicesAndAcceptsCanonicalEquivalentMacInput()
    {
        await using var context = LiveTestData.Context();
        var sensor = LiveTestData.Sensor(1, TelemetryValueKind.Boolean);
        context.Sensors.Add(sensor);
        context.TelemetryRecords.Add(LiveTestData.Record(sensor, 1, 0));
        await context.SaveChangesAsync();
        var live = new LiveTelemetryService(context, new LiveTelemetryStore());
        var controller = new LiveDevicesController(live);
        var result = await controller.GetDevice(" a4:cf:12:8b:40:01 ", CancellationToken.None);
        var response = Assert.IsType<LiveDeviceResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(sensor.MacAddress, response.MacAddress);
        Assert.False(response.LatestReading!.BooleanValue);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("Boolean", json.RootElement.GetProperty("valueKind").GetString());
        Assert.Equal("Actuator", json.RootElement.GetProperty("category").GetString());
        Assert.False(json.RootElement.GetProperty("latestReading").GetProperty("booleanValue").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("latestReading").GetProperty("floatValue").ValueKind);
    }

    [Fact]
    public async Task RegistryListIncludesRegisteredDevicesWithNoTelemetry()
    {
        await using var context = LiveTestData.Context();
        context.Sensors.AddRange(LiveTestData.Sensor(), LiveTestData.Sensor(2));
        await context.SaveChangesAsync();
        var controller = new LiveDevicesController(new LiveTelemetryService(context, new LiveTelemetryStore()));
        var result = await controller.GetDevices(CancellationToken.None);
        var devices = Assert.IsAssignableFrom<IReadOnlyList<LiveDeviceResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(2, devices.Count);
        Assert.All(devices, d => Assert.Null(d.LatestReading));
    }

    [Theory]
    [InlineData("wrong", 400)]
    [InlineData("A4:CF:12:8B:40:99", 404)]
    public async Task DeviceLookupDistinguishesMalformedAndUnknownMac(string macAddress, int status)
    {
        await using var context = LiveTestData.Context();
        var controller = new LiveDevicesController(new LiveTelemetryService(context, new LiveTelemetryStore()));
        var result = await controller.GetDevice(macAddress, CancellationToken.None);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task HistoryApiLabelsTheWindowAndReturnsFilteredChronologicalNativeReadings()
    {
        await using var context = LiveTestData.Context();
        var first = LiveTestData.Sensor();
        var second = LiveTestData.Sensor(2);
        context.Sensors.AddRange(first, second);
        context.TelemetryRecords.AddRange(LiveTestData.Record(first, 3, 30), LiveTestData.Record(first, 1, 10),
            LiveTestData.Record(first, 2, 20), LiveTestData.Record(second, 2, 50));
        await context.SaveChangesAsync();
        var controller = new LiveDevicesController(new LiveTelemetryService(context, new LiveTelemetryStore(3)));
        var result = await controller.GetHistory(first.MacAddress.ToLowerInvariant(), limit: 1);
        var history = Assert.IsType<RecentLiveHistoryResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("RecentMemory", history.Source);
        Assert.Equal(3, history.Capacity);
        Assert.Equal(3, history.RetainedCount);
        Assert.Equal(2, history.MatchingCount);
        Assert.True(history.IsLimited);
        Assert.Equal(30, Assert.Single(history.Readings).IntegerValue);
        var all = await controller.GetHistory(limit: 500);
        var readings = Assert.IsType<RecentLiveHistoryResponse>(Assert.IsType<OkObjectResult>(all.Result).Value).Readings;
        Assert.Equal(readings.OrderBy(r => r.RecordedAtUtc).Select(r => r.Id), readings.Select(r => r.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task InvalidHistoryLimitsAreRejectedBeforeHydration(int limit)
    {
        await using var context = LiveTestData.Context();
        var store = new LiveTelemetryStore();
        var controller = new LiveDevicesController(new LiveTelemetryService(context, store));
        Assert.IsType<BadRequestObjectResult>((await controller.GetHistory(limit: limit)).Result);
        Assert.False(store.IsInitialized);
    }

    [Fact]
    public async Task InvalidRangeAndMacFiltersReturnValidationErrors()
    {
        await using var context = LiveTestData.Context();
        var controller = new LiveDevicesController(new LiveTelemetryService(context, new LiveTelemetryStore()));
        Assert.IsType<BadRequestObjectResult>((await controller.GetHistory(fromUtc: LiveTestData.Time.AddMinutes(1), toUtc: LiveTestData.Time)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetHistory(macAddress: "wrong")).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.GetHistory(macAddress: "A4:CF:12:8B:40:99")).Result);
    }
}
