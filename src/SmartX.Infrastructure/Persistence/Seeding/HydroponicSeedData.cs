using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Domain.Telemetry;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Infrastructure.Persistence.Seeding;

/// <summary>
/// Creates deterministic demonstration data for a simulated hydroponic
/// facility. Supplying the same end time always produces the same dataset.
/// </summary>
public static class HydroponicSeedData
{
    public const int ReadingsPerSensor = 288;
    public const int ReadingIntervalMinutes = 5;

    private const int FirstHighAnomalyIndex = 72;
    private const int SecondLowAnomalyIndex = 216;

    public static HydroponicSeedDataset Create(DateTimeOffset seedEndUtc)
    {
        if (seedEndUtc == default)
        {
            throw new ArgumentException(
                "A seed end timestamp is required.",
                nameof(seedEndUtc));
        }

        var deploymentNodes = CreateDeploymentNodes();
        var definitions = CreateSensorDefinitions(deploymentNodes);
        var telemetryRecords = CreateTelemetryRecords(
            definitions,
            seedEndUtc.ToUniversalTime());

        return new HydroponicSeedDataset(
            deploymentNodes,
            definitions.Select(definition => definition.Sensor).ToList(),
            telemetryRecords);
    }

    private static List<DeploymentNode> CreateDeploymentNodes()
    {
        var facility = new DeploymentNode(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "Smart-X Hydroponic Facility",
            "FAC-PTA-01",
            DeploymentNodeType.Facility);

        var productionZone = new DeploymentNode(
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            "Production Zone",
            "ZONE-PROD",
            DeploymentNodeType.Zone);

        var utilitiesZone = new DeploymentNode(
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            "Utilities Zone",
            "ZONE-UTIL",
            DeploymentNodeType.Zone);

        var growRoom = new DeploymentNode(
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            "Grow Room A",
            "SUB-GROW-A",
            DeploymentNodeType.SubZone);

        var nutrientRoom = new DeploymentNode(
            Guid.Parse("10000000-0000-0000-0000-000000000005"),
            "Nutrient Room",
            "SUB-NUTRIENT",
            DeploymentNodeType.SubZone);

        var powerRoom = new DeploymentNode(
            Guid.Parse("10000000-0000-0000-0000-000000000006"),
            "Power Room",
            "SUB-POWER",
            DeploymentNodeType.SubZone);

        var growBed = new DeploymentNode(
            Guid.Parse("10000000-0000-0000-0000-000000000007"),
            "Grow Bed A1",
            "NODE-BED-A1",
            DeploymentNodeType.Node);

        var reservoir = new DeploymentNode(
            Guid.Parse("10000000-0000-0000-0000-000000000008"),
            "Nutrient Reservoir 1",
            "NODE-RES-01",
            DeploymentNodeType.Node);

        var electricalPanel = new DeploymentNode(
            Guid.Parse("10000000-0000-0000-0000-000000000009"),
            "Electrical Panel 1",
            "NODE-PANEL-01",
            DeploymentNodeType.Node);

        facility.AddChild(productionZone);
        facility.AddChild(utilitiesZone);
        productionZone.AddChild(growRoom);
        utilitiesZone.AddChild(nutrientRoom);
        utilitiesZone.AddChild(powerRoom);
        growRoom.AddChild(growBed);
        nutrientRoom.AddChild(reservoir);
        powerRoom.AddChild(electricalPanel);

        return
        [
            facility,
            productionZone,
            utilitiesZone,
            growRoom,
            nutrientRoom,
            powerRoom,
            growBed,
            reservoir,
            electricalPanel
        ];
    }

