using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Infrastructure.AI.Prompts;

public class PromptTemplateService : IPromptTemplateService
{
    private readonly IRepository<PromptTemplate> _templates;
    private readonly IUnitOfWork _unitOfWork;

    public PromptTemplateService(IRepository<PromptTemplate> templates, IUnitOfWork unitOfWork)
    {
        _templates = templates;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> GetActiveTemplateAsync(AssistantType type, Guid tenantId, CancellationToken ct = default)
    {
        var active = await GetActiveEntityAsync(type, tenantId, ct);
        return active?.Content ?? SystemPromptTemplates.GetTemplate(type);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllActiveTemplatesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(SystemPromptTemplates.GetAll());

        var overrides = await _templates.FindAsync(t => t.TenantId == tenantId && t.IsActive, ct);
        foreach (var over in overrides)
            result[over.AssistantType.ToString()] = over.Content;

        return result;
    }

    public async Task<string> SetTemplateAsync(AssistantType type, Guid tenantId, string content, CancellationToken ct = default)
    {
        var newVersion = string.Empty;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var current = await GetActiveEntityAsync(type, tenantId, ct);
            var nextVersion = (current?.Version ?? 0) + 1;

            current?.Supersede();

            var template = new PromptTemplate(Guid.NewGuid(), tenantId, type, content, nextVersion);
            await _templates.AddAsync(template, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            newVersion = template.Content;
        }, ct);

        return newVersion;
    }

    public async Task<bool> ResetTemplateAsync(AssistantType type, Guid tenantId, CancellationToken ct = default)
    {
        var current = await GetActiveEntityAsync(type, tenantId, ct);
        if (current is null)
            return false;

        current.Supersede();
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private Task<PromptTemplate?> GetActiveEntityAsync(AssistantType type, Guid tenantId, CancellationToken ct) =>
        _templates.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.AssistantType == type && t.IsActive, ct);
}
