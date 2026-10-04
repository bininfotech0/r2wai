using R2WAI.Domain.Enums;

namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Tenant-editable, versioned overrides of the per-AssistantType base system prompt (Prompt
/// Management). Falls back to the hardcoded SystemPromptTemplates defaults when a tenant hasn't
/// configured an override for a type — so a fresh tenant with zero PromptTemplate rows behaves
/// exactly as before this existed.
/// </summary>
public interface IPromptTemplateService
{
    Task<string> GetActiveTemplateAsync(AssistantType type, Guid tenantId, CancellationToken ct = default);

    /// <summary>Keyed the same way as SystemPromptTemplates.GetAll() (by AssistantType name).</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllActiveTemplatesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Supersedes any existing active version for this type and creates a new one.</summary>
    Task<string> SetTemplateAsync(AssistantType type, Guid tenantId, string content, CancellationToken ct = default);

    /// <summary>
    /// Removes this tenant's override for the type, falling back to SystemPromptTemplates' default —
    /// not a delete, the superseded row is kept for history same as SetTemplateAsync's own edits.
    /// A no-op (returns false) when the tenant has no active override for this type.
    /// </summary>
    Task<bool> ResetTemplateAsync(AssistantType type, Guid tenantId, CancellationToken ct = default);
}
