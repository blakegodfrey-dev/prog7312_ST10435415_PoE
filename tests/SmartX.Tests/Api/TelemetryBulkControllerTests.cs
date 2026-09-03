using Microsoft.AspNetCore.Http;
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

public sealed class TelemetryBulkControllerTests
{
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IngestBulk_StoresMixedTypedAndInvalidReadings()
    {
        await using var context = CreateContext();
        var floatSensor = CreateSensor(
            TelemetryValueKind.Float,
            1,
            18,
            28);
        var integerSensor = CreateSensor(
            TelemetryValueKind.Integer,
            2,
            100,
            1000);
        var booleanSensor = CreateSensor(
            TelemetryValueKind.Boolean,
            3);
        context.Sensors.AddRange(
            floatSensor,
            integerSensor,
            booleanSensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);
        var request = new BulkTelemetryIngestionRequest(
        [
            CreateFloatReading(floatSensor.Id, 35.5f),
            CreateIntegerReading(integerSensor.Id, 450),
            CreateBooleanReading(booleanSensor.Id, true)
        ]);

        var action = await controller.IngestBulk(
            request,
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var response = Assert.IsType<BulkTelemetryIngestionResponse>(
            result.Value);
        Assert.Equal(3, response.SubmittedCount);
        Assert.Equal(3, response.StoredCount);
        Assert.Equal(2, response.ValidCount);
        Assert.Equal(1, response.InvalidCount);
        Assert.Equal(3, await context.TelemetryRecords.CountAsync());
        Assert.Contains(response.Readings, reading =>
            reading.FloatValue == 35.5f && !reading.IsValid);
        Assert.Contains(response.Readings, reading =>
            reading.IntegerValue == 450 && reading.IsValid);
        Assert.Contains(response.Readings, reading =>
            reading.BooleanValue is true && reading.IsValid);
    }

    [Fact]
    public async Task IngestBulk_AcceptsMaximumBatchSize()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(
            TelemetryValueKind.Integer,
            4,
            0,
            1000);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);
        var readings = Enumerable
            .Range(0, TelemetryController.MaximumBatchSize)
            .Select(index => CreateIntegerReading(
                sensor.Id,
                index,
                RecordedAtUtc.AddSeconds(index)))
            .Cast<BulkTelemetryItemRequest?>()
            .ToList();

        var action = await controller.IngestBulk(
            new BulkTelemetryIngestionRequest(readings),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(
            TelemetryController.MaximumBatchSize,
            await context.TelemetryRecords.CountAsync());
    }

