using SmartX.Application.Live;
namespace SmartX.Application.Operations;
public static class TelemetryContext
{
    public static string? For(DeviceSnapshot? device)
    {
        if (device?.LatestReading is not { IsValid: false } reading) return null;
        double? value = reading.FloatValue is { } f ? f : reading.IntegerValue;
        if (value is null) return null;
        var direction = value < device.ExpectedMinimum ? "BelowMinimum" : value > device.ExpectedMaximum ? "AboveMaximum" : "Invalid";
        return $"{device.Id}:{direction}";
    }
}
