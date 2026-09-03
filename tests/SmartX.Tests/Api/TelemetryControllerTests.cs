using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Telemetry;
using SmartX.Api.Controllers;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Tests.Api;

public sealed class TelemetryControllerTests
{
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IngestFloat_PersistsOnlyTheFloatColumn()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Float, 18, 28);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);
        var request = CreateRequest(sensor.Id, 22.5f);

        var action = await controller.IngestFloat(
            request,
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<TelemetryReadingResponse>(created.Value);
        Assert.Equal(22.5f, response.FloatValue);
        Assert.Null(response.IntegerValue);
        Assert.Null(response.BooleanValue);
        Assert.True(response.IsValid);
        Assert.Single(context.TelemetryRecords);
    }

    [Fact]
    public async Task IngestInteger_PersistsOnlyTheIntegerColumn()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Integer, 100, 1000);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.IngestInteger(
            CreateRequest(sensor.Id, 450),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<TelemetryReadingResponse>(created.Value);
        Assert.Equal(450, response.IntegerValue);
        Assert.Null(response.FloatValue);
        Assert.Null(response.BooleanValue);
    }

    [Fact]
    public async Task IngestBoolean_PersistsOnlyTheBooleanColumn()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Boolean);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.IngestBoolean(
            CreateRequest(sensor.Id, true),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<TelemetryReadingResponse>(created.Value);
        Assert.True(response.BooleanValue is true);
        Assert.Null(response.FloatValue);
        Assert.Null(response.IntegerValue);
    }

    [Fact]
    public async Task IngestFloat_StoresOutOfRangeReadingAsInvalid()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Float, 18, 28);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.IngestFloat(
            CreateRequest(sensor.Id, 35.5f),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<TelemetryReadingResponse>(created.Value);
        Assert.False(response.IsValid);
        Assert.Contains(
            "outside the expected range",
            response.ValidationMessage ?? string.Empty);
    }

    [Fact]
    public async Task IngestFloat_RejectsWrongTypeForSensor()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Integer, 100, 1000);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.IngestFloat(
            CreateRequest(sensor.Id, 22.5f),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(context.TelemetryRecords);
    }

    [Fact]
    public async Task IngestFloat_ReturnsNotFoundForMissingSensor()
    {
        await using var context = CreateContext();
        var controller = new TelemetryController(context);

        var action = await controller.IngestFloat(
            CreateRequest(Guid.NewGuid(), 22.5f),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    [Fact]
    public async Task IngestFloat_RejectsDuplicateTelemetryIdentifier()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Float, 18, 28);
        var request = CreateRequest(sensor.Id, 22.5f);
        var existingPacket = new TelemetryPacket<float>(
            request.Id,
            sensor.Id,
            21.5f,
            RecordedAtUtc.AddMinutes(-5));
        context.Sensors.Add(sensor);
        context.TelemetryRecords.Add(TelemetryRecord.FromPacket(existingPacket));
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.IngestFloat(
            request,
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Single(context.TelemetryRecords);
    }

    [Fact]
    public async Task GetById_ReturnsStoredTelemetry()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Boolean);
        var packet = new TelemetryPacket<bool>(
            Guid.NewGuid(),
            sensor.Id,
            true,
            RecordedAtUtc);
        var record = TelemetryRecord.FromPacket(packet);
        context.Sensors.Add(sensor);
        context.TelemetryRecords.Add(record);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.GetById(
            record.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<TelemetryReadingResponse>(ok.Value);
        Assert.Equal(record.Id, response.Id);
        Assert.True(response.BooleanValue is true);
    }

    [Fact]
    public async Task GetHistory_ReturnsNewestReadingsWithPagination()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Integer, 100, 1000);
        context.Sensors.Add(sensor);

        for (var index = 0; index < 5; index++)
        {
            var packet = new TelemetryPacket<int>(
                Guid.NewGuid(),
                sensor.Id,
                200 + index,
                RecordedAtUtc.AddMinutes(index));
            context.TelemetryRecords.Add(TelemetryRecord.FromPacket(packet));
        }

        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.GetHistory(
            sensor.Id,
            page: 1,
            pageSize: 2,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var history = Assert.IsType<TelemetryHistoryResponse>(ok.Value);
        Assert.Equal(5, history.TotalCount);
        Assert.Equal(2, history.Readings.Count);
        Assert.True(
            history.Readings[0].RecordedAtUtc >
            history.Readings[1].RecordedAtUtc);
    }

    [Fact]
    public async Task GetHistory_FiltersInvalidReadings()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Float, 18, 28);
        var validPacket = new TelemetryPacket<float>(
            Guid.NewGuid(),
            sensor.Id,
            22.5f,
            RecordedAtUtc);
        var invalidPacket = new TelemetryPacket<float>(
            Guid.NewGuid(),
            sensor.Id,
            35.5f,
            RecordedAtUtc.AddMinutes(1));
        context.Sensors.Add(sensor);
        context.TelemetryRecords.Add(TelemetryRecord.FromPacket(validPacket));
        context.TelemetryRecords.Add(TelemetryRecord.FromPacket(
            invalidPacket,
            false,
            "Outside expected range."));
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.GetHistory(
            sensor.Id,
            isValid: false,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var history = Assert.IsType<TelemetryHistoryResponse>(ok.Value);
        var reading = Assert.Single(history.Readings);
        Assert.False(reading.IsValid);
    }

    private static SmartXDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SmartXDbContext(options);
    }

    private static Sensor CreateSensor(
        TelemetryValueKind valueKind,
        double? expectedMinimum = null,
        double? expectedMaximum = null)
    {
        return new Sensor(
            Guid.NewGuid(),
            "A4:CF:12:8B:60:01",
            $"Test {valueKind} Sensor",
            valueKind == TelemetryValueKind.Boolean
                ? SensorCategory.Actuator
                : SensorCategory.Environmental,
            "Test measurement",
            valueKind,
            valueKind == TelemetryValueKind.Boolean ? "state" : "unit",
            Guid.NewGuid(),
            expectedMinimum,
            expectedMaximum);
    }

    private static TelemetryIngestionRequest<T> CreateRequest<T>(
        Guid sensorId,
        T value)
        where T : struct
    {
        return new TelemetryIngestionRequest<T>(
            Guid.NewGuid(),
            sensorId,
            value,
            RecordedAtUtc,
            RecordedAtUtc.AddSeconds(2));
    }
}
