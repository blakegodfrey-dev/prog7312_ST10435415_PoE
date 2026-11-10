using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Attachments;

public sealed class UploadSensorAttachmentRequest
{
    public IFormFile? File { get; init; }

    public SensorAttachmentCategory Category { get; init; }
}
