namespace SmartX.Application.Attachments;

public interface IAttachmentFileStorage
{
    Task<StoredAttachmentFile> SaveAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}
