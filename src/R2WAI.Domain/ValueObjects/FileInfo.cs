using R2WAI.Domain.Common;

namespace R2WAI.Domain.ValueObjects;

public sealed class FileInfo : ValueObject
{
    public string FileName { get; }
    public string ContentType { get; }
    public long FileSize { get; }
    public string Path { get; }

    public FileInfo(string fileName, string contentType, long fileSize, string path)
    {
        FileName = fileName;
        ContentType = contentType;
        FileSize = fileSize;
        Path = path;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FileName;
        yield return ContentType;
        yield return FileSize;
        yield return Path;
    }
}
