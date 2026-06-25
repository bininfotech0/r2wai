namespace R2WAI.Application.Features.Chat.DTOs;

public class MessageDto
{
    public Guid Id { get; init; }
    public MessageRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? ContentBlocks { get; init; }
    public MessageStatus Status { get; init; }
    public List<MessageAttachmentDto> Attachments { get; init; } = [];
    public DateTime CreatedAt { get; init; }
}
