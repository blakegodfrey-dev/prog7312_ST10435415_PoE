using System.Text.Json.Serialization;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Attachments;

public sealed record SensorAttachmentResponse(
    Guid Id,
    Guid SensorId,
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<SensorAttachmentCategory>))]
    SensorAttachmentCategory Category,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc,
    string DownloadUrl);
