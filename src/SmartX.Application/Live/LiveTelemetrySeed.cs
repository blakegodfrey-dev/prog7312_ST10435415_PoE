namespace SmartX.Application.Live;

public sealed record LiveTelemetrySeed(
    IReadOnlyList<DeviceSnapshot> Devices,
    IReadOnlyList<TelemetrySnapshot> RecentReadings);