    [Fact]
    public async Task IngestBulk_RejectsBatchAboveMaximumSize()
    {
        await using var context = CreateContext();
        var controller = new TelemetryController(context);
        var readings = Enumerable
            .Range(0, TelemetryController.MaximumBatchSize + 1)
            .Select(_ => (BulkTelemetryItemRequest?)CreateBooleanReading(
                Guid.NewGuid(),
                true))
            .ToList();

        var action = await controller.IngestBulk(
            new BulkTelemetryIngestionRequest(readings),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(context.TelemetryRecords);
    }

    [Fact]
    public async Task IngestBulk_RejectsDuplicateIdentifierWithinBatch()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Boolean, 5);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);
        var duplicateId = Guid.NewGuid();

        var action = await controller.IngestBulk(
            new BulkTelemetryIngestionRequest(
            [
                CreateBooleanReading(sensor.Id, true, id: duplicateId),
                CreateBooleanReading(sensor.Id, false, id: duplicateId)
            ]),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Empty(context.TelemetryRecords);
    }

    [Fact]
    public async Task IngestBulk_RejectsIdentifierAlreadyInDatabase()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(TelemetryValueKind.Boolean, 6);
        var packet = new TelemetryPacket<bool>(
            Guid.NewGuid(),
            sensor.Id,
            true,
            RecordedAtUtc);
        context.Sensors.Add(sensor);
        context.TelemetryRecords.Add(TelemetryRecord.FromPacket(packet));
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.IngestBulk(
            new BulkTelemetryIngestionRequest(
            [
                CreateBooleanReading(
                    sensor.Id,
                    false,
                    id: packet.Id)
            ]),
            CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Single(context.TelemetryRecords);
    }

    [Fact]
    public async Task IngestBulk_ReturnsNotFoundForMissingSensor()
    {
        await using var context = CreateContext();
        var controller = new TelemetryController(context);

        var action = await controller.IngestBulk(
            new BulkTelemetryIngestionRequest(
            [
                CreateFloatReading(Guid.NewGuid(), 22.5f)
            ]),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(action.Result);
        Assert.Empty(context.TelemetryRecords);
    }

    [Fact]
    public async Task IngestBulk_WrongSensorTypeRejectsEntireBatch()
    {
        await using var context = CreateContext();
        var floatSensor = CreateSensor(
            TelemetryValueKind.Float,
            7,
            18,
            28);
        var integerSensor = CreateSensor(
            TelemetryValueKind.Integer,
            8,
            100,
            1000);
        context.Sensors.AddRange(floatSensor, integerSensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);

        var action = await controller.IngestBulk(
            new BulkTelemetryIngestionRequest(
            [
                CreateFloatReading(floatSensor.Id, 22.5f),
                CreateFloatReading(integerSensor.Id, 450f)
            ]),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(context.TelemetryRecords);
    }

    [Fact]
    public async Task IngestBulk_MultipleTypedValuesRejectEntireBatch()
    {
        await using var context = CreateContext();
        var sensor = CreateSensor(
            TelemetryValueKind.Float,
            9,
            18,
            28);
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var controller = new TelemetryController(context);
        var malformedReading = CreateFloatReading(sensor.Id, 22.5f) with
        {
            IntegerValue = 22
        };

        var action = await controller.IngestBulk(
            new BulkTelemetryIngestionRequest([malformedReading]),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(context.TelemetryRecords);
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
        int macSuffix,
        double? expectedMinimum = null,
        double? expectedMaximum = null)
    {
        return new Sensor(
            Guid.NewGuid(),
            $"A4:CF:12:8B:70:{macSuffix:D2}",
            $"Bulk {valueKind} Sensor",
            valueKind == TelemetryValueKind.Boolean
                ? SensorCategory.Actuator
                : SensorCategory.Environmental,
            "Bulk measurement",
            valueKind,
            valueKind == TelemetryValueKind.Boolean ? "state" : "unit",
            Guid.NewGuid(),
            expectedMinimum,
            expectedMaximum);
    }

    private static BulkTelemetryItemRequest CreateFloatReading(
        Guid sensorId,
        float value,
        DateTimeOffset? recordedAtUtc = null,
        Guid? id = null)
    {
        return new BulkTelemetryItemRequest(
            id ?? Guid.NewGuid(),
            sensorId,
            TelemetryValueKind.Float,
            value,
            null,
            null,
            recordedAtUtc ?? RecordedAtUtc,
            (recordedAtUtc ?? RecordedAtUtc).AddSeconds(2));
    }

    private static BulkTelemetryItemRequest CreateIntegerReading(
        Guid sensorId,
        int value,
        DateTimeOffset? recordedAtUtc = null,
        Guid? id = null)
    {
        return new BulkTelemetryItemRequest(
            id ?? Guid.NewGuid(),
            sensorId,
            TelemetryValueKind.Integer,
            null,
            value,
            null,
            recordedAtUtc ?? RecordedAtUtc,
            (recordedAtUtc ?? RecordedAtUtc).AddSeconds(2));
    }

    private static BulkTelemetryItemRequest CreateBooleanReading(
        Guid sensorId,
        bool value,
        DateTimeOffset? recordedAtUtc = null,
        Guid? id = null)
    {
        return new BulkTelemetryItemRequest(
            id ?? Guid.NewGuid(),
            sensorId,
            TelemetryValueKind.Boolean,
            null,
            null,
            value,
            recordedAtUtc ?? RecordedAtUtc,
            (recordedAtUtc ?? RecordedAtUtc).AddSeconds(2));
    }
}
