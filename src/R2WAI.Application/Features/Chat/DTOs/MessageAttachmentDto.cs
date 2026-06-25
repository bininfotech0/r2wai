namespace R2WAI.Application.Features.Chat.DTOs;

public class MessageAttachmentDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string? TempFilePath { get; set; }
}
