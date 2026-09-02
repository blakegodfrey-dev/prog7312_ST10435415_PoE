using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Sensors;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Persistence;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SensorsController : ControllerBase
{
    private static readonly Expression<Func<Sensor, SensorResponse>>
        ResponseProjection = sensor => new SensorResponse(
            sensor.Id,
            sensor.MacAddress,
            sensor.FriendlyName,
            sensor.Category,
            sensor.MeasuredProperty,
            sensor.ValueKind,
            sensor.Unit,
            sensor.ExpectedMinimum,
            sensor.ExpectedMaximum,
            new DeploymentLocationResponse(
                sensor.DeploymentNode!.Id,
                sensor.DeploymentNode.Name,
                sensor.DeploymentNode.Code,
                sensor.DeploymentNode.NodeType,
                sensor.DeploymentNode.ParentId));

    private readonly SmartXDbContext _context;

    public SensorsController(SmartXDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SensorResponse>>> GetAll(
        [FromQuery] SensorCategory? category,
        [FromQuery] Guid? deploymentNodeId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = _context.Sensors.AsNoTracking();

        if (category.HasValue)
        {
            if (!Enum.IsDefined(category.Value))
            {
                return ValidationError(
                    nameof(category),
                    "The sensor category is not supported.");
            }

            query = query.Where(sensor => sensor.Category == category.Value);
        }

        if (deploymentNodeId.HasValue)
        {
            query = query.Where(sensor =>
                sensor.DeploymentNodeId == deploymentNodeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(sensor =>
                sensor.FriendlyName.Contains(searchTerm) ||
                sensor.MacAddress.Contains(searchTerm) ||
                sensor.MeasuredProperty.Contains(searchTerm));
        }

        var sensors = await query
            .OrderBy(sensor => sensor.FriendlyName)
            .ThenBy(sensor => sensor.Id)
            .Select(ResponseProjection)
            .ToListAsync(cancellationToken);

        return Ok(sensors);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SensorResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var sensor = await _context.Sensors
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(ResponseProjection)
            .SingleOrDefaultAsync(cancellationToken);

        if (sensor is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Sensor not found.",
                Detail = $"No sensor with identifier '{id}' exists.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(sensor);
    }

    [HttpPost]
    public async Task<ActionResult<SensorResponse>> Register(
        RegisterSensorRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return ValidationError(
                nameof(request.Id),
                "A sensor identifier is required.");
        }

        if (request.DeploymentNodeId == Guid.Empty)
        {
            return ValidationError(
                nameof(request.DeploymentNodeId),
                "A deployment node identifier is required.");
        }

        if (!Enum.IsDefined(request.Category))
        {
            return ValidationError(
                nameof(request.Category),
                "The sensor category is not supported.");
        }

        if (!Enum.IsDefined(request.ValueKind))
        {
            return ValidationError(
                nameof(request.ValueKind),
                "The telemetry value type is not supported.");
        }

        var deploymentNode = await _context.DeploymentNodes
            .SingleOrDefaultAsync(
                node => node.Id == request.DeploymentNodeId,
                cancellationToken);

        if (deploymentNode is null)
        {
            return ValidationError(
                nameof(request.DeploymentNodeId),
                "The selected deployment node does not exist.");
        }

        if (deploymentNode.NodeType != DeploymentNodeType.Node)
        {
            return ValidationError(
                nameof(request.DeploymentNodeId),
                "A sensor must be deployed at the Node level.");
        }

        var normalisedMacAddress = request.MacAddress.Trim()
            .ToUpperInvariant();

        if (await _context.Sensors.AnyAsync(
                sensor => sensor.Id == request.Id,
                cancellationToken))
        {
            return ConflictError(
                $"Sensor identifier '{request.Id}' is already registered.");
        }

        if (!string.IsNullOrEmpty(normalisedMacAddress) &&
            await _context.Sensors.AnyAsync(
                sensor => sensor.MacAddress == normalisedMacAddress,
                cancellationToken))
        {
            return ConflictError(
                $"MAC address '{normalisedMacAddress}' is already registered.");
        }

        Sensor sensor;

        try
        {
            sensor = new Sensor(
                request.Id,
                request.MacAddress,
                request.FriendlyName,
                request.Category,
                request.MeasuredProperty,
                request.ValueKind,
                request.Unit,
                request.DeploymentNodeId,
                request.ExpectedMinimum,
                request.ExpectedMaximum);
        }
        catch (ArgumentException exception)
        {
            return ValidationError("sensor", exception.Message);
        }

        _context.Sensors.Add(sensor);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ConflictError(
                "The sensor conflicts with an existing registration.");
        }

        var response = new SensorResponse(
            sensor.Id,
            sensor.MacAddress,
            sensor.FriendlyName,
            sensor.Category,
            sensor.MeasuredProperty,
            sensor.ValueKind,
            sensor.Unit,
            sensor.ExpectedMinimum,
            sensor.ExpectedMaximum,
            new DeploymentLocationResponse(
                deploymentNode.Id,
                deploymentNode.Name,
                deploymentNode.Code,
                deploymentNode.NodeType,
                deploymentNode.ParentId));

        return CreatedAtAction(
            nameof(GetById),
            new { id = sensor.Id },
            response);
    }

    private ActionResult ValidationError(string key, string message)
    {
        return BadRequest(new ValidationProblemDetails(
            new Dictionary<string, string[]>
            {
                [key] = [message]
            })
        {
            Title = "Sensor request validation failed.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private ActionResult ConflictError(string detail)
    {
        return Conflict(new ProblemDetails
        {
            Title = "Sensor registration conflict.",
            Detail = detail,
            Status = StatusCodes.Status409Conflict
        });
    }
}
