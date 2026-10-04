namespace R2WAI.Application.Common.Interfaces;

public enum ToolCallProgressKind
{
    Started,
    Completed
}

/// <summary>
/// Display-name-only progress event for the in-chat "Checking {Capability}..." UX (Phase 2 of the
/// redesign plan) — never carries the resolver class, endpoint, arguments, or SQL, per the brief's
/// explicit "do not expose" list.
/// </summary>
public sealed record ToolCallProgressEvent(string ToolName, ToolCallProgressKind Kind, bool? Success = null);

/// <summary>
/// Scoped, per-request ambient hook from AiFunctionAuditFilter (deep inside the Semantic Kernel
/// function-invocation pipeline) to whichever chat entry point is awaiting the response stream, so it
/// can forward tool-call-started/completed events to its own transport (SignalR group broadcast or an
/// SSE write) in real time. Safe to call synchronously with no concurrency concerns: SK's auto
/// function-calling invokes filters fully nested inside the same async call the caller is already
/// awaiting (chatCompletion.GetStreamingChatMessageContentsAsync resolves every function call before
/// yielding any content), so OnProgress always runs on the same logical call stack as the caller's own
/// stream loop — never on a separate thread that could race a shared response writer.
/// </summary>
public interface IChatStreamContext
{
    Func<ToolCallProgressEvent, Task>? OnProgress { get; set; }

    // Response cards (Phase 3): a tool whose result is already small and structured (not free text)
    // can stash a JSON array of card objects here (see ResponseCardDto) while it runs, so the chat
    // entry point can attach it to the final message alongside the model's own prose narration —
    // instead of relying only on the model to describe the data in words. Always a JSON array (even
    // for one card), matching Message.ContentBlocks' plural shape. Scoped deliberately to a small set
    // of tools where the shape is genuinely unambiguous (see WorkflowPlugin), not every tool.
    // Last-write-wins if more than one card-producing tool call happens in a turn — good enough for
    // the single-card-per-turn case this ships with; not meant to model a multi-card timeline.
    string? CapturedContentBlock { get; set; }
}
