using SmartX.Application.Live;
using SmartX.Domain.Enums;

namespace SmartX.Application.Operations;

public static class TelemetrySeverity
{
    // Configuration-derived thresholds: a moisture drop below 2/3 of its minimum
    // or a power reading above 1.5x its maximum represents a severe incident.
    public static int Classify(DeviceSnapshot device, TelemetrySnapshot? reading)
    {
        if (reading is null || reading.IsValid) return 0;
        double? value = reading.FloatValue is { } f ? f : reading.IntegerValue;
        if (value is null) return 1;
        if (device.MeasuredProperty.Contains("moisture", StringComparison.OrdinalIgnoreCase) &&
            device.ExpectedMinimum is { } min && value < min * 2 / 3) return 3;
        if (device.Category == SensorCategory.PowerConsumption && device.ExpectedMaximum is { } max && value > max * 1.5) return 3;
        return 1;
    }
}
