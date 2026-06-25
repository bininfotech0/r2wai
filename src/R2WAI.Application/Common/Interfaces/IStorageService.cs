namespace R2WAI.Application.Common.Interfaces;

public interface IStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string? folder = null, CancellationToken ct = default);
    Task<Stream> DownloadFileAsync(string path, CancellationToken ct = default);
    Task DeleteFileAsync(string path, CancellationToken ct = default);
    Task<bool> FileExistsAsync(string path, CancellationToken ct = default);
    Task<string> GetFileUrlAsync(string path, CancellationToken ct = default);
}
