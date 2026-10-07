using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Contracts.Live;
using SmartX.Domain.ValueObjects;
using SmartX.Infrastructure.Live;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/live")]
public sealed class LiveDevicesController(LiveTelemetryService live) : ControllerBase
{
    [HttpGet("devices")]
    public async Task<ActionResult<IReadOnlyList<LiveDeviceResponse>>> GetDevices(CancellationToken cancellationToken)
    {
        await live.EnsureInitializedAsync(cancellationToken);
        return Ok(live.Store.GetDevices().Select(LiveDeviceResponse.FromSnapshot).ToArray());
    }

    [HttpGet("devices/{macAddress}")]
    public async Task<ActionResult<LiveDeviceResponse>> GetDevice(string macAddress, CancellationToken cancellationToken)
    {
        if (!MacAddressNormalizer.TryNormalize(macAddress, out var canonical))
            return ValidationError(nameof(macAddress), "Use a colon-separated MAC address.");
        await live.EnsureInitializedAsync(cancellationToken);
        var device = live.Store.FindByMac(canonical);
        return device is null ? MissingDevice(canonical) : Ok(LiveDeviceResponse.FromSnapshot(device));
    }

    [HttpGet("history")]
    public async Task<ActionResult<RecentLiveHistoryResponse>> GetHistory(
        [FromQuery] string? macAddress = null, [FromQuery] int limit = 100,
        [FromQuery] DateTimeOffset? fromUtc = null, [FromQuery] DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 500)
            return ValidationError(nameof(limit), "Limit must be between 1 and 500.");
        if (fromUtc > toUtc)
            return ValidationError(nameof(fromUtc), "The start timestamp cannot be after the end timestamp.");
        string? canonical = null;
        if (macAddress is not null && !MacAddressNormalizer.TryNormalize(macAddress, out canonical))
            return ValidationError(nameof(macAddress), "Use a colon-separated MAC address.");
        await live.EnsureInitializedAsync(cancellationToken);
        var device = canonical is null ? null : live.Store.FindByMac(canonical);
        if (canonical is not null && device is null) return MissingDevice(canonical);
        var history = live.Store.GetRecentHistory(device?.Id, limit, fromUtc, toUtc);
        return Ok(new RecentLiveHistoryResponse(
            "RecentMemory", history.Capacity, history.RetainedCount, history.MatchingCount,
            history.Limit, history.IsLimited, history.OldestRetainedAtUtc, history.NewestRetainedAtUtc,
            history.Readings.Select(LiveTelemetryReadingResponse.FromSnapshot).ToArray()));
    }

    private BadRequestObjectResult ValidationError(string key, string message) => BadRequest(
        new ValidationProblemDetails(new Dictionary<string, string[]> { [key] = [message] })
        { Title = "Live-device request validation failed.", Status = StatusCodes.Status400BadRequest });

    private NotFoundObjectResult MissingDevice(string macAddress) => NotFound(new ProblemDetails
    {
        Title = "Live device not found.", Detail = $"No device with MAC address '{macAddress}' is registered.",
        Status = StatusCodes.Status404NotFound
    });
}
