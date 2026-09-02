using System.Text.RegularExpressions;
using SmartX.Domain.Enums;

namespace SmartX.Domain.Entities;

/// <summary>
/// Represents a registered physical or simulated Smart-X sensor.
/// </summary>
public sealed class Sensor
{
    private static readonly Regex MacAddressPattern = new(
        @"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Sensor()
    {
        // Required later by Entity Framework Core.
    }

    public Sensor(
        Guid id,
        string macAddress,
        string friendlyName,
        SensorCategory category,
        string measuredProperty,
        TelemetryValueKind valueKind,
        string unit,
        Guid deploymentNodeId,
        double? expectedMinimum = null,
        double? expectedMaximum = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "A sensor must have a valid identifier.",
                nameof(id));
        }

        if (deploymentNodeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A sensor must be linked to a deployment node.",
                nameof(deploymentNodeId));
        }

        var normalisedMacAddress = RequireText(macAddress, nameof(macAddress))
            .ToUpperInvariant();

        if (!MacAddressPattern.IsMatch(normalisedMacAddress))
        {
            throw new ArgumentException(
                "The MAC address must use the format A4:CF:12:8B:39:01.",
                nameof(macAddress));
        }

        ValidateExpectedRange(
            valueKind,
            expectedMinimum,
            expectedMaximum);

        Id = id;
        MacAddress = normalisedMacAddress;
        FriendlyName = RequireText(friendlyName, nameof(friendlyName));
        Category = category;
        MeasuredProperty = RequireText(measuredProperty, nameof(measuredProperty));
        ValueKind = valueKind;
        Unit = unit?.Trim() ?? string.Empty;
        DeploymentNodeId = deploymentNodeId;
        ExpectedMinimum = expectedMinimum;
        ExpectedMaximum = expectedMaximum;
    }

    public Guid Id { get; private set; }

    public string MacAddress { get; private set; } = string.Empty;

    public string FriendlyName { get; private set; } = string.Empty;

    public SensorCategory Category { get; private set; }

    public string MeasuredProperty { get; private set; } = string.Empty;

    public TelemetryValueKind ValueKind { get; private set; }

    public string Unit { get; private set; } = string.Empty;

    public double? ExpectedMinimum { get; private set; }

    public double? ExpectedMaximum { get; private set; }

    public Guid DeploymentNodeId { get; private set; }

    public DeploymentNode? DeploymentNode { get; private set; }

    private static void ValidateExpectedRange(
        TelemetryValueKind valueKind,
        double? expectedMinimum,
        double? expectedMaximum)
    {
        if (expectedMinimum.HasValue != expectedMaximum.HasValue)
        {
            throw new ArgumentException(
                "Both expected minimum and maximum values must be supplied together.");
        }

        if (expectedMinimum > expectedMaximum)
        {
            throw new ArgumentException(
                "The expected minimum cannot be greater than the expected maximum.");
        }

        if (valueKind == TelemetryValueKind.Boolean &&
            (expectedMinimum.HasValue || expectedMaximum.HasValue))
        {
            throw new ArgumentException(
                "Boolean sensors cannot have a numeric expected range.");
        }
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A value is required.",
                parameterName);
        }

        return value.Trim();
    }
}