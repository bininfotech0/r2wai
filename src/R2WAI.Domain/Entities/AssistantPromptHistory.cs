using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A versioned history entry of one <see cref="AssistantDefinition"/> instance's live SystemPrompt.
/// Mirrors <see cref="PromptTemplate"/>'s Supersede-on-edit shape exactly, scoped per assistant
/// instance instead of per AssistantType — AssistantDefinition.SystemPrompt itself stays the plain
/// mutable "current value" field (still what ChatWithAssistantCommand actually reads at chat time);
/// this table is purely the append-only history alongside it. Recording is automatic on every real
/// edit (see UpdateAssistantCommandHandler), not a separate explicit "create version" action —
/// deliberately matching PromptTemplateService.SetTemplateAsync's behavior, not Track B Phase 4a's
/// ToolDefinitionVersion/KnowledgeBaseVersion (which snapshot on an explicit user action instead).
/// </summary>
public sealed class AssistantPromptHistory : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid AssistantDefinitionId { get; private set; }
    public string Content { get; private set; }
    public int Version { get; private set; }
    public bool IsActive { get; private set; }

    public AssistantDefinition AssistantDefinition { get; private set; } = null!;

    private AssistantPromptHistory() { }

    public AssistantPromptHistory(Guid id, Guid tenantId, Guid assistantDefinitionId, string content, int version)
    {
        Id = id;
        TenantId = tenantId;
        AssistantDefinitionId = assistantDefinitionId;
        Content = content;
        Version = version;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Superseded by a newer edit — kept for history, no longer "the" active entry.</summary>
    public void Supersede()
    {
        IsActive = false;
        MarkAsModified();
    }
}
