using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Api.Services;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Workflows.Commands;
using R2WAI.Application.Features.Workflows.DTOs;
using R2WAI.Application.Features.Workflows.Queries;
using R2WAI.Domain.Entities;
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
    IAIService aiService,
    IModelConfigurationResolver modelConfigResolver,
    IWorkflowTemplateService workflowTemplateService,
    ICurrentUserService currentUser,
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

    public record DraftWorkflowRequest(string Description);

    // Bounded natural-language "what do you want to automate?" step for the New Automation wizard.
    // Deliberately uses IAIService.GenerateResponseAsync (tools disabled), not IAgentRuntime: this
    // call only needs to extract structured JSON from a fixed vocabulary, and FunctionChoiceBehavior
    // .Auto() (what IAgentRuntime always enables) would let the model choose to call a tool — e.g.
    // start_workflow — instead of returning the draft, which is not a risk worth taking for a
    // suggestion the admin still has to review and click through before anything is created.
    private static readonly string[] KnownTriggers =
        ["Application Submitted", "Application Updated", "Payment Received", "Form Submitted", "Schedule", "Webhook"];
    private static readonly string[] KnownActions =
        ["Verify Documents", "Get Application Details", "Assign Officer", "Send Notification", "Update Application Status", "Create Task"];

    [HttpPost("draft")]
    public async Task<IActionResult> DraftFromDescription([FromBody] DraftWorkflowRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "Description is required" });

        try
        {
            var prompt =
                "You are helping a non-technical user configure a business process automation. " +
                "Based on their description below, pick the single best-matching trigger and the ordered " +
                "list of actions from the fixed options given, and any simple conditions implied. " +
                "Return ONLY a valid JSON object — no markdown, no explanation.\n\n" +
                $"Description: {request.Description}\n\n" +
                $"Available triggers (pick exactly one, or null if none fit): {string.Join(", ", KnownTriggers)}\n" +
                $"Available actions (pick zero or more, in the order they should run): {string.Join(", ", KnownActions)}\n\n" +
                "JSON format:\n" +
                "{\n" +
                "  \"name\": \"<short automation name, 2-6 words>\",\n" +
                "  \"trigger\": \"<one of the triggers above, or null>\",\n" +
                "  \"actions\": [\"<action label>\", ...],\n" +
                "  \"conditions\": [{\"field\": \"<field name>\", \"operator\": \"<Is equal to|Is not equal to|Contains|Greater than|Less than>\", \"value\": \"<value>\"}]\n" +
                "}";

            var modelConfig = await modelConfigResolver.ResolveAsync(null, CurrentTenantId, ct);
            var raw = await aiService.GenerateResponseAsync(prompt, modelConfig: modelConfig, ct: ct);
            var draft = WorkflowDraftParser.Parse(raw, KnownTriggers, KnownActions);
            return Ok(draft);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Automation draft generation failed for description: {Description}", request.Description);
            // Was `Ok(new WorkflowDraft(null, null, [], []))` — the client didn't treat an empty
            // draft as a failure, so it silently created a near-blank real workflow (empty steps,
            // a truncated-description fallback name) and told the user "Automation drafted". A
            // real error status lets the client's existing catch block do its actual job.
            return StatusCode(502, new { error = "Automation drafting failed. Try a template or build manually." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? applicationId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new GetWorkflowsQuery { Page = page, PageSize = pageSize, Search = search, ApplicationId = applicationId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/application")]
    public async Task<IActionResult> AssignApplication(Guid id, [FromBody] AssignApplicationRequest request, CancellationToken ct = default)
    {
        var command = new AssignWorkflowApplicationCommand { Id = id, ApplicationId = request.ApplicationId };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    public record AssignApplicationRequest(Guid? ApplicationId);

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

    public record BulkDeleteRequest(Guid[] Ids);

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request, CancellationToken ct = default)
    {
        await mediator.Send(new BulkDeleteWorkflowsCommand { Ids = request.Ids }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Execute(Guid id, [FromBody] ExecuteWorkflowCommand command, CancellationToken ct = default)
    {
        command = command with { WorkflowId = id };
        var result = await mediator.Send(command, ct);

        try
        {
            var (elsaInstanceId, instanceId) = await workflowBridge.StartWorkflowAsync(
                id, CurrentTenantId, CurrentUserId, command.Data, ct, existingInstanceId: result.Id);

            return Accepted(new { instanceId, instance = result, elsaInstanceId, status = "running" });
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
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
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

        var versionRow = await GetOrCreateVersionSnapshotAsync(workflow, ct);
        foreach (var published in await dbContext.WorkflowVersions
            .Where(v => v.WorkflowId == id && v.IsPublished && v.Id != versionRow.Id).ToListAsync(ct))
            published.Unpublish();
        versionRow.Publish(CurrentUserId);

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Workflow {Id} published (v{Version})", id, workflow.Version);
        return Ok(new { id, version = workflow.Version, versionStatus = workflow.VersionStatus });
    }

    // Snapshots the workflow's current content under its current Version number if one doesn't
    // already exist — Publish/NewVersion are the only two places a version transition happens, so
    // both funnel through here to guarantee every version number that's ever been "current" ends up
    // with a real, fetchable WorkflowVersion row (not just the ones that happened to get published).
    private async Task<WorkflowVersion> GetOrCreateVersionSnapshotAsync(Workflow workflow, CancellationToken ct)
    {
        var existing = await dbContext.WorkflowVersions
            .FirstOrDefaultAsync(v => v.WorkflowId == workflow.Id && v.VersionNumber == workflow.Version, ct);
        if (existing is not null)
            return existing;

        var snapshot = WorkflowVersion.CreateSnapshot(
            Guid.NewGuid(), workflow.TenantId, workflow.Id, workflow.Version, BuildWorkflowSnapshotJson(workflow));
        dbContext.WorkflowVersions.Add(snapshot);
        return snapshot;
    }

    private static string BuildWorkflowSnapshotJson(Workflow workflow) => JsonSerializer.Serialize(new
    {
        workflow.Name,
        workflow.Description,
        workflow.Type,
        workflow.Trigger,
        workflow.Steps
    });

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

        // Snapshot what the current version actually contains before the counter moves on and
        // subsequent edits start overwriting Steps in place under the new version number.
        await GetOrCreateVersionSnapshotAsync(workflow, ct);

        workflow.NewVersion();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Workflow {Id} new version created (v{Version})", id, workflow.Version);
        return Ok(new { id, version = workflow.Version, versionStatus = workflow.VersionStatus });
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct = default)
    {
        var versions = await dbContext.WorkflowVersions
            .Where(v => v.WorkflowId == id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new
            {
                v.Id,
                v.WorkflowId,
                v.VersionNumber,
                v.ConfigSnapshot,
                v.IsPublished,
                v.PublishedByUserId,
                v.PublishedAt,
                v.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(versions);
    }

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
        // Webhooks created through the admin UI (WebhooksController) persist a WebhookEndpoint row
        // keyed by its own Slug/Secret/WorkflowId — that row is the source of truth whenever one
        // exists for this slug, since it's the only thing the admin panel actually lets anyone
        // configure. Workflow.Trigger is a separate, older field with no admin UI at all; kept only
        // as a fallback against the single global config secret for anything still relying on it.
        //
        // IgnoreQueryFilters: this action is [AllowAnonymous], so there is no ambient tenant_id claim
        // and the fail-closed tenant filter (P0-5) matches zero rows for every webhook caller — which
        // silently downgraded the whole endpoint to the 503 "not configured" path below. The real
        // boundary here is the unguessable Slug plus the endpoint's own secret/HMAC check further
        // down, not the ambient filter; the linked Workflow is then loaded with the same bypass
        // because the ambient tenant is still null. Same reasoning as ChatbotsController.GetPublicInfo
        // and AuthController.Refresh. Guarded by RegressionTests.D12.
        var webhookEndpoint = await dbContext.WebhookEndpoints
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Slug == slug && !w.IsDeleted, ct);

        R2WAI.Domain.Entities.Workflow? workflow;
        string? configuredSecret;

        if (webhookEndpoint is not null)
        {
            if (!webhookEndpoint.IsActive || webhookEndpoint.WorkflowId is null)
                return NotFound(new { error = $"No active webhook with slug '{slug}'" });

            configuredSecret = webhookEndpoint.Secret;
            if (string.IsNullOrEmpty(configuredSecret))
            {
                logger.LogError("Webhook rejected: webhook '{Slug}' has no secret configured", slug);
                return StatusCode(503, new { error = "Webhook endpoint not configured. Contact administrator." });
            }

            workflow = await dbContext.Workflows.IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Id == webhookEndpoint.WorkflowId && w.IsActive, ct);
            if (workflow is null)
                return NotFound(new { error = "Linked workflow not found or inactive" });
        }
        else
        {
            configuredSecret = configuration["Webhooks:Secret"];
            if (string.IsNullOrEmpty(configuredSecret))
            {
                logger.LogError("Webhook rejected: no webhook secret configured — all webhooks blocked until Webhooks:Secret is set");
                return StatusCode(503, new { error = "Webhook endpoint not configured. Contact administrator." });
            }

            workflow = await dbContext.Workflows.IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Trigger == slug && w.IsActive, ct);
            if (workflow is null)
                return NotFound(new { error = $"No active workflow with trigger '{slug}'" });
        }

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

        var data = payload is not null ? System.Text.Json.JsonSerializer.Serialize(payload) : null;

        try
        {
            var defaultTenantId = workflow.TenantId;
            var (elsaInstanceId, instanceId) = await workflowBridge.StartWorkflowAsync(
                workflow.Id, defaultTenantId, workflow.UserId, data, ct);

            webhookEndpoint?.RecordCall();
            await dbContext.SaveChangesAsync(ct);

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
    public async Task<IActionResult> GetTemplates(CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var templates = await workflowTemplateService.GetAllAsync(tenantId, ct);
        return Ok(new { items = templates });
    }

    public record SetWorkflowTemplateRequest(string Name, string? Description, string Type, List<WorkflowTemplateStepDto> Steps);

    [HttpPut("templates/{id}")]
    public async Task<IActionResult> SetTemplate(string id, [FromBody] SetWorkflowTemplateRequest request, CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        var result = await workflowTemplateService.SetTemplateAsync(
            id, tenantId, request.Name, request.Description, request.Type, request.Steps, ct);
        return Ok(result);
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
        var currentUser = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.ICurrentUserService>();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        var instanceBelongsToTenant = await dbContext.WorkflowInstances
            .AnyAsync(wi => wi.Id == instanceId && wi.TenantId == tenantId, ct);
        if (!instanceBelongsToTenant)
            return NotFound(new { error = "Workflow instance not found." });

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
