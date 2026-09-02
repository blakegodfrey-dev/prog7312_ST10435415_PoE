using SmartX.Domain.Enums;
using SmartX.Domain.Validation;
using SmartX.Infrastructure.Persistence.Entities;
using SmartX.Infrastructure.Persistence.Seeding;

namespace SmartX.Tests.Infrastructure;

public sealed class HydroponicSeedDataTests
{
    private static readonly DateTimeOffset SeedEndUtc =
        new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_BuildsAValidFourLevelDeploymentHierarchy()
    {
        var dataset = HydroponicSeedData.Create(SeedEndUtc);
        var root = Assert.Single(
            dataset.DeploymentNodes,
            node => node.NodeType == DeploymentNodeType.Facility);

        var validation = DeploymentHierarchyValidator.Validate(root);

        Assert.True(validation.IsValid);
        Assert.Equal(9, validation.NodesVisited);
        Assert.Equal(4, validation.MaximumDepthReached);
    }

    [Fact]
    public void Create_IncludesRealisticSensorsForEveryTelemetryType()
    {
        var dataset = HydroponicSeedData.Create(SeedEndUtc);

        Assert.Equal(12, dataset.Sensors.Count);
        Assert.Equal(
            6,
            dataset.Sensors.Count(sensor =>
                sensor.ValueKind == TelemetryValueKind.Float));
        Assert.Equal(
            3,
            dataset.Sensors.Count(sensor =>
                sensor.ValueKind == TelemetryValueKind.Integer));
        Assert.Equal(
            3,
            dataset.Sensors.Count(sensor =>
                sensor.ValueKind == TelemetryValueKind.Boolean));
        Assert.Equal(
            Enum.GetValues<SensorCategory>().Order(),
            dataset.Sensors.Select(sensor => sensor.Category)
                .Distinct()
                .Order());
    }

    [Fact]
    public void Create_GeneratesTwentyFourHoursOfTypedTelemetry()
    {
        var dataset = HydroponicSeedData.Create(SeedEndUtc);

        Assert.Equal(
            dataset.Sensors.Count * HydroponicSeedData.ReadingsPerSensor,
            dataset.TelemetryRecords.Count);

        foreach (var sensor in dataset.Sensors)
        {
            var sensorRecords = dataset.TelemetryRecords
                .Where(record => record.SensorId == sensor.Id)
                .OrderBy(record => record.RecordedAtUtc)
                .ToList();

            Assert.Equal(
                HydroponicSeedData.ReadingsPerSensor,
                sensorRecords.Count);
            Assert.Equal(SeedEndUtc, sensorRecords[^1].RecordedAtUtc);
            Assert.Equal(
                TimeSpan.FromMinutes(
                    HydroponicSeedData.ReadingIntervalMinutes),
                sensorRecords[1].RecordedAtUtc -
                sensorRecords[0].RecordedAtUtc);
        }
    }

    [Fact]
    public void Create_PopulatesExactlyOneNativeValueColumnPerRecord()
    {
        var dataset = HydroponicSeedData.Create(SeedEndUtc);

        foreach (var record in dataset.TelemetryRecords)
        {
            var populatedColumnCount =
                (record.FloatValue.HasValue ? 1 : 0) +
                (record.IntegerValue.HasValue ? 1 : 0) +
                (record.BooleanValue.HasValue ? 1 : 0);

            Assert.Equal(1, populatedColumnCount);
            Assert.Equal(
                ExpectedKind(record),
                record.ValueKind);
        }
    }

    [Fact]
    public void Create_IncludesRepeatableValidationAnomalies()
    {
        var first = HydroponicSeedData.Create(SeedEndUtc);
        var second = HydroponicSeedData.Create(SeedEndUtc);
        var expectedInvalidCount = first.Sensors.Count * 2;

        Assert.Equal(
            expectedInvalidCount,
            first.TelemetryRecords.Count(record => !record.IsValid));
        Assert.All(
            first.TelemetryRecords.Where(record => !record.IsValid),
            record => Assert.False(
                string.IsNullOrWhiteSpace(record.ValidationMessage)));
        Assert.Equal(
            first.TelemetryRecords.Select(record => record.Id),
            second.TelemetryRecords.Select(record => record.Id));
    }

    private static TelemetryValueKind ExpectedKind(TelemetryRecord record)
    {
        if (record.FloatValue.HasValue)
        {
            return TelemetryValueKind.Float;
        }

        if (record.IntegerValue.HasValue)
        {
            return TelemetryValueKind.Integer;
        }

        return TelemetryValueKind.Boolean;
    }
}
