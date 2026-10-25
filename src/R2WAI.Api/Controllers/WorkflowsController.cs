using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Api.Services;
using R2WAI.Api.Workflows;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Workflows;
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
    IAIService aiService,
    IModelConfigurationResolver modelConfigResolver,
    IWorkflowTemplateService workflowTemplateService,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
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
    //
    // Both vocabularies are limited to what actually runs. Triggers: only these three start a run
    // (WorkflowTriggerSpec). Actions: the executable step types (StepActivityFactory) — the old list
    // ("Verify Documents", "Assign Officer"…) all became type "Action" steps, which only log a line.
    private static readonly string[] KnownTriggers = ["Manual", "Webhook", "Schedule"];
    private static readonly (string Type, string Meaning)[] StepTypes =
    [
        ("Approval", "a person must approve before the automation continues"),
        ("Email", "send an email notification"),
        ("API Call", "read, create or update a record in another system"),
        ("AI Generate", "draft, summarize, classify or extract text with AI"),
        ("Condition", "branch on a value"),
        ("Transform", "reshape or map data between steps"),
        ("Delay", "wait for a period of time"),
    ];
    private static readonly string[] KnownActions = StepTypes.Select(s => s.Type).ToArray();

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
                "list of steps from the fixed step types given, and any simple conditions implied. " +
                "Return ONLY a valid JSON object — no markdown, no explanation.\n\n" +
                $"Description: {request.Description}\n\n" +
                "Available triggers (pick exactly one, or null if none fit): Manual (someone runs it), " +
                "Webhook (another system calls it), Schedule (runs at set times)\n" +
                "Available step types (pick zero or more, in the order they should run):\n" +
                string.Join("\n", StepTypes.Select(s => $"- {s.Type}: {s.Meaning}")) + "\n\n" +
                "JSON format:\n" +
                "{\n" +
                "  \"name\": \"<short automation name, 2-6 words>\",\n" +
                "  \"trigger\": \"<one of the triggers above, or null>\",\n" +
                "  \"steps\": [{\"type\": \"<step type>\", \"name\": \"<what this step does, 2-5 words>\"}, ...],\n" +
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

        var blockers = GetPublishBlockers(workflow);
        if (blockers.Count > 0)
            return UnprocessableEntity(new ValidationProblemDetails(blockers)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "This automation can't be published yet",
                Detail = blockers.Values.SelectMany(v => v).First(),
            });

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

    // A published automation runs unattended (schedule, webhook, agent tool), so it must not contain
    // anything that silently does nothing: an unsupported trigger, or a placeholder "Action" step,
    // which only writes a log line (ActionNodeProvider). Test runs stay allowed.
    private static Dictionary<string, string[]> GetPublishBlockers(Workflow workflow)
    {
        var errors = new Dictionary<string, string[]>();
        if (WorkflowTriggerSpec.Validate(workflow.Trigger) is { } triggerError)
            errors["Trigger"] = [triggerError];

        List<WorkflowStepDto> steps;
        try
        {
            steps = JsonSerializer.Deserialize<List<WorkflowStepDto>>(workflow.Steps ?? "[]", StepJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            errors["Steps"] = ["The steps could not be read. Open the builder and save the automation again."];
            return errors;
        }

        var placeholders = steps
            .Where(s => StepActivityFactory.ClassifyStepType(s) == "Action")
            .Select(s => $"'{s.Name}'")
            .ToList();
        if (placeholders.Count > 0)
            errors["Steps"] = [$"Replace the placeholder step(s) {string.Join(", ", placeholders)} — they do nothing when run. " +
                "Use Approval, Email, API Call, AI Generate, Condition, Transform or Delay."];

        return errors;
    }

    private static readonly JsonSerializerOptions StepJsonOptions = new(JsonSerializerDefaults.Web);

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
        [FromHeader(Name = "X-Webhook-Signature")] string? webhookSignature = null,
        [FromHeader(Name = "X-Webhook-Timestamp")] string? webhookTimestamp = null,
        CancellationToken ct = default)
    {
        // Keep authentication on an exact, tenant-owned endpoint row. The older global-secret
        // fallback could trigger a workflow in any tenant by guessing its Workflow.Trigger value.
        const int maxBodyBytes = 1_048_576;
        if (Request.ContentLength > maxBodyBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "Webhook payload exceeds the 1 MiB limit." });

        Request.EnableBuffering();
        await using var bodyBuffer = new MemoryStream();
        var readBuffer = new byte[16 * 1024];
        while (true)
        {
            var read = await Request.Body.ReadAsync(readBuffer, ct);
            if (read == 0) break;
            if (bodyBuffer.Length + read > maxBodyBytes)
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "Webhook payload exceeds the 1 MiB limit." });
            await bodyBuffer.WriteAsync(readBuffer.AsMemory(0, read), ct);
        }

        var rawBody = bodyBuffer.ToArray();
        Request.Body.Position = 0;
        if (rawBody.Length > 0)
        {
            try { using var _ = JsonDocument.Parse(rawBody); }
            catch (JsonException) { return BadRequest(new { error = "Webhook payload must be valid JSON." }); }
        }

        var webhookEndpoint = await dbContext.WebhookEndpoints
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Slug == slug && !w.IsDeleted, ct);

        if (webhookEndpoint is null || !webhookEndpoint.IsActive || webhookEndpoint.WorkflowId is null)
            return NotFound(new { error = "No active webhook endpoint found." });

        var storedSecret = webhookEndpoint.Secret;
        if (string.IsNullOrWhiteSpace(storedSecret))
        {
            logger.LogError("Webhook rejected: webhook '{Slug}' has no secret configured", slug);
            return StatusCode(503, new { error = "Webhook endpoint not configured. Contact administrator." });
        }

        // New values are version-prefixed AES-GCM ciphertext. Plaintext is accepted only for legacy
        // rows and upgraded after a successful call so existing customers can migrate without a
        // keyless database migration.
        var isLegacySecret = !storedSecret.StartsWith("enc:v1:", StringComparison.Ordinal);
        var configuredSecret = isLegacySecret
            ? storedSecret
            : encryptionService.Decrypt(storedSecret["enc:v1:".Length..]);

        var workflow = await dbContext.Workflows.IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == webhookEndpoint.WorkflowId
                && w.TenantId == webhookEndpoint.TenantId && w.IsActive && !w.IsDeleted, ct);
        if (workflow is null)
            return NotFound(new { error = "Linked automation not found or inactive." });

        var hasSignature = !string.IsNullOrWhiteSpace(webhookSignature);
        var hasTimestamp = !string.IsNullOrWhiteSpace(webhookTimestamp);
        if (!hasSignature || !hasTimestamp)
            return Unauthorized(new { error = "Signed webhook requests require signature and timestamp headers." });

        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!long.TryParse(webhookTimestamp, out var ts) || ts < now - 300 || ts > now + 300)
            {
                logger.LogWarning("Webhook rejected: timestamp too old or invalid for slug {Slug}", slug);
                return Unauthorized(new { error = "Webhook timestamp expired or invalid" });
            }

            var signPayload = System.Text.Encoding.UTF8.GetBytes($"{webhookTimestamp}.").Concat(rawBody).ToArray();
            var expected = System.Security.Cryptography.HMACSHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(configuredSecret), signPayload);
            var suppliedSignature = webhookSignature!.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
                ? webhookSignature[7..]
                : webhookSignature;

            byte[] suppliedBytes;
            try { suppliedBytes = Convert.FromHexString(suppliedSignature); }
            catch (FormatException) { return Unauthorized(new { error = "Invalid webhook signature" }); }

            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, suppliedBytes))
            {
                logger.LogWarning("Webhook rejected: HMAC signature mismatch for slug {Slug}", slug);
                return Unauthorized(new { error = "Invalid webhook signature" });
            }
        }

        var providerDeliveryKey = new[]
            {
                Request.Headers["Idempotency-Key"].FirstOrDefault(),
                Request.Headers["X-Webhook-Delivery"].FirstOrDefault(),
                Request.Headers["X-GitHub-Delivery"].FirstOrDefault()
            }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var deliveryKey = providerDeliveryKey?.Trim()
            ?? $"signature:{webhookTimestamp}:{webhookSignature!.Trim().ToLowerInvariant()}";
        if (deliveryKey.Length > 256)
            return BadRequest(new { error = "Webhook delivery key exceeds the 256 character limit." });

        // Store only a digest; provider delivery IDs can otherwise reveal identifiers in database
        // exports. The signature fallback still catches exact signed-request replays when a sender
        // does not provide a stable delivery ID header.
        var deliveryKeyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{webhookEndpoint.Id:N}:{deliveryKey}")));
        var data = rawBody.Length > 0 ? System.Text.Encoding.UTF8.GetString(rawBody) : null;

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
                transaction = await dbContext.Database.BeginTransactionAsync(ct);

            var receivedAt = DateTime.UtcNow;
            if (dbContext.Database.IsNpgsql())
            {
                // Keep the replay table bounded without putting an unbounded cleanup query in the
                // request path. Each accepted delivery removes at most 100 expired claims.
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM "WebhookDeliveryRecords"
                    WHERE "Id" IN (
                        SELECT "Id" FROM "WebhookDeliveryRecords"
                        WHERE "ExpiresAt" <= {receivedAt}
                        ORDER BY "ExpiresAt"
                        LIMIT 100
                    )
                    """, ct);

                var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "WebhookDeliveryRecords"
                        ("Id", "WebhookEndpointId", "DeliveryKeyHash", "ExpiresAt", "CreatedAt", "ModifiedAt", "IsDeleted")
                    VALUES
                        ({Guid.NewGuid()}, {webhookEndpoint.Id}, {deliveryKeyHash}, {receivedAt.AddHours(24)}, {receivedAt}, {receivedAt}, FALSE)
                    ON CONFLICT ("WebhookEndpointId", "DeliveryKeyHash") DO NOTHING
                    """, ct);

                if (inserted == 0)
                {
                    if (transaction is not null)
                        await transaction.CommitAsync(ct);
                    return Accepted(new { workflowId = workflow.Id, duplicate = true, status = "already accepted" });
                }
            }
            else
            {
                var alreadyReceived = await dbContext.WebhookDeliveryRecords
                    .AnyAsync(record => record.WebhookEndpointId == webhookEndpoint.Id
                        && record.DeliveryKeyHash == deliveryKeyHash && record.ExpiresAt > receivedAt, ct);
                if (alreadyReceived)
                {
                    if (transaction is not null)
                        await transaction.CommitAsync(ct);
                    return Accepted(new { workflowId = workflow.Id, duplicate = true, status = "already accepted" });
                }

                dbContext.WebhookDeliveryRecords.Add(new WebhookDeliveryRecord(
                    Guid.NewGuid(), webhookEndpoint.Id, deliveryKeyHash, receivedAt.AddHours(24)));
                await dbContext.SaveChangesAsync(ct);
            }

            var defaultTenantId = workflow.TenantId;
            var (elsaInstanceId, instanceId) = await workflowBridge.StartWorkflowAsync(
                workflow.Id, defaultTenantId, workflow.UserId, data, ct);

            webhookEndpoint.RecordCall();
            if (isLegacySecret)
                webhookEndpoint.SetSecret($"enc:v1:{encryptionService.Encrypt(configuredSecret)}");
            await dbContext.SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);

            logger.LogInformation("Webhook triggered workflow {WorkflowId} → instance {InstanceId}", workflow.Id, instanceId);
            return Accepted(new { workflowId = workflow.Id, instanceId, elsaInstanceId, status = "running" });
        }
        catch (Exception ex)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            logger.LogError(ex, "Webhook trigger failed for workflow {WorkflowId}", workflow.Id);
            return StatusCode(500, new { error = "Workflow execution failed" });
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
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
