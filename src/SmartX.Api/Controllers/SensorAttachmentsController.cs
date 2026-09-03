using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Contracts.Attachments;
using SmartX.Application.Attachments;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Api.Controllers;

[ApiController]
[Route("api/sensors/{sensorId:guid}/attachments")]
public sealed class SensorAttachmentsController : ControllerBase
{
    public const long MaximumFileSizeBytes = 5 * 1024 * 1024;

    private readonly SmartXDbContext _context;
    private readonly IAttachmentFileStorage _storage;
    private readonly ILogger<SensorAttachmentsController> _logger;

    public SensorAttachmentsController(
        SmartXDbContext context,
        IAttachmentFileStorage storage,
        ILogger<SensorAttachmentsController> logger)
    {
        _context = context;
        _storage = storage;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumFileSizeBytes + 1024 * 1024)]
    public async Task<ActionResult<SensorAttachmentResponse>> Upload(
        Guid sensorId,
        [FromForm] UploadSensorAttachmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _context.Sensors.AnyAsync(
                sensor => sensor.Id == sensorId,
                cancellationToken))
        {
            return NotFoundError(
                $"No sensor with identifier '{sensorId}' exists.");
        }

        if (!Enum.IsDefined(request.Category))
        {
            return ValidationError(
                nameof(request.Category),
                "The attachment category is not supported.");
        }

        if (request.File is null || request.File.Length == 0)
        {
            return ValidationError(
                nameof(request.File),
                "A non-empty attachment file is required.");
        }

        var file = request.File;

        if (file.Length > MaximumFileSizeBytes)
        {
            return ValidationError(
                nameof(request.File),
                $"The attachment cannot exceed " +
                $"{MaximumFileSizeBytes / (1024 * 1024)} MB.");
        }

        var originalFileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(originalFileName) ||
            originalFileName.Length >
            SensorAttachmentRecord.MaximumFileNameLength)
        {
            return ValidationError(
                nameof(request.File),
                "The attachment must have a valid file name of 255 characters or fewer.");
        }

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var contentType = NormaliseContentType(file.ContentType);

        if (!IsAllowedFile(request.Category, extension, contentType))
        {
            return ValidationError(
                nameof(request.File),
                "The file extension or content type is not allowed for this category.");
        }

        StoredAttachmentFile storedFile;

        await using (var content = file.OpenReadStream())
        {
            storedFile = await _storage.SaveAsync(
                content,
                extension,
                cancellationToken);
        }

        var record = new SensorAttachmentRecord(
            Guid.NewGuid(),
            sensorId,
            request.Category,
            originalFileName,
            storedFile.StoredFileName,
            contentType,
            file.Length,
            storedFile.RelativePath,
            DateTimeOffset.UtcNow);

        _context.SensorAttachments.Add(record);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await TryDeleteFileAsync(
                storedFile.RelativePath,
                cancellationToken);
            _logger.LogWarning(
                exception,
                "Attachment metadata could not be saved for sensor {SensorId}.",
                sensorId);

            return Conflict(new ProblemDetails
            {
                Title = "Attachment upload conflict.",
                Detail = "The attachment metadata could not be saved.",
                Status = StatusCodes.Status409Conflict
            });
        }

        return CreatedAtAction(
            nameof(GetMetadata),
            new { sensorId, attachmentId = record.Id },
            ToResponse(record));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SensorAttachmentResponse>>> List(
        Guid sensorId,
        CancellationToken cancellationToken)
    {
        if (!await _context.Sensors.AnyAsync(
                sensor => sensor.Id == sensorId,
                cancellationToken))
        {
            return NotFoundError(
                $"No sensor with identifier '{sensorId}' exists.");
        }

        var records = await _context.SensorAttachments
            .AsNoTracking()
            .Where(attachment => attachment.SensorId == sensorId)
            .OrderByDescending(attachment => attachment.UploadedAtUtc)
            .ThenBy(attachment => attachment.OriginalFileName)
            .ToListAsync(cancellationToken);
        var attachments = records
            .Select(ToResponse)
            .ToList();

        return Ok(attachments);
    }

    [HttpGet("{attachmentId:guid}")]
    public async Task<ActionResult<SensorAttachmentResponse>> GetMetadata(
        Guid sensorId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await FindAttachmentAsync(
            sensorId,
            attachmentId,
            trackChanges: false,
            cancellationToken);

        if (attachment is null)
        {
            return NotFoundError(
                $"No attachment with identifier '{attachmentId}' exists " +
                $"for sensor '{sensorId}'.");
        }

        return Ok(ToResponse(attachment));
    }

    [HttpGet("{attachmentId:guid}/content")]
    public async Task<IActionResult> Download(
        Guid sensorId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await FindAttachmentAsync(
            sensorId,
            attachmentId,
            trackChanges: false,
            cancellationToken);

        if (attachment is null)
        {
            return NotFoundError(
                $"No attachment with identifier '{attachmentId}' exists " +
                $"for sensor '{sensorId}'.");
        }

        Stream content;

        try
        {
            content = await _storage.OpenReadAsync(
                attachment.RelativePath,
                cancellationToken);
        }
        catch (FileNotFoundException exception)
        {
            _logger.LogWarning(
                exception,
                "Attachment file {AttachmentId} is missing from storage.",
                attachmentId);

            return NotFoundError(
                "The attachment metadata exists, but its stored file is missing.");
        }

        return File(
            content,
            attachment.ContentType,
            attachment.OriginalFileName,
            enableRangeProcessing: true);
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(
        Guid sensorId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await FindAttachmentAsync(
            sensorId,
            attachmentId,
            trackChanges: true,
            cancellationToken);

        if (attachment is null)
        {
            return NotFoundError(
                $"No attachment with identifier '{attachmentId}' exists " +
                $"for sensor '{sensorId}'.");
        }

        _context.SensorAttachments.Remove(attachment);
        await _context.SaveChangesAsync(cancellationToken);
        await TryDeleteFileAsync(
            attachment.RelativePath,
            cancellationToken);

        return NoContent();
    }

    private Task<SensorAttachmentRecord?> FindAttachmentAsync(
        Guid sensorId,
        Guid attachmentId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        IQueryable<SensorAttachmentRecord> query = trackChanges
            ? _context.SensorAttachments
            : _context.SensorAttachments.AsNoTracking();

        return query.SingleOrDefaultAsync(
            attachment => attachment.Id == attachmentId &&
                attachment.SensorId == sensorId,
            cancellationToken);
    }

    private async Task TryDeleteFileAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(relativePath, cancellationToken);
        }
        catch (IOException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not remove attachment file {RelativePath}.",
                relativePath);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Access was denied while removing attachment file {RelativePath}.",
                relativePath);
        }
    }

    private static string NormaliseContentType(string contentType)
    {
        return (contentType ?? string.Empty)
            .Split(';', 2)[0]
            .Trim()
            .ToLowerInvariant();
    }

    private static bool IsAllowedFile(
        SensorAttachmentCategory category,
        string extension,
        string contentType)
    {
        return category switch
        {
            SensorAttachmentCategory.ConfigurationFile =>
                (extension, contentType) is
                    (".json", "application/json") or
                    (".json", "text/json") or
                    (".txt", "text/plain") or
                    (".csv", "text/csv") or
                    (".csv", "application/vnd.ms-excel") or
                    (".pdf", "application/pdf"),

            SensorAttachmentCategory.DeploymentImage =>
                (extension, contentType) is
                    (".png", "image/png") or
                    (".jpg", "image/jpeg") or
                    (".jpeg", "image/jpeg"),

            SensorAttachmentCategory.HardwareLog =>
                (extension, contentType) is
                    (".log", "text/plain") or
                    (".log", "application/octet-stream") or
                    (".txt", "text/plain") or
                    (".csv", "text/csv") or
                    (".csv", "application/vnd.ms-excel"),

            _ => false
        };
    }

    private static SensorAttachmentResponse ToResponse(
        SensorAttachmentRecord attachment)
    {
        return new SensorAttachmentResponse(
            attachment.Id,
            attachment.SensorId,
            attachment.Category,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.UploadedAtUtc,
            $"/api/sensors/{attachment.SensorId}/attachments/" +
            $"{attachment.Id}/content");
    }

    private ActionResult NotFoundError(string detail)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Attachment resource not found.",
            Detail = detail,
            Status = StatusCodes.Status404NotFound
        });
    }

    private ActionResult ValidationError(string key, string message)
    {
        return BadRequest(new ValidationProblemDetails(
            new Dictionary<string, string[]>
            {
                [key] = [message]
            })
        {
            Title = "Attachment validation failed.",
            Status = StatusCodes.Status400BadRequest
        });
    }
}
