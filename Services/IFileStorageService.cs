namespace SecureFilesMvc.Web.Services;

public record StoredFile(string Id, string OriginalName, string StoredPath, long Size, string ContentType);

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(Stream content, string originalName, string contentType, CancellationToken ct);
    Task<(Stream stream, StoredFile meta)?> OpenAsync(string id, CancellationToken ct);
    Task<bool> ExistsAsync(string id, CancellationToken ct);
}
