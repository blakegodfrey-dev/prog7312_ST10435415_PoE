using SmartX.Domain.Enums;

namespace SmartX.Infrastructure.Persistence.Entities;

/// <summary>
/// Stores safe metadata for a sensor attachment. The file contents remain
/// in protected server-side storage rather than inside SQL Server.
/// </summary>
public sealed class SensorAttachmentRecord
{
    public const int MaximumFileNameLength = 255;
    public const int MaximumContentTypeLength = 100;
    public const int MaximumRelativePathLength = 500;

    private SensorAttachmentRecord()
    {
        // Required by Entity Framework Core.
    }

    public SensorAttachmentRecord(
        Guid id,
        Guid sensorId,
        SensorAttachmentCategory category,
        string originalFileName,
        string storedFileName,
        string contentType,
        long sizeBytes,
        string relativePath,
        DateTimeOffset uploadedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "An attachment must have a valid identifier.",
                nameof(id));
        }

        if (sensorId == Guid.Empty)
        {
            throw new ArgumentException(
                "An attachment must identify its sensor.",
                nameof(sensorId));
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "The attachment category is not supported.");
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeBytes),
                sizeBytes,
                "An attachment must contain data.");
        }

        if (uploadedAtUtc == default)
        {
            throw new ArgumentException(
                "An upload timestamp is required.",
                nameof(uploadedAtUtc));
        }

        Id = id;
        SensorId = sensorId;
        Category = category;
        OriginalFileName = NormaliseOriginalFileName(originalFileName);
        StoredFileName = ValidateStoredFileName(storedFileName);
        ContentType = RequireText(
            contentType,
            MaximumContentTypeLength,
            nameof(contentType));
        SizeBytes = sizeBytes;
        RelativePath = ValidateRelativePath(relativePath);
        UploadedAtUtc = uploadedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid SensorId { get; private set; }

    public SensorAttachmentCategory Category { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string StoredFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string RelativePath { get; private set; } = string.Empty;

    public DateTimeOffset UploadedAtUtc { get; private set; }

    private static string NormaliseOriginalFileName(string originalFileName)
    {
        var suppliedName = RequireText(
            originalFileName,
            MaximumFileNameLength,
            nameof(originalFileName));
        var lastSeparatorIndex = Math.Max(
            suppliedName.LastIndexOf('/'),
            suppliedName.LastIndexOf('\\'));
        var fileName = suppliedName[(lastSeparatorIndex + 1)..];

        return RequireText(
            fileName,
            MaximumFileNameLength,
            nameof(originalFileName));
    }

    private static string ValidateStoredFileName(string storedFileName)
    {
        var fileName = RequireText(
            storedFileName,
            MaximumFileNameLength,
            nameof(storedFileName));

        if (fileName.Contains('/') ||
            fileName.Contains('\\') ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "The stored file name must be a safe file name without a path.",
                nameof(storedFileName));
        }

        return fileName;
    }

    private static string ValidateRelativePath(string relativePath)
    {
        var path = RequireText(
            relativePath,
            MaximumRelativePathLength,
            nameof(relativePath));
        var segments = path.Split(
            new[] { '/', '\\' },
            StringSplitOptions.RemoveEmptyEntries);

        var hasWindowsDriveRoot =
            path.Length >= 3 &&
            char.IsLetter(path[0]) &&
            path[1] == ':' &&
            (path[2] == '/' || path[2] == '\\');

        if (Path.IsPathRooted(path) ||
            path.StartsWith('\\') ||
            hasWindowsDriveRoot ||
            segments.Contains(".."))
        {
            throw new ArgumentException(
                "The attachment path must be relative and cannot traverse directories.",
                nameof(relativePath));
        }

        return path;
    }

    private static string RequireText(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A value is required.",
                parameterName);
        }

        var trimmedValue = value.Trim();

        if (trimmedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return trimmedValue;
    }
}
