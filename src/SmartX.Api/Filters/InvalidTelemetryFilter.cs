using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using SmartX.Application.Live;
using SmartX.Application.Operations;
namespace SmartX.Api.Filters;

// Runs before ApiController's automatic model-state response. A structurally invalid
// packet can still identify its sender; unknown/unparseable senders get only HTTP 400.
public sealed class InvalidTelemetryFilter(IncidentTracker incidents, LiveTelemetryStore live) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var ids = new HashSet<Guid>();
        if (HttpMethods.IsPost(request.Method) && request.Path.StartsWithSegments("/api/telemetry") &&
            request.ContentLength is > 0 and <= 1048576 && request.ContentType?.Contains("application/json") == true)
        {
            request.EnableBuffering();
            try
            {
                using var json = await JsonDocument.ParseAsync(request.Body, cancellationToken: context.HttpContext.RequestAborted);
                FindSender(json.RootElement, ids);
            }
            catch (JsonException) { }
            finally { request.Body.Position = 0; }
        }
        var result = await next();
        if (context.HttpContext.Response.StatusCode == 400 || result.Result is Microsoft.AspNetCore.Mvc.BadRequestObjectResult)
            foreach (var id in ids.Where(id => live.FindById(id) is not null)) incidents.InvalidPayload(id, "Payload rejected: malformed value, type, identifier or timestamp. Check the HTTP validation response.");
    }
    private static void FindSender(JsonElement element, HashSet<Guid> ids)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        if (element.TryGetProperty("sensorId", out var id) && id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var parsed)) ids.Add(parsed);
        if (element.TryGetProperty("readings", out var items) && items.ValueKind == JsonValueKind.Array)
            foreach (var item in items.EnumerateArray()) FindSender(item, ids);
    }
}
