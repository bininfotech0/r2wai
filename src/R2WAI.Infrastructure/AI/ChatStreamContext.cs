using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI;

public sealed class ChatStreamContext : IChatStreamContext
{
    public Func<ToolCallProgressEvent, Task>? OnProgress { get; set; }
    public string? CapturedContentBlock { get; set; }
}