    private static List<SensorDefinition> CreateSensorDefinitions(
        IReadOnlyList<DeploymentNode> deploymentNodes)
    {
        var growBedId = FindNodeId(deploymentNodes, "NODE-BED-A1");
        var reservoirId = FindNodeId(deploymentNodes, "NODE-RES-01");
        var electricalPanelId = FindNodeId(
            deploymentNodes,
            "NODE-PANEL-01");

        return
        [
            CreateNumericSensor(1, "Air Temperature", SensorCategory.Environmental,
                "Air temperature", TelemetryValueKind.Float, "°C", growBedId,
                18, 28, 23, 3, 288),
            CreateNumericSensor(2, "Air Humidity", SensorCategory.Environmental,
                "Relative humidity", TelemetryValueKind.Float, "%", growBedId,
                55, 75, 65, 7, 144),
            CreateNumericSensor(3, "Nutrient pH", SensorCategory.Environmental,
                "Nutrient pH", TelemetryValueKind.Float, "pH", reservoirId,
                5.5, 6.5, 6, 0.3, 96),
            CreateNumericSensor(4, "Nutrient Conductivity", SensorCategory.Environmental,
                "Electrical conductivity", TelemetryValueKind.Float, "mS/cm",
                reservoirId, 1.2, 2.4, 1.8, 0.35, 144),
            CreateNumericSensor(5, "Solution Temperature", SensorCategory.Environmental,
                "Nutrient solution temperature", TelemetryValueKind.Float, "°C",
                reservoirId, 18, 24, 21, 1.5, 288),
            CreateNumericSensor(6, "Reservoir Level", SensorCategory.Environmental,
                "Reservoir level", TelemetryValueKind.Float, "%", reservoirId,
                30, 100, 70, 20, 288),
            CreateNumericSensor(7, "Irrigation Flow", SensorCategory.Environmental,
                "Irrigation flow rate", TelemetryValueKind.Integer, "L/h", growBedId,
                400, 700, 550, 80, 96),
            CreateNumericSensor(8, "Pump Power Draw", SensorCategory.PowerConsumption,
                "Pump power consumption", TelemetryValueKind.Integer, "W",
                electricalPanelId, 200, 500, 330, 60, 48),
            CreateNumericSensor(9, "Lighting Power Draw", SensorCategory.PowerConsumption,
                "Grow light power consumption", TelemetryValueKind.Integer, "W",
                electricalPanelId, 700, 1200, 900, 120, 288),
            CreateBooleanSensor(10, "Circulation Pump", "Pump running",
                reservoirId, initialState: true, period: 12),
            CreateBooleanSensor(11, "Irrigation Valve", "Valve open",
                growBedId, initialState: false, period: 6),
            CreateBooleanSensor(12, "Grow Lights", "Grow lights on",
                growBedId, initialState: true, period: 144)
        ];
    }

    private static SensorDefinition CreateNumericSensor(
        int number,
        string friendlyName,
        SensorCategory category,
        string measuredProperty,
        TelemetryValueKind valueKind,
        string unit,
        Guid deploymentNodeId,
        double minimum,
        double maximum,
        double baseline,
        double amplitude,
        int period)
    {
        return new SensorDefinition(
            new Sensor(
                CreateSensorId(number),
                $"A4:CF:12:8B:39:{number:X2}",
                friendlyName,
                category,
                measuredProperty,
                valueKind,
                unit,
                deploymentNodeId,
                minimum,
                maximum),
            baseline,
            amplitude,
            period,
            false);
    }

    private static SensorDefinition CreateBooleanSensor(
        int number,
        string friendlyName,
        string measuredProperty,
        Guid deploymentNodeId,
        bool initialState,
        int period)
    {
        return new SensorDefinition(
            new Sensor(
                CreateSensorId(number),
                $"A4:CF:12:8B:39:{number:X2}",
                friendlyName,
                SensorCategory.Actuator,
                measuredProperty,
                TelemetryValueKind.Boolean,
                "state",
                deploymentNodeId),
            0,
            0,
            period,
            initialState);
    }

    private static List<TelemetryRecord> CreateTelemetryRecords(
        IReadOnlyList<SensorDefinition> definitions,
        DateTimeOffset seedEndUtc)
    {
        var records = new List<TelemetryRecord>(
            definitions.Count * ReadingsPerSensor);
        var firstRecordedAtUtc = seedEndUtc.AddMinutes(
            -ReadingIntervalMinutes * (ReadingsPerSensor - 1));
        var telemetrySequence = 1;

        foreach (var definition in definitions)
        {
            for (var readingIndex = 0;
                 readingIndex < ReadingsPerSensor;
                 readingIndex++)
            {
                var recordedAtUtc = firstRecordedAtUtc.AddMinutes(
                    ReadingIntervalMinutes * readingIndex);
                var receivedAtUtc = recordedAtUtc.AddSeconds(
                    2 + (readingIndex % 4));
                var isValid = readingIndex != FirstHighAnomalyIndex &&
                    readingIndex != SecondLowAnomalyIndex;
                var validationMessage = isValid
                    ? null
                    : CreateValidationMessage(definition.Sensor);

                records.Add(CreateTelemetryRecord(
                    definition,
                    readingIndex,
                    telemetrySequence,
                    recordedAtUtc,
                    receivedAtUtc,
                    isValid,
                    validationMessage));

                telemetrySequence++;
            }
        }

        return records;
    }

