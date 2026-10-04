using R2WAI.Application.Features.Workflows.DTOs;

namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Tenant-editable overrides of the hardcoded starter workflow templates (Prompt Management's
/// sibling for workflows — see IPromptTemplateService). A tenant with no override for a given
/// template id sees the static default unchanged.
/// </summary>
public interface IWorkflowTemplateService
{
    Task<IReadOnlyList<WorkflowTemplateDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Upserts this tenant's override for templateId. Unlike PromptTemplate, this replaces
    /// in place rather than versioning — nothing reads prior workflow-template edit history today.
    /// Throws NotFoundException if templateId doesn't match any known default template.</summary>
    Task<WorkflowTemplateDto> SetTemplateAsync(string templateId, Guid tenantId, string name,
        string? description, string type, List<WorkflowTemplateStepDto> steps, CancellationToken ct = default);
}
