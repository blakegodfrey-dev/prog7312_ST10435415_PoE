using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SmartX.Application.Mesh;
using SmartX.Domain.ValueObjects;
using SmartX.Infrastructure.Mesh;

namespace SmartX.Api.Controllers;

public sealed record MeshAvailabilityRequest([property: JsonRequired] bool IsAvailable);

[ApiController]
[Route("api/mesh")]
public sealed class MeshController(MeshNetworkService service, MeshSimulationState simulation,
    IOptions<MeshNetworkOptions> configuration, ILogger<MeshController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MeshSnapshot>> Get([FromQuery] string? subnetId = null,
        [FromQuery] Guid? nodeId = null, [FromQuery] string? macAddress = null, CancellationToken token = default)
    {
        if (subnetId is not null && !StringComparer.Ordinal.Equals(subnetId, configuration.Value.SubnetId))
            return Invalid("subnetId", "This gateway serves the configured hydroponic subnet only.");
        if ((nodeId.HasValue && macAddress is not null) || nodeId == Guid.Empty)
            return Invalid("nodeId", "Provide at most one non-empty nodeId or MAC address.");
        string? canonical = null;
        if (macAddress is not null && !MacAddressNormalizer.TryNormalize(macAddress, out canonical))
            return Invalid("macAddress", "Use a colon-separated MAC address.");
        var analysis = await service.AnalyseAsync(token);
        if (!nodeId.HasValue && canonical is null) return Ok(analysis.Snapshot);
        var id = nodeId ?? service.FindSensorIdByMac(canonical!);
        if (!id.HasValue || !analysis.Graph.Vertices.ContainsKey(id.Value)) return Missing("Mesh node not found.");
        var route = MeshAlgorithms.GatewayRoute(analysis.Graph, analysis.Snapshot.Forest,
            id.Value, analysis.Snapshot.GatewayId, token);
        return Ok(analysis.Snapshot with { SelectedRoute = route });
    }

    [HttpGet("route")]
    public async Task<ActionResult<MeshRoute>> GetRoute([FromQuery] Guid? nodeId = null,
        [FromQuery] string? macAddress = null, CancellationToken token = default)
    {
        if (nodeId.HasValue == (macAddress is not null) || nodeId == Guid.Empty)
            return Invalid("nodeId", "Provide exactly one non-empty nodeId or MAC address.");
        string? canonical = null;
        if (macAddress is not null && !MacAddressNormalizer.TryNormalize(macAddress, out canonical))
            return Invalid("macAddress", "Use a colon-separated MAC address.");
        var analysis = await service.AnalyseAsync(token);
        var id = nodeId ?? service.FindSensorIdByMac(canonical!);
        if (!id.HasValue || !analysis.Graph.Vertices.ContainsKey(id.Value)) return Missing("Mesh node not found.");
        return Ok(MeshAlgorithms.GatewayRoute(analysis.Graph, analysis.Snapshot.Forest,
            id.Value, analysis.Snapshot.GatewayId, token));
    }

    [HttpPut("nodes/{id:guid}/availability")]
    public async Task<ActionResult<MeshSnapshot>> NodeAvailability(Guid id, MeshAvailabilityRequest request, CancellationToken token)
    {
        if (!configuration.Value.AllowSimulation) return Disabled();
        var analysis = await service.AnalyseAsync(token);
        if (!analysis.Graph.Vertices.ContainsKey(id)) return Missing("Mesh node not found.");
        simulation.SetNodeAvailable(id, request.IsAvailable);
        logger.LogInformation("Mesh simulation node {NodeId} available override cleared: {IsAvailable}", id, request.IsAvailable);
        return Ok((await service.AnalyseAsync(token)).Snapshot);
    }

    [HttpPut("links/{id:guid}/availability")]
    public async Task<ActionResult<MeshSnapshot>> LinkAvailability(Guid id, MeshAvailabilityRequest request, CancellationToken token)
    {
        if (!configuration.Value.AllowSimulation) return Disabled();
        var analysis = await service.AnalyseAsync(token);
        if (!analysis.Snapshot.Links.Any(e => e.Id == id)) return Missing("Mesh link not found.");
        simulation.SetLinkAvailable(id, request.IsAvailable);
        logger.LogInformation("Mesh simulation link {LinkId} available override cleared: {IsAvailable}", id, request.IsAvailable);
        return Ok((await service.AnalyseAsync(token)).Snapshot);
    }

    [HttpDelete("simulation")]
    public async Task<ActionResult<MeshSnapshot>> Reset(CancellationToken token)
    {
        if (!configuration.Value.AllowSimulation) return Disabled();
        simulation.Reset();
        logger.LogInformation("Mesh simulation failures reset.");
        return Ok((await service.AnalyseAsync(token)).Snapshot);
    }

    private BadRequestObjectResult Invalid(string key, string message) => BadRequest(new ValidationProblemDetails(
        new Dictionary<string, string[]> { [key] = [message] })
        { Title = "Mesh request validation failed.", Status = StatusCodes.Status400BadRequest });
    private NotFoundObjectResult Missing(string message) => NotFound(new ProblemDetails
        { Title = message, Status = StatusCodes.Status404NotFound });
    private ObjectResult Disabled() => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        { Title = "Mesh failure simulation is disabled in this environment.", Status = StatusCodes.Status403Forbidden });
}
