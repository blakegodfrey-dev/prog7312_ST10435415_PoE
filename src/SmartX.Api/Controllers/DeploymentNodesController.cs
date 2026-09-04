using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.DeploymentNodes;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/deployment-nodes")]
public sealed class DeploymentNodesController : ControllerBase
{
    private readonly SmartXDbContext _context;

    public DeploymentNodesController(SmartXDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyList<DeploymentNodeResponse>>> GetAll(
        [FromQuery] DeploymentNodeType? nodeType,
        CancellationToken cancellationToken)
    {
        if (nodeType.HasValue && !Enum.IsDefined(nodeType.Value))
        {
            return BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(nodeType)] =
                    [
                        "The deployment node type is not supported."
                    ]
                })
            {
                Title = "Deployment node request validation failed.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var query = _context.DeploymentNodes.AsNoTracking();

        if (nodeType.HasValue)
        {
            query = query.Where(node =>
                node.NodeType == nodeType.Value);
        }

        var nodes = await query
            .OrderBy(node => node.NodeType)
            .ThenBy(node => node.Name)
            .ThenBy(node => node.Id)
            .Select(node => new DeploymentNodeResponse(
                node.Id,
                node.Name,
                node.Code,
                node.NodeType,
                node.ParentId))
            .ToListAsync(cancellationToken);

        return Ok(nodes);
    }
}
