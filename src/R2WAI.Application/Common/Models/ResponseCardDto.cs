namespace R2WAI.Application.Common.Models;

/// <summary>
/// A structured chat response card (redesign plan Phase 3) — the shape a tool's result renders as
/// instead of only being narrated in prose. Serialized as JSON into Message.ContentBlocks (persisted
/// path) or sent as a standalone SSE event (ephemeral test-chat path); the client's ResponseCard.tsx
/// is the single renderer for all four Type values. Only "Status" and "Table" have a real producer as
/// of this phase (WorkflowPlugin) — "Summary"/"Kpi" are defined so the client is ready for the next
/// tool that needs them, not fabricated ahead of a real source.
/// </summary>
public sealed record ResponseCardDto(
    string Type,
    string Title,
    IReadOnlyList<ResponseCardField>? Fields = null,
    IReadOnlyList<string>? Columns = null,
    IReadOnlyList<IReadOnlyList<string>>? Rows = null);

public sealed record ResponseCardField(string Label, string Value);
