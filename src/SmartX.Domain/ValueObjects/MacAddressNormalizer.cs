using System.Text.RegularExpressions;

namespace SmartX.Domain.ValueObjects;

/// <summary>Reuses Part 1's colon-separated, trimmed uppercase MAC format.</summary>
public static class MacAddressNormalizer
{
    private static readonly Regex Pattern = new(
        @"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Normalize(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            throw new ArgumentException("A value is required.", nameof(macAddress));
        if (!TryNormalize(macAddress, out var canonical))
        {
            throw new ArgumentException(
                "The MAC address must use the format A4:CF:12:8B:39:01.",
                nameof(macAddress));
        }
        return canonical;
    }

    public static bool TryNormalize(string? macAddress, out string canonical)
    {
        canonical = macAddress?.Trim().ToUpperInvariant() ?? string.Empty;
        return Pattern.IsMatch(canonical);
    }
}
