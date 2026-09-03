using SmartX.Application.Attachments;

namespace SmartX.Infrastructure.Attachments;

public sealed class LocalAttachmentFileStorage : IAttachmentFileStorage
{
    private const int MaximumExtensionLength = 10;

    private readonly string _rootPath;
    private readonly string _rootPathPrefix;
    private readonly StringComparison _pathComparison;

    public LocalAttachmentFileStorage(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException(
                "An attachment storage root path is required.",
                nameof(rootPath));
        }

        _rootPath = Path.GetFullPath(rootPath);
        _rootPathPrefix =
            Path.TrimEndingDirectorySeparator(_rootPath) +
            Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredAttachmentFile> SaveAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "The attachment content stream must be readable.",
                nameof(content));
        }

        var extension = NormaliseExtension(fileExtension);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(
                "sensors",
                storedFileName[..2],
                storedFileName)
            .Replace(Path.DirectorySeparatorChar, '/');
        var fullPath = ResolveSafeFullPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath) ??
            throw new InvalidOperationException(
                "The attachment storage directory could not be resolved.");

        Directory.CreateDirectory(directory);

        try
        {
            await using var destination = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await content.CopyToAsync(destination, cancellationToken);
        }
        catch
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            throw;
        }

        return new StoredAttachmentFile(storedFileName, relativePath);
    }

    public Task<Stream> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveSafeFullPath(relativePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The requested attachment file does not exist.",
                fullPath);
        }

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(stream);
    }

    public Task<bool> DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveSafeFullPath(relativePath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    private static string NormaliseExtension(string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            throw new ArgumentException(
                "A file extension is required.",
                nameof(fileExtension));
        }

        var extension = fileExtension.Trim().ToLowerInvariant();

        if (!extension.StartsWith('.') ||
            extension.Length > MaximumExtensionLength ||
            extension.Length == 1 ||
            extension[1..].Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException(
                "The file extension must contain only letters and numbers.",
                nameof(fileExtension));
        }

        return extension;
    }

    private string ResolveSafeFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException(
                "An attachment relative path is required.",
                nameof(relativePath));
        }

        var segments = relativePath.Split(
            ['/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);

        if (Path.IsPathRooted(relativePath) ||
            relativePath.StartsWith('\\') ||
            segments.Contains("..", StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "The attachment path must remain inside its storage root.",
                nameof(relativePath));
        }

        var platformPath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(platformPath, _rootPath);

        if (!fullPath.StartsWith(_rootPathPrefix, _pathComparison))
        {
            throw new ArgumentException(
                "The attachment path must remain inside its storage root.",
                nameof(relativePath));
        }

        return fullPath;
    }
}
