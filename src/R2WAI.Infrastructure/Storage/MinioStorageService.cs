using Minio;
using Minio.DataModel.Args;

namespace R2WAI.Infrastructure.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(
        IConfiguration configuration,
        ILogger<MinioStorageService> logger)
    {
        _logger = logger;

        var endpoint = configuration["Storage:Minio:Endpoint"] ?? "localhost:9000";
        var accessKey = configuration["Storage:Minio:AccessKey"] ?? "minioadmin";
        var secretKey = configuration["Storage:Minio:SecretKey"] ?? "minioadmin";
        _bucketName = configuration["Storage:Minio:Bucket"] ?? "r2wai-documents";
        var useSsl = bool.Parse(configuration["Storage:Minio:UseSsl"] ?? "false");

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
    {
        try
        {
            var beArgs = new BucketExistsArgs().WithBucket(_bucketName);
            var found = await _minioClient.BucketExistsAsync(beArgs, ct);
            if (!found)
            {
                var mbArgs = new MakeBucketArgs().WithBucket(_bucketName);
                await _minioClient.MakeBucketAsync(mbArgs, ct);
            }

            var objectName = string.IsNullOrEmpty(folder)
                ? $"{Guid.NewGuid():N}_{fileName}"
                : $"{folder}/{Guid.NewGuid():N}_{fileName}";

            var putArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putArgs, ct);

            _logger.LogInformation("File uploaded: {ObjectName} to bucket {Bucket}", objectName, _bucketName);
            return objectName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName}", fileName);
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var memoryStream = new MemoryStream();
            var getArgs = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(path)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getArgs, ct);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file {Path}", path);
            throw;
        }
    }

    public async Task DeleteFileAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var rmArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(path);

            await _minioClient.RemoveObjectAsync(rmArgs, ct);
            _logger.LogInformation("File deleted: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {Path}", path);
        }
    }

    public async Task<bool> FileExistsAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(path);

            await _minioClient.StatObjectAsync(statArgs, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetFileUrlAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var presignedArgs = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(path)
                .WithExpiry(3600);

            var url = await _minioClient.PresignedGetObjectAsync(presignedArgs);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file URL for {Path}", path);
            return path;
        }
    }
}
