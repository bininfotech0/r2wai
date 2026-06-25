namespace R2WAI.Infrastructure.Storage;

public class LocalStorageService : IStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalStorageService> _logger;

    public LocalStorageService(
        IConfiguration configuration,
        ILogger<LocalStorageService> logger)
    {
        _logger = logger;
        _basePath = configuration["Storage:Local:Path"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
    {
        var relativePath = string.IsNullOrEmpty(folder)
            ? $"{Guid.NewGuid():N}_{fileName}"
            : $"{folder}/{Guid.NewGuid():N}_{fileName}";

        var fullPath = Path.Combine(_basePath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await using var fileStream2 = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await fileStream.CopyToAsync(fileStream2, ct);

        _logger.LogInformation("File saved locally: {Path}", relativePath);
        return relativePath;
    }

    public async Task<Stream> DownloadFileAsync(string path, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {path}", fullPath);

        var memoryStream = new MemoryStream();
        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        await fileStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public Task DeleteFileAsync(string path, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: {Path}", path);
        }
        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string path, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, path);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<string> GetFileUrlAsync(string path, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, path);
        return Task.FromResult(fullPath);
    }
}
