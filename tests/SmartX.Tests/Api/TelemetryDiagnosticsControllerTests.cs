using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Telemetry.Diagnostics;
using SmartX.Api.Controllers;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Tests.Api;

public sealed class TelemetryDiagnosticsControllerTests
{
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetSummary_ReturnsValidationAndTypeCounts()
    {
        await using var context = CreateContext();
        var node = CreateNode();
        var floatSensor = CreateSensor(node.Id, 1, TelemetryValueKind.Float);
        var integerSensor = CreateSensor(
            node.Id,
            2,
            TelemetryValueKind.Integer);
        var booleanSensor = CreateSensor(
            node.Id,
            3,
            TelemetryValueKind.Boolean);
        context.AddRange(node, floatSensor, integerSensor, booleanSensor);
        context.TelemetryRecords.AddRange(
            CreateFloatRecord(floatSensor.Id, 20, 0),
            CreateFloatRecord(floatSensor.Id, 35, 1, isValid: false),
            CreateIntegerRecord(integerSensor.Id, 900, 2, isValid: false),
            CreateBooleanRecord(booleanSensor.Id, true, 3));
        await context.SaveChangesAsync();
        var controller = new TelemetryDiagnosticsController(context);

        var action = await controller.GetSummary(
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var summary = Assert.IsType<TelemetryDiagnosticsSummaryResponse>(
            ok.Value);
        Assert.Equal(4, summary.TotalReadings);
        Assert.Equal(2, summary.ValidReadings);
        Assert.Equal(2, summary.InvalidReadings);
        Assert.Equal(50d, summary.InvalidPercentage);
        Assert.Equal(2, summary.AffectedSensorCount);
        Assert.Equal(2, summary.FloatReadings);
        Assert.Equal(1, summary.IntegerReadings);
        Assert.Equal(1, summary.BooleanReadings);
        Assert.Equal(RecordedAtUtc, summary.EarliestRecordedAtUtc);
        Assert.Equal(
            RecordedAtUtc.AddMinutes(3),
            summary.LatestRecordedAtUtc);
    }

    [Fact]
    public async Task GetSummary_AppliesTimeRange()
    {
        await using var context = CreateContext();
        var node = CreateNode();
        var sensor = CreateSensor(node.Id, 1, TelemetryValueKind.Float);
        context.AddRange(node, sensor);
        context.TelemetryRecords.AddRange(
            CreateFloatRecord(sensor.Id, 20, 0),
            CreateFloatRecord(sensor.Id, 21, 10));
        await context.SaveChangesAsync();
        var controller = new TelemetryDiagnosticsController(context);

        var action = await controller.GetSummary(
            fromUtc: RecordedAtUtc.AddMinutes(5),
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var summary = Assert.IsType<TelemetryDiagnosticsSummaryResponse>(
            ok.Value);
        Assert.Equal(1, summary.TotalReadings);
        Assert.Equal(
            RecordedAtUtc.AddMinutes(10),
            summary.EarliestRecordedAtUtc);
    }

    [Fact]
    public async Task GetSummary_ReturnsZeroedSummaryForNoReadings()
    {
        await using var context = CreateContext();
        var controller = new TelemetryDiagnosticsController(context);

        var action = await controller.GetSummary(
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var summary = Assert.IsType<TelemetryDiagnosticsSummaryResponse>(
            ok.Value);
        Assert.Equal(0, summary.TotalReadings);
        Assert.Equal(0d, summary.InvalidPercentage);
        Assert.Null(summary.EarliestRecordedAtUtc);
        Assert.Null(summary.LatestRecordedAtUtc);
    }

    [Fact]
    public async Task GetInvalidReadings_ReturnsContextualTypedData()
    {
        await using var context = CreateContext();
        var node = CreateNode();
        var sensor = CreateSensor(node.Id, 1, TelemetryValueKind.Float);
        context.AddRange(node, sensor);
        context.TelemetryRecords.AddRange(
            CreateFloatRecord(sensor.Id, 20, 0),
            CreateFloatRecord(sensor.Id, 35, 1, isValid: false));
        await context.SaveChangesAsync();
        var controller = new TelemetryDiagnosticsController(context);

        var action = await controller.GetInvalidReadings(
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<InvalidTelemetryPageResponse>(ok.Value);
        var reading = Assert.Single(page.Readings);
        Assert.Equal(sensor.FriendlyName, reading.SensorName);
        Assert.Equal(node.Code, reading.DeploymentLocationCode);
        Assert.Equal(35f, reading.FloatValue);
        Assert.Null(reading.IntegerValue);
        Assert.Null(reading.BooleanValue);
        Assert.False(string.IsNullOrWhiteSpace(reading.ValidationMessage));
    }

    [Fact]
    public async Task GetInvalidReadings_FiltersBySensor()
    {
        await using var context = CreateContext();
        var node = CreateNode();
        var firstSensor = CreateSensor(node.Id, 1, TelemetryValueKind.Float);
        var secondSensor = CreateSensor(node.Id, 2, TelemetryValueKind.Float);
        context.AddRange(node, firstSensor, secondSensor);
        context.TelemetryRecords.AddRange(
            CreateFloatRecord(firstSensor.Id, 35, 0, isValid: false),
            CreateFloatRecord(secondSensor.Id, 36, 1, isValid: false));
        await context.SaveChangesAsync();
        var controller = new TelemetryDiagnosticsController(context);

        var action = await controller.GetInvalidReadings(
            sensorId: secondSensor.Id,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<InvalidTelemetryPageResponse>(ok.Value);
        var reading = Assert.Single(page.Readings);
        Assert.Equal(secondSensor.Id, reading.SensorId);
    }

    [Fact]
    public async Task GetInvalidReadings_ReturnsNewestPage()
    {
        await using var context = CreateContext();
        var node = CreateNode();
        var sensor = CreateSensor(node.Id, 1, TelemetryValueKind.Integer);
        context.AddRange(node, sensor);
        context.TelemetryRecords.AddRange(
            CreateIntegerRecord(sensor.Id, 800, 0, isValid: false),
            CreateIntegerRecord(sensor.Id, 900, 1, isValid: false),
            CreateIntegerRecord(sensor.Id, 1000, 2, isValid: false));
        await context.SaveChangesAsync();
        var controller = new TelemetryDiagnosticsController(context);

        var action = await controller.GetInvalidReadings(
            page: 2,
            pageSize: 1,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<InvalidTelemetryPageResponse>(ok.Value);
        var reading = Assert.Single(page.Readings);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(900, reading.IntegerValue);
    }

    [Fact]
    public async Task GetInvalidReadings_ReturnsNotFoundForMissingSensor()
    {
        await using var context = CreateContext();
        var controller = new TelemetryDiagnosticsController(context);

        var action = await controller.GetInvalidReadings(
            sensorId: Guid.NewGuid(),
            cancellationToken: CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    public async Task GetInvalidReadings_RejectsInvalidPaging(
        int page,
        int pageSize)
    {
        await using var context = CreateContext();
        var controller = new TelemetryDiagnosticsController(context);

        var action = await controller.GetInvalidReadings(
            page: page,
            pageSize: pageSize,
            cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task GetSummary_RejectsReversedTimeRange()
    {
        await using var context = CreateContext();
        var controller = new TelemetryDiagnosticsController(context);

        var action = await controller.GetSummary(
            fromUtc: RecordedAtUtc.AddMinutes(1),
            toUtc: RecordedAtUtc,
            cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    private static SmartXDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SmartXDbContext(options);
    }

    private static DeploymentNode CreateNode()
    {
        return new DeploymentNode(
            Guid.NewGuid(),
            "Diagnostics Node",
            "NODE-DIAGNOSTICS",
            DeploymentNodeType.Node);
    }

    private static Sensor CreateSensor(
        Guid deploymentNodeId,
        int number,
        TelemetryValueKind valueKind)
    {
        var hasNumericRange = valueKind != TelemetryValueKind.Boolean;

        return new Sensor(
            Guid.NewGuid(),
            $"A4:CF:12:8B:70:{number:X2}",
            $"Diagnostics {valueKind} Sensor {number}",
            valueKind == TelemetryValueKind.Boolean
                ? SensorCategory.Actuator
                : SensorCategory.Environmental,
            "Diagnostic measurement",
            valueKind,
            valueKind == TelemetryValueKind.Boolean ? "state" : "unit",
            deploymentNodeId,
            hasNumericRange ? 10 : null,
            hasNumericRange ? 30 : null);
    }

    private static TelemetryRecord CreateFloatRecord(
        Guid sensorId,
        float value,
        int minuteOffset,
        bool isValid = true)
    {
        var packet = new TelemetryPacket<float>(
            Guid.NewGuid(),
            sensorId,
            value,
            RecordedAtUtc.AddMinutes(minuteOffset));

        return TelemetryRecord.FromPacket(
            packet,
            isValid,
            isValid ? null : "Float reading is outside the expected range.");
    }

    private static TelemetryRecord CreateIntegerRecord(
        Guid sensorId,
        int value,
        int minuteOffset,
        bool isValid = true)
    {
        var packet = new TelemetryPacket<int>(
            Guid.NewGuid(),
            sensorId,
            value,
            RecordedAtUtc.AddMinutes(minuteOffset));

        return TelemetryRecord.FromPacket(
            packet,
            isValid,
            isValid ? null : "Integer reading is outside the expected range.");
    }

    private static TelemetryRecord CreateBooleanRecord(
        Guid sensorId,
        bool value,
        int minuteOffset)
    {
        var packet = new TelemetryPacket<bool>(
            Guid.NewGuid(),
            sensorId,
            value,
            RecordedAtUtc.AddMinutes(minuteOffset));

        return TelemetryRecord.FromPacket(packet);
    }
}
