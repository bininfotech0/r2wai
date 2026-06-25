using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Api.Services;
using R2WAI.Application.Features.Workflows.Commands;
using R2WAI.Application.Features.Workflows.Queries;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize(Policy = "CanManageWorkflows")]
[Route("api/v1/[controller]")]
public class WorkflowsController(
    IMediator mediator,
    IWorkflowBridge workflowBridge,
    ApplicationDbContext dbContext,
    IConfiguration configuration,
    ILogger<WorkflowsController> logger) : ControllerBase
{
    private Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (claim is null || !Guid.TryParse(claim.Value, out var id))
                throw new UnauthorizedAccessException("Invalid user identity");
            return id;
        }
    }

    private Guid CurrentTenantId
    {
        get
        {
            var claim = User.FindFirst("tenant_id");
            if (claim is not null && Guid.TryParse(claim.Value, out var tid))
                return tid;
            throw new UnauthorizedAccessException("Tenant context not found");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating workflow: {Name}", command.Name);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var query = new GetWorkflowsQuery { Page = page, PageSize = pageSize, Search = search };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetWorkflowByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteWorkflowCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Execute(Guid id, [FromBody] ExecuteWorkflowCommand command, CancellationToken ct = default)
    {
        command = command with { WorkflowId = id };
        var result = await mediator.Send(command, ct);

        try
        {
            var (elsaInstanceId, _) = await workflowBridge.StartWorkflowAsync(
                id, CurrentTenantId, CurrentUserId, command.Data, ct);

            var workflowInstance = await dbContext.WorkflowInstances
                .FirstOrDefaultAsync(wi => wi.Id == result.Id, ct);

            if (workflowInstance is not null)
            {
                workflowInstance.SetElsaInstanceId(elsaInstanceId);
                await dbContext.SaveChangesAsync(ct);
            }

            return Accepted(new { instance = result, elsaInstanceId, status = "running" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Elsa workflow for {WorkflowId}, instance {InstanceId}", id, result.Id);
            return Accepted(new { instance = result, status = "degraded", warning = $"Workflow instance created but execution engine unavailable: {ex.Message}" });
        }
    }

    [HttpGet("instances")]
    public async Task<IActionResult> GetInstances(
        [FromQuery] Guid? workflowId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetWorkflowInstancesQuery { WorkflowId = workflowId, Page = page, PageSize = pageSize };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("instances/{instanceId:guid}")]
    public async Task<IActionResult> GetInstanceById(Guid instanceId, CancellationToken ct = default)
    {
        var query = new GetWorkflowInstanceByIdQuery { Id = instanceId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct = default)
    {
        var workflow = await dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workflow is null) return NotFound();
        workflow.Publish();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Workflow {Id} published (v{Version})", id, workflow.Version);
        return Ok(new { id, version = workflow.Version, versionStatus = workflow.VersionStatus });
    }

    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct = default)
    {
        var workflow = await dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workflow is null) return NotFound();
        workflow.Unpublish();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Workflow {Id} unpublished", id);
        return Ok(new { id, version = workflow.Version, versionStatus = workflow.VersionStatus });
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct = default)
    {
        var workflow = await dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workflow is null) return NotFound();
        workflow.Archive();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Workflow {Id} archived", id);
        return Ok(new { id, version = workflow.Version, versionStatus = workflow.VersionStatus });
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct = default)
    {
        var workflow = await dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workflow is null) return NotFound();
        workflow.Restore();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Workflow {Id} restored from archive", id);
        return Ok(new { id, version = workflow.Version, versionStatus = workflow.VersionStatus });
    }

    [HttpPost("{id:guid}/new-version")]
    public async Task<IActionResult> NewVersion(Guid id, CancellationToken ct = default)
    {
        var workflow = await dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workflow is null) return NotFound();
        workflow.NewVersion();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Workflow {Id} new version created (v{Version})", id, workflow.Version);
        return Ok(new { id, version = workflow.Version, versionStatus = workflow.VersionStatus });
    }

    [HttpPost("{id:guid}/schedule")]
    public async Task<IActionResult> Schedule(Guid id, [FromBody] ScheduleWorkflowRequest request, CancellationToken ct = default)
    {
        var workflow = await dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workflow is null) return NotFound();

        workflow.UpdateDetails(workflow.Name, workflow.Description, workflow.Type,
            request.CronExpression, workflow.Steps);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Workflow {Id} scheduled with cron: {Cron}", id, request.CronExpression);
        return Ok(new { id, cronExpression = request.CronExpression, message = "Workflow scheduled" });
    }

    public record ScheduleWorkflowRequest(string CronExpression);

    [HttpPost("webhook/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookTrigger(
        string slug,
        [FromBody] object? payload,
        [FromHeader(Name = "X-Webhook-Secret")] string? webhookSecret = null,
        [FromHeader(Name = "X-Webhook-Signature")] string? webhookSignature = null,
        [FromHeader(Name = "X-Webhook-Timestamp")] string? webhookTimestamp = null,
        CancellationToken ct = default)
    {
        var configuredSecret = configuration["Webhooks:Secret"];
        if (!string.IsNullOrEmpty(configuredSecret))
        {
            if (!string.IsNullOrEmpty(webhookSignature) && !string.IsNullOrEmpty(webhookTimestamp))
            {
                if (!long.TryParse(webhookTimestamp, out var ts)
                    || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300)
                {
                    logger.LogWarning("Webhook rejected: timestamp too old or invalid for slug {Slug}", slug);
                    return Unauthorized(new { error = "Webhook timestamp expired or invalid" });
                }

                var body = payload is not null ? System.Text.Json.JsonSerializer.Serialize(payload) : "";
                var signPayload = $"{webhookTimestamp}.{body}";
                using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(configuredSecret));
                var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(signPayload));
                var expected = Convert.ToHexStringLower(hash);

                if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(expected),
                    System.Text.Encoding.UTF8.GetBytes(webhookSignature)))
                {
                    logger.LogWarning("Webhook rejected: HMAC signature mismatch for slug {Slug}", slug);
                    return Unauthorized(new { error = "Invalid webhook signature" });
                }
            }
            else if (string.IsNullOrEmpty(webhookSecret) || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(configuredSecret),
                System.Text.Encoding.UTF8.GetBytes(webhookSecret)))
            {
                logger.LogWarning("Webhook rejected: invalid or missing secret for slug {Slug}", slug);
                return Unauthorized(new { error = "Invalid or missing webhook secret" });
            }
        }

        var workflow = await dbContext.Workflows
            .FirstOrDefaultAsync(w => w.Trigger == slug && w.IsActive, ct);

        if (workflow is null)
            return NotFound(new { error = $"No active workflow with trigger '{slug}'" });

        var data = payload is not null ? System.Text.Json.JsonSerializer.Serialize(payload) : null;

        try
        {
            var defaultTenantId = workflow.TenantId;
            var (elsaInstanceId, instanceId) = await workflowBridge.StartWorkflowAsync(
                workflow.Id, defaultTenantId, workflow.UserId, data, ct);

            logger.LogInformation("Webhook triggered workflow {WorkflowId} → instance {InstanceId}", workflow.Id, instanceId);
            return Accepted(new { workflowId = workflow.Id, instanceId, elsaInstanceId, status = "running" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook trigger failed for workflow {WorkflowId}", workflow.Id);
            return StatusCode(500, new { error = "Workflow execution failed" });
        }
    }

    [HttpGet("templates")]
    public IActionResult GetTemplates()
    {
        var templates = new[]
        {
            new
            {
                Id = "invoice-approval",
                Name = "Invoice Approval",
                Description = "Three-level invoice approval workflow with amount-based routing",
                Type = "Approval",
                Steps = new[]
                {
                    new { Name = "Submit Invoice", Action = "Action", AssignedRole = "Submitter", Order = 0 },
                    new { Name = "Manager Approval", Action = "Approval", AssignedRole = "Manager", Order = 1 },
                    new { Name = "Finance Review", Action = "Approval", AssignedRole = "Finance", Order = 2 },
                    new { Name = "Process Payment", Action = "API Call", AssignedRole = "System", Order = 3 },
                    new { Name = "Send Confirmation", Action = "Email", AssignedRole = "System", Order = 4 }
                }
            },
            new
            {
                Id = "purchase-request",
                Name = "Purchase Request",
                Description = "Purchase order request with budget check and approval",
                Type = "Approval",
                Steps = new[]
                {
                    new { Name = "Submit Request", Action = "Action", AssignedRole = "Requester", Order = 0 },
                    new { Name = "Budget Check", Action = "AI Generate", AssignedRole = "System", Order = 1 },
                    new { Name = "Manager Approval", Action = "Approval", AssignedRole = "Manager", Order = 2 },
                    new { Name = "Procurement Review", Action = "Approval", AssignedRole = "Procurement", Order = 3 },
                    new { Name = "Create PO", Action = "API Call", AssignedRole = "System", Order = 4 }
                }
            },
            new
            {
                Id = "employee-onboarding",
                Name = "Employee Onboarding",
                Description = "New employee onboarding workflow with IT and HR tasks",
                Type = "Process",
                Steps = new[]
                {
                    new { Name = "HR Intake", Action = "Action", AssignedRole = "HR", Order = 0 },
                    new { Name = "Generate Welcome Pack", Action = "AI Generate", AssignedRole = "System", Order = 1 },
                    new { Name = "IT Setup Request", Action = "API Call", AssignedRole = "IT", Order = 2 },
                    new { Name = "Manager Introduction", Action = "Email", AssignedRole = "System", Order = 3 },
                    new { Name = "HR Approval", Action = "Approval", AssignedRole = "HR", Order = 4 }
                }
            },
            new
            {
                Id = "travel-request",
                Name = "Travel Request",
                Description = "Travel approval with policy check and booking",
                Type = "Approval",
                Steps = new[]
                {
                    new { Name = "Submit Travel Request", Action = "Action", AssignedRole = "Employee", Order = 0 },
                    new { Name = "Policy Check", Action = "AI Generate", AssignedRole = "System", Order = 1 },
                    new { Name = "Manager Approval", Action = "Approval", AssignedRole = "Manager", Order = 2 },
                    new { Name = "Book Travel", Action = "API Call", AssignedRole = "System", Order = 3 }
                }
            },
            new
            {
                Id = "vendor-approval",
                Name = "Vendor Approval",
                Description = "New vendor onboarding with compliance and legal review",
                Type = "Approval",
                Steps = new[]
                {
                    new { Name = "Submit Vendor Info", Action = "Action", AssignedRole = "Procurement", Order = 0 },
                    new { Name = "Compliance Check", Action = "AI Generate", AssignedRole = "System", Order = 1 },
                    new { Name = "Legal Review", Action = "Approval", AssignedRole = "Legal", Order = 2 },
                    new { Name = "Finance Approval", Action = "Approval", AssignedRole = "Finance", Order = 3 },
                    new { Name = "Register Vendor", Action = "API Call", AssignedRole = "System", Order = 4 }
                }
            }
        };

        return Ok(new { items = templates });
    }

    [HttpPost("instances/{instanceId:guid}/retry")]
    public async Task<IActionResult> RetryFailedStep(Guid instanceId, CancellationToken ct = default)
    {
        var success = await workflowBridge.RetryFailedStepAsync(instanceId, ct);
        if (success)
            return Ok(new { message = "Failed step retried successfully" });
        return BadRequest(new { error = "No failed step to retry, or retry failed" });
    }

    [HttpGet("instances/{instanceId:guid}/steps")]
    public async Task<IActionResult> GetInstanceSteps(Guid instanceId, CancellationToken ct = default)
    {
        var steps = await dbContext.WorkflowStepExecutions
            .Where(s => s.WorkflowInstanceId == instanceId)
            .OrderBy(s => s.StepIndex)
            .Select(s => new
            {
                s.Id,
                s.StepIndex,
                s.StepName,
                s.StepType,
                Status = s.Status.ToString(),
                s.StartedAt,
                s.CompletedAt,
                s.Output,
                s.Error
            })
            .ToListAsync(ct);

        return Ok(new { items = steps });
    }
}
