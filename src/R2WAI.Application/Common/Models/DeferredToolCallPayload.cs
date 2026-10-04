using System.Text.Json;

namespace R2WAI.Application.Common.Models;

/// <summary>
/// Durable snapshot of an AI-initiated tool call that was paused for human approval instead of
/// executing immediately (AiFunctionAuditFilter's DenyApprovalRequired path), stored as JSON in
/// ApprovalRequest.Data — a jsonb column that sat unused for standalone (non-workflow) requests
/// until this. Only ever created for a tool DynamicToolExecutor can actually re-invoke later with
/// nothing more than this payload (see DynamicToolExecutor.IsExecutable) — a built-in plugin method
/// has no standalone re-invocation path, so it is never wrapped in one of these; it still denies
/// outright, same as before.
/// </summary>
public sealed record DeferredToolCallPayload(Guid ToolDefinitionId, string? Input)
{
    // Discriminator so a workflow-bound approval's Data (or a hand-edited/garbage value) can never be
    // misread as "a tool call to execute" — TryParse only succeeds when this exact marker is present.
    private const string PayloadKind = "deferred_tool_call";

    private sealed record Envelope(string? Kind, Guid ToolDefinitionId, string? Input);

    public string ToJson() =>
        JsonSerializer.Serialize(new Envelope(PayloadKind, ToolDefinitionId, Input));

    public static DeferredToolCallPayload? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(json);
            return envelope is { Kind: PayloadKind }
                ? new DeferredToolCallPayload(envelope.ToolDefinitionId, envelope.Input)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
