using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

public sealed class MessageAttachment : BaseEntity<Guid>
{
    public Guid MessageId { get; private set; }
    public string FileName { get; private set; }
    public string FilePath { get; private set; }
    public string ContentType { get; private set; }
    public long FileSize { get; private set; }
    public Guid? DocumentId { get; private set; }

    public Message Message { get; private set; } = null!;

    private MessageAttachment() { }

    public MessageAttachment(Guid id, Guid messageId, string fileName,
                              string filePath, string contentType,
                              long fileSize, Guid? documentId = null)
    {
        Id = id;
        MessageId = messageId;
        FileName = fileName;
        FilePath = filePath;
        ContentType = contentType;
        FileSize = fileSize;
        DocumentId = documentId;
        CreatedAt = DateTime.UtcNow;
    }
}