    private static TelemetryRecord CreateTelemetryRecord(
        SensorDefinition definition,
        int readingIndex,
        int telemetrySequence,
        DateTimeOffset recordedAtUtc,
        DateTimeOffset receivedAtUtc,
        bool isValid,
        string? validationMessage)
    {
        var sensor = definition.Sensor;
        var telemetryId = Guid.Parse(
            $"30000000-0000-0000-0000-{telemetrySequence:X12}");

        return sensor.ValueKind switch
        {
            TelemetryValueKind.Float => TelemetryRecord.FromPacket(
                new TelemetryPacket<float>(
                    telemetryId,
                    sensor.Id,
                    CreateFloatValue(definition, readingIndex),
                    recordedAtUtc,
                    receivedAtUtc),
                isValid,
                validationMessage),

            TelemetryValueKind.Integer => TelemetryRecord.FromPacket(
                new TelemetryPacket<int>(
                    telemetryId,
                    sensor.Id,
                    CreateIntegerValue(definition, readingIndex),
                    recordedAtUtc,
                    receivedAtUtc),
                isValid,
                validationMessage),

            TelemetryValueKind.Boolean => TelemetryRecord.FromPacket(
                new TelemetryPacket<bool>(
                    telemetryId,
                    sensor.Id,
                    CreateBooleanValue(definition, readingIndex),
                    recordedAtUtc,
                    receivedAtUtc),
                isValid,
                validationMessage),

            _ => throw new InvalidOperationException(
                $"Unsupported seed telemetry type '{sensor.ValueKind}'.")
        };
    }

    private static float CreateFloatValue(
        SensorDefinition definition,
        int readingIndex)
    {
        if (readingIndex == FirstHighAnomalyIndex)
        {
            return (float)(definition.Sensor.ExpectedMaximum!.Value +
                definition.Amplitude);
        }

        if (readingIndex == SecondLowAnomalyIndex)
        {
            return (float)(definition.Sensor.ExpectedMinimum!.Value -
                definition.Amplitude);
        }

        return (float)CreateNormalNumericValue(definition, readingIndex);
    }

    private static int CreateIntegerValue(
        SensorDefinition definition,
        int readingIndex)
    {
        if (readingIndex == FirstHighAnomalyIndex)
        {
            return (int)Math.Ceiling(
                definition.Sensor.ExpectedMaximum!.Value +
                definition.Amplitude);
        }

        if (readingIndex == SecondLowAnomalyIndex)
        {
            return (int)Math.Floor(
                definition.Sensor.ExpectedMinimum!.Value -
                definition.Amplitude);
        }

        return (int)Math.Round(
            CreateNormalNumericValue(definition, readingIndex),
            MidpointRounding.AwayFromZero);
    }

    private static double CreateNormalNumericValue(
        SensorDefinition definition,
        int readingIndex)
    {
        var primaryWave = Math.Sin(
            2 * Math.PI * readingIndex / definition.Period);
        var secondaryWave = Math.Cos(
            2 * Math.PI * readingIndex / (definition.Period / 2d));

        return definition.Baseline +
            (definition.Amplitude * primaryWave) +
            (definition.Amplitude * 0.08 * secondaryWave);
    }

    private static bool CreateBooleanValue(
        SensorDefinition definition,
        int readingIndex)
    {
        var currentPeriodIsEven =
            (readingIndex / definition.Period) % 2 == 0;

        return definition.InitialState
            ? currentPeriodIsEven
            : !currentPeriodIsEven;
    }

    private static string CreateValidationMessage(Sensor sensor)
    {
        return sensor.ValueKind == TelemetryValueKind.Boolean
            ? "Simulated device checksum validation failure."
            : $"Simulated {sensor.MeasuredProperty.ToLowerInvariant()} " +
              "reading outside the expected range.";
    }

    private static Guid FindNodeId(
        IEnumerable<DeploymentNode> deploymentNodes,
        string code)
    {
        return deploymentNodes.Single(node => node.Code == code).Id;
    }

    private static Guid CreateSensorId(int number)
    {
        return Guid.Parse(
            $"20000000-0000-0000-0000-{number:X12}");
    }

    private sealed record SensorDefinition(
        Sensor Sensor,
        double Baseline,
        double Amplitude,
        int Period,
        bool InitialState);
}
