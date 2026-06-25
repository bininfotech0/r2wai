using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace R2WAI.Infrastructure.Services;

public class WorkflowService : IWorkflowService
{
    private readonly ApplicationDbContext _context;
    private readonly IAIService _aiService;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        ApplicationDbContext context,
        IAIService aiService,
        ILogger<WorkflowService> logger)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<WorkflowDto> CreateWorkflowAsync(Guid tenantId, Guid userId, string name, string? description, string? type, CancellationToken ct = default)
    {
        var workflow = new Workflow(Guid.NewGuid(), tenantId, userId, name, description, type);
        workflow.Activate();
        await _context.Workflows.AddAsync(workflow, ct);
        await _context.SaveChangesAsync(ct);

        return MapToDto(workflow);
    }

    public async Task<WorkflowDto> UpdateWorkflowAsync(Guid id, string name, string? description, string? type, string? trigger, string? steps, CancellationToken ct = default)
    {
        var workflow = await _context.Workflows
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (workflow is null)
            throw new NotFoundException(nameof(Workflow), id);

        workflow.UpdateDetails(name, description, type, trigger, steps);
        await _context.SaveChangesAsync(ct);

        return MapToDto(workflow);
    }

    public async Task DeleteWorkflowAsync(Guid id, CancellationToken ct = default)
    {
        var workflow = await _context.Workflows
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (workflow is null)
            throw new NotFoundException(nameof(Workflow), id);

        workflow.SoftDelete();
        await _context.SaveChangesAsync(ct);
    }

    public async Task<WorkflowInstanceDto> ExecuteWorkflowAsync(Guid workflowId, Guid initiatedBy, string? data, CancellationToken ct = default)
    {
        var workflow = await _context.Workflows
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct);

        if (workflow is null)
            throw new NotFoundException(nameof(Workflow), workflowId);

        var instance = new WorkflowInstance(
            Guid.NewGuid(), workflowId, workflow.TenantId, initiatedBy, data);

        _context.WorkflowInstances.Add(instance);
        await _context.SaveChangesAsync(ct);

        var steps = DeserializeSteps(workflow.Steps);
        if (steps.Count > 0)
        {
            var currentStepInfo = steps[0];
            var prompt = $"Execute workflow step '{currentStepInfo.Name}' for workflow '{workflow.Name}'. Data: {data}";
            await _aiService.GenerateResponseAsync(prompt, "You are a workflow execution engine.", null, ct);
        }

        return MapToInstanceDto(instance, workflow.Name, steps);
    }

    public async Task<PagedResult<WorkflowDto>> GetWorkflowsAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Workflows.Where(w => w.TenantId == tenantId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => MapToDto(w))
            .ToListAsync(ct);

        return new PagedResult<WorkflowDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<WorkflowDto> GetWorkflowByIdAsync(Guid id, CancellationToken ct = default)
    {
        var workflow = await _context.Workflows
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (workflow is null)
            throw new NotFoundException(nameof(Workflow), id);

        return MapToDto(workflow);
    }

    public async Task<PagedResult<WorkflowInstanceDto>> GetWorkflowInstancesAsync(Guid tenantId, Guid? workflowId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.WorkflowInstances
            .Include(i => i.Workflow)
            .Where(i => i.TenantId == tenantId);

        if (workflowId.HasValue)
            query = query.Where(i => i.WorkflowId == workflowId.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => MapToInstanceDto(i, i.Workflow.Name, new List<WorkflowStepDto>()))
            .ToListAsync(ct);

        return new PagedResult<WorkflowInstanceDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<WorkflowInstanceDto> GetWorkflowInstanceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var instance = await _context.WorkflowInstances
            .Include(i => i.Workflow)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (instance is null)
            throw new NotFoundException(nameof(WorkflowInstance), id);

        var steps = DeserializeSteps(instance.Workflow.Steps);
        return MapToInstanceDto(instance, instance.Workflow.Name, steps);
    }

    private static WorkflowDto MapToDto(Workflow workflow) => new()
    {
        Id = workflow.Id,
        Name = workflow.Name,
        Description = workflow.Description,
        Type = workflow.Type,
        Trigger = workflow.Trigger,
        Steps = workflow.Steps,
        IsActive = workflow.IsActive,
        CreatedAt = workflow.CreatedAt
    };

    private static WorkflowInstanceDto MapToInstanceDto(WorkflowInstance instance, string workflowName, List<WorkflowStepDto> steps) => new()
    {
        Id = instance.Id,
        WorkflowId = instance.WorkflowId,
        WorkflowName = workflowName,
        Status = instance.Status,
        CurrentStep = instance.CurrentStep,
        Data = instance.Data,
        StartedAt = instance.StartedAt,
        CompletedAt = instance.CompletedAt,
        CreatedAt = instance.CreatedAt,
        Steps = steps
    };

    private static List<WorkflowStepDto> DeserializeSteps(string? steps)
    {
        if (string.IsNullOrEmpty(steps)) return [];
        try { return JsonSerializer.Deserialize<List<WorkflowStepDto>>(steps) ?? []; }
        catch { return []; }
    }
}
