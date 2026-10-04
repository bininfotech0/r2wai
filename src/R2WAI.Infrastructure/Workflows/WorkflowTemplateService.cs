using System.Text.Json;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Workflows.DTOs;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.Workflows;

public class WorkflowTemplateService : IWorkflowTemplateService
{
    private readonly IRepository<WorkflowTemplateOverride> _overrides;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowTemplateService(IRepository<WorkflowTemplateOverride> overrides, IUnitOfWork unitOfWork)
    {
        _overrides = overrides;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<WorkflowTemplateDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        var defaults = WorkflowTemplateDefaults.GetAll();
        var overrides = await _overrides.FindAsync(o => o.TenantId == tenantId, ct);
        var overridesById = overrides.ToDictionary(o => o.TemplateId);

        return defaults
            .Select(d => overridesById.TryGetValue(d.Id, out var over) ? ToDto(over) : d)
            .ToList();
    }

    public async Task<WorkflowTemplateDto> SetTemplateAsync(string templateId, Guid tenantId, string name,
        string? description, string type, List<WorkflowTemplateStepDto> steps, CancellationToken ct = default)
    {
        if (!WorkflowTemplateDefaults.GetAll().Any(d => d.Id == templateId))
            throw new NotFoundException(nameof(WorkflowTemplateOverride), templateId);

        var stepsJson = JsonSerializer.Serialize(steps);
        var existing = await _overrides.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.TemplateId == templateId, ct);

        if (existing is not null)
        {
            existing.Update(name, description, type, stepsJson);
        }
        else
        {
            existing = new WorkflowTemplateOverride(Guid.NewGuid(), tenantId, templateId, name, description, type, stepsJson);
            await _overrides.AddAsync(existing, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    private static WorkflowTemplateDto ToDto(WorkflowTemplateOverride entity) => new(
        entity.TemplateId, entity.Name, entity.Description, entity.Type,
        JsonSerializer.Deserialize<List<WorkflowTemplateStepDto>>(entity.StepsJson) ?? []);
}
