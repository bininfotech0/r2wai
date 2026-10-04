using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Assistants.Commands;
using R2WAI.Application.Features.Assistants.Queries;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.Prompts;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class AssistantsController(
    IMediator mediator,
    ApplicationDbContext dbContext,
    IAIService aiService,
    IKnowledgeBaseService knowledgeBaseService,
    IPromptTemplateService promptTemplateService,
    IModelConfigurationResolver modelConfigResolver,
    IAiUsagePolicyService aiUsagePolicyService,
    IPiiPolicyService piiPolicyService,
    IRepository<AuditLog> auditLogRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IChatStreamContext chatStreamContext,
    ILogger<AssistantsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssistantCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating assistant: {Name}", command.Name);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] Guid? applicationId = null, [FromQuery] PublishStatus? publishStatus = null, [FromQuery] string? sortBy = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new GetAssistantsQuery { Page = page, PageSize = pageSize, Search = search, ApplicationId = applicationId, PublishStatus = publishStatus, SortBy = sortBy };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/application")]
    public async Task<IActionResult> AssignApplication(Guid id, [FromBody] AssignApplicationRequest request, CancellationToken ct = default)
    {
        var command = new AssignAssistantApplicationCommand { Id = id, ApplicationId = request.ApplicationId };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    public record AssignApplicationRequest(Guid? ApplicationId);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetAssistantByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssistantCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteAssistantCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/prompt-history")]
    public async Task<IActionResult> GetPromptHistory(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAssistantPromptHistoryQuery { AssistantDefinitionId = id }, ct);
        return Ok(result);
    }

    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.3 #63/#64 — mirrors KnowledgeBasesController's
    // versions/rollback endpoints exactly (same pattern already proven on Workflows/KnowledgeBases/
    // Capabilities).
    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAssistantVersionsQuery { AssistantDefinitionId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> CreateVersion(Guid id, [FromBody] CreateAssistantVersionCommand command, CancellationToken ct = default)
    {
        command = command with { AssistantDefinitionId = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/rollback")]
    public async Task<IActionResult> RollbackVersion(Guid id, Guid versionId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new RollbackAssistantVersionCommand { VersionId = versionId }, ct);
        return Ok(result);
    }

    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.3 #65 — the brief's "Duplicate" card action.
    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> Clone(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new CloneAssistantCommand { AssistantDefinitionId = id }, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("prompt-templates")]
    public async Task<IActionResult> GetPromptTemplates(CancellationToken ct = default)
    {
        if (currentUser.TenantId is not { } tenantId)
            return Ok(new { items = SystemPromptTemplates.GetAll().Select(t => new { type = t.Key, prompt = t.Value }) });

        // Tenant-overridden templates (Prompt Management) take precedence over the static defaults —
        // GetAllActiveTemplatesAsync already merges the two.
        var templates = await promptTemplateService.GetAllActiveTemplatesAsync(tenantId, ct);
        return Ok(new { items = templates.Select(t => new { type = t.Key, prompt = t.Value }) });
    }

    public record SetPromptTemplateRequest(string Content);

    [HttpPut("prompt-templates/{type}")]
    public async Task<IActionResult> SetPromptTemplate(string type, [FromBody] SetPromptTemplateRequest request, CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        if (!Enum.TryParse<AssistantType>(type, ignoreCase: true, out var assistantType))
            return BadRequest(new { error = $"'{type}' is not a known assistant type." });

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Content is required." });

        var content = await promptTemplateService.SetTemplateAsync(assistantType, tenantId, request.Content, ct);
        return Ok(new { type = assistantType.ToString(), prompt = content });
    }

    [HttpDelete("prompt-templates/{type}")]
    public async Task<IActionResult> ResetPromptTemplate(string type, CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        if (!Enum.TryParse<AssistantType>(type, ignoreCase: true, out var assistantType))
            return BadRequest(new { error = $"'{type}' is not a known assistant type." });

        await promptTemplateService.ResetTemplateAsync(assistantType, tenantId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct = default)
    {
        var assistant = await dbContext.AssistantDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assistant is null) return NotFound();
        var checks = await GetPublishReadinessAsync(assistant, ct);
        var blockers = checks.Where(check => !check.Ready).ToArray();
        if (blockers.Length > 0)
            return Conflict(new { message = "Resolve the readiness items before publishing.", checks = blockers });
        assistant.Publish();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Assistant {Id} published", id);
        return Ok(new { id, isActive = true, status = "Published", publishedVersion = assistant.PublishedVersion });
    }

    [HttpGet("{id:guid}/readiness")]
    public async Task<IActionResult> GetPublishReadiness(Guid id, CancellationToken ct = default)
    {
        var assistant = await dbContext.AssistantDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assistant is null) return NotFound();
        return Ok(new { checks = await GetPublishReadinessAsync(assistant, ct) });
    }

    private async Task<IReadOnlyList<AssistantPublishCheck>> GetPublishReadinessAsync(
        AssistantDefinition assistant, CancellationToken ct)
    {
        var hasModel = assistant.ModelConfigurationId is Guid modelId
            ? await dbContext.ModelConfigurations.AnyAsync(m => m.Id == modelId && m.TenantId == assistant.TenantId && m.IsActive, ct)
            : await dbContext.ModelConfigurations.AnyAsync(m => m.TenantId == assistant.TenantId && m.IsDefault && m.IsActive, ct);
        var knowledgeReady = assistant.KnowledgeBaseId is not Guid knowledgeBaseId ||
            await dbContext.KnowledgeBases.AnyAsync(k => k.Id == knowledgeBaseId && k.TenantId == assistant.TenantId && k.Status == KnowledgeBaseStatus.Active, ct);

        return new[]
        {
            new AssistantPublishCheck("Name configured", !string.IsNullOrWhiteSpace(assistant.Name),
                string.IsNullOrWhiteSpace(assistant.Name) ? "Add a name to the assistant." : null),
            new AssistantPublishCheck("Instructions configured", !string.IsNullOrWhiteSpace(assistant.SystemPrompt),
                string.IsNullOrWhiteSpace(assistant.SystemPrompt) ? "Add instructions so the assistant knows how to respond." : null),
            new AssistantPublishCheck("AI model available", hasModel,
                hasModel ? null : "Choose an active model or set an active tenant default in AI Models."),
            new AssistantPublishCheck("Selected knowledge source available", knowledgeReady,
                assistant.KnowledgeBaseId is null
                    ? "Optional; no knowledge base selected."
                    : knowledgeReady ? null : "The selected knowledge base is unavailable. Choose an active knowledge base or remove it.")
        };
    }

    private sealed record AssistantPublishCheck(string Label, bool Ready, string? Detail);

    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct = default)
    {
        var assistant = await dbContext.AssistantDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assistant is null) return NotFound();
        assistant.Unpublish();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Assistant {Id} unpublished", id);
        return Ok(new { id, isActive = false, status = "Draft" });
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct = default)
    {
        var assistant = await dbContext.AssistantDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assistant is null) return NotFound();
        assistant.Archive();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Assistant {Id} archived", id);
        return Ok(new { id, isActive = false, status = "Archived" });
    }

    [HttpPost("{id:guid}/chat")]
    public async Task<IActionResult> Chat(Guid id, [FromBody] ChatWithAssistantCommand command, CancellationToken ct = default)
    {
        command = command with { AssistantId = id };
        var result = await mediator.Send(command, ct);
        return Ok(new
        {
            reply = result.Reply,
            conversationId = result.ConversationId,
            tokensUsed = result.TokensUsed,
            citations = result.Citations
        });
    }

    [HttpPost("{id:guid}/chat/stream")]
    public async Task StreamChat(Guid id, [FromBody] ChatWithAssistantCommand command, CancellationToken ct = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var streamCt = linkedCts.Token;

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var assistant = await dbContext.AssistantDefinitions
            .Include(a => a.KnowledgeBase)
            .FirstOrDefaultAsync(a => a.Id == id, streamCt);

        if (assistant is null)
        {
            await WriteSseEventAsync("error", new { message = "Assistant not found" }, streamCt);
            return;
        }

        // Same Draft/Archived gate as the non-streaming ChatWithAssistantCommand — this SSE path
        // bypasses MediatR entirely and previously bypassed the check along with it.
        if (assistant.PublishStatus != PublishStatus.Published
            && !currentUser.Roles.Contains("Admin") && !currentUser.Roles.Contains("SystemAdmin"))
        {
            logger.LogWarning("Denied StreamChat for assistant {AssistantId} — not published and requester lacks Admin/SystemAdmin", id);
            await WriteSseEventAsync("error", new { message = "This assistant is not published." }, streamCt);
            return;
        }

        // Policy Engine: same tenant-configured daily request cap ChatWithAssistantCommand
        // enforces on the non-streaming chat endpoint — this SSE path bypassed it entirely.
        if (!await aiUsagePolicyService.IsUnderCapAsync(assistant.TenantId, streamCt))
        {
            logger.LogWarning("Denied StreamChat for tenant {TenantId} — exceeds this tenant's configured AiUsage policy cap", assistant.TenantId);
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), assistant.TenantId, AuditAction.Execute, "GlobalPolicy",
                "AiUsage", currentUser.UserId, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "exceeds daily AiUsage cap", source = "AssistantsController.StreamChat" })),
                streamCt);
            await unitOfWork.SaveChangesAsync(streamCt);
            await WriteSseEventAsync("error", new
            {
                message = "This tenant has reached its configured daily AI usage limit. Please try again tomorrow, or ask an administrator to raise the limit."
            }, streamCt);
            return;
        }
        await aiUsagePolicyService.RecordRequestAsync(assistant.TenantId, streamCt);

        // Policy Engine: same tenant-configured PII rule ChatWithAssistantCommand enforces on the
        // non-streaming chat endpoint — this SSE path bypassed it entirely, same as AiUsage above.
        var piiResult = await piiPolicyService.CheckAsync(command.Message, assistant.TenantId, streamCt);
        if (piiResult.Blocked)
        {
            logger.LogWarning("Denied StreamChat for tenant {TenantId} — message contains PII ({Types}) and this tenant's policy blocks it",
                assistant.TenantId, string.Join(", ", piiResult.DetectedTypes));
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), assistant.TenantId, AuditAction.Execute, "GlobalPolicy",
                "Pii", currentUser.UserId, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "message contains PII", types = piiResult.DetectedTypes, source = "AssistantsController.StreamChat" })),
                streamCt);
            await unitOfWork.SaveChangesAsync(streamCt);
            await WriteSseEventAsync("error", new
            {
                message = "This message appears to contain personal information this tenant's policy doesn't allow sending to the assistant. Please remove it and try again."
            }, streamCt);
            return;
        }
        if (piiResult.DetectedTypes.Count > 0)
        {
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), assistant.TenantId, AuditAction.Execute, "GlobalPolicy",
                "Pii", currentUser.UserId, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "redacted", types = piiResult.DetectedTypes, source = "AssistantsController.StreamChat" })),
                streamCt);
            await unitOfWork.SaveChangesAsync(streamCt);
        }
        var effectiveMessage = piiResult.ProcessedText;

        string? context = null;
        List<CitationDto>? citations = null;

        if (assistant.KnowledgeBaseId.HasValue)
        {
            try
            {
                var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                    assistant.KnowledgeBaseId.Value, effectiveMessage, 1, 5, streamCt);

                if (searchResult.Items.Count > 0)
                {
                    context = string.Join("\n\n", searchResult.Items.Select(i => i.Content));
                    citations = searchResult.Items
                        .Select((item, index) => new CitationDto(
                            item.SourceName ?? "Unknown",
                            item.Content,
                            (float)item.Score,
                            index + 1))
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to search knowledge base for streaming chat");
            }
        }

        var basePrompt = assistant.SystemPrompt
            ?? await promptTemplateService.GetActiveTemplateAsync(assistant.Type, assistant.TenantId, streamCt);
        var systemPrompt = basePrompt
            + $"\n\n[Assistant context: your assistant ID is {assistant.Id}, your name is \"{assistant.Name}\"" +
              (!string.IsNullOrWhiteSpace(assistant.Description)
                  ? $", and your purpose is: {assistant.Description}"
                  : $", and you are a {assistant.Type} assistant") +
              ". This is everything you need to know about yourself — you do not need to call get_assistant_context " +
              "to answer questions about what you do or who you are. If you need an assistant ID for some other tool, " +
              "use the ID above directly, never guess or leave it blank.]";

        var behaviorSettings = assistant.GetBehaviorSettings();
        var behaviorAddendum = behaviorSettings?.BuildPromptAddendum();
        if (!string.IsNullOrEmpty(behaviorAddendum))
            systemPrompt += $"\n\n[Response style: {behaviorAddendum}]";
        if (behaviorSettings?.CitationsEnabled == false)
            citations = null;

        var modelConfig = await modelConfigResolver.ResolveAsync(assistant.ModelConfigurationId, assistant.TenantId, streamCt);
        if (modelConfig is not null && behaviorSettings is not null && (behaviorSettings.Temperature.HasValue || behaviorSettings.MaxOutputTokens.HasValue))
            modelConfig = modelConfig with
            {
                Temperature = behaviorSettings.Temperature ?? modelConfig.Temperature,
                MaxTokens = behaviorSettings.MaxOutputTokens ?? modelConfig.MaxTokens
            };

        chatStreamContext.OnProgress = evt => evt.Kind == ToolCallProgressKind.Started
            ? WriteSseEventAsync("toolCallStarted", new { toolName = evt.ToolName }, streamCt)
            : WriteSseEventAsync("toolCallCompleted", new { toolName = evt.ToolName, success = evt.Success ?? true }, streamCt);

        try
        {
            await foreach (var chunk in aiService.StreamChatAsync(effectiveMessage, context, systemPrompt, enableTools: true, modelConfig: modelConfig, enabledToolIds: assistant.GetEnabledToolIds(), ct: streamCt))
            {
                await WriteSseEventAsync("chunk", new { content = chunk }, streamCt);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // A repository research pass found this codebase's own established pattern — every
            // other failure mode above (not found, not published, over cap, PII blocked) emits a
            // clean "error" SSE event — was missing here specifically: a provider failure mid-stream
            // (timeout, bad key, unreachable host) used to just drop the connection silently, with no
            // error/done event at all. `!ct.IsCancellationRequested` (the caller's own token, not
            // streamCt, which also trips on our own 5-minute ceiling) distinguishes "the client is
            // still there, tell them" from "the client already disconnected, there's no one to tell" —
            // in the latter case this filter doesn't match and the exception propagates normally.
            logger.LogWarning(ex, "AI provider failed mid-stream for assistant {AssistantId}", id);
            await WriteSseEventAsync("error", new { message = "The AI service failed while generating a response. Please try again." }, ct);
            return;
        }

        if (citations is { Count: > 0 })
        {
            await WriteSseEventAsync("citations", new { citations }, streamCt);
        }

        // Response cards (Phase 3) — this SSE path is ephemeral (no Message row to persist to), so the
        // card travels as its own event instead of a MessageDto.contentBlocks round-trip.
        if (chatStreamContext.CapturedContentBlock is { } capturedBlock)
        {
            await WriteSseEventAsync("contentBlocks", new { contentBlocks = capturedBlock }, streamCt);
        }

        await WriteSseEventAsync("done", new { message = "Stream complete" }, streamCt);
    }

    private async Task WriteSseEventAsync(string eventType, object data, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var sseMessage = $"event: {eventType}\ndata: {json}\n\n";
        await Response.WriteAsync(sseMessage, ct);
        await Response.Body.FlushAsync(ct);
    }

    [HttpPost("generate-config")]
    public async Task<IActionResult> GenerateConfig([FromBody] GenerateConfigRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "Description is required" });

        try
        {
            var prompt =
                "You are an enterprise AI assistant configuration generator. " +
                "Based on the description below, return ONLY a valid JSON object — no markdown, no explanation.\n\n" +
                $"Description: {request.Description}\n\n" +
                "JSON format:\n" +
                "{\n" +
                "  \"name\": \"<Short professional name, 2-5 words>\",\n" +
                "  \"type\": \"<Exactly one of: General, HR, IT, Finance, Procurement, Legal>\",\n" +
                "  \"description\": \"<One sentence, max 150 characters>\",\n" +
                "  \"systemPrompt\": \"<Detailed behavioral system prompt, 150-350 words, professional tone>\"\n" +
                "}";

            var modelConfig = currentUser.TenantId.HasValue
                ? await modelConfigResolver.ResolveAsync(null, currentUser.TenantId.Value, ct)
                : null;
            var raw = await aiService.GenerateResponseAsync(prompt, modelConfig: modelConfig, ct: ct);

            var json = raw.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) json = json[7..];
            else if (json.StartsWith("```")) json = json[3..];
            if (json.EndsWith("```")) json = json[..^3];
            json = json.Trim();

            var cfg = System.Text.Json.JsonSerializer.Deserialize<AiConfigDto>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (cfg is not null && !string.IsNullOrWhiteSpace(cfg.Name))
            {
                logger.LogInformation("AI generated assistant config: {Name} ({Type})", cfg.Name, cfg.Type);
                var validatedType = cfg.Type is not null && Enum.TryParse<AssistantType>(cfg.Type, ignoreCase: true, out var parsedType)
                    ? parsedType.ToString()
                    : "General";
                return Ok(new
                {
                    name = cfg.Name,
                    type = validatedType,
                    description = cfg.Description ?? string.Empty,
                    systemPrompt = cfg.SystemPrompt ?? string.Empty,
                    isAiGenerated = true
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI config generation failed, falling back to template");
        }

        // Template fallback
        var desc = request.Description.ToLowerInvariant();
        var type = desc.Contains("hr") || desc.Contains("onboard") || desc.Contains("employee") ? "HR"
            : desc.Contains(" it ") || desc.Contains("helpdesk") || desc.Contains("support") || desc.Contains("technical") ? "IT"
            : desc.Contains("finance") || desc.Contains("expense") || desc.Contains("invoice") || desc.Contains("budget") ? "Finance"
            : desc.Contains("legal") || desc.Contains("contract") || desc.Contains("compliance") ? "Legal"
            : desc.Contains("procure") || desc.Contains("vendor") || desc.Contains("purchase") ? "Procurement"
            : "General";

        var templates = currentUser.TenantId is { } tenantIdForFallback
            ? await promptTemplateService.GetAllActiveTemplatesAsync(tenantIdForFallback, ct)
            : SystemPromptTemplates.GetAll();
        var systemPrompt = templates.TryGetValue(type, out var tmpl) ? tmpl : templates["General"];
        var name = type == "General" ? "General Assistant" : $"{type} Assistant";

        return Ok(new
        {
            name,
            type,
            description = request.Description[..Math.Min(150, request.Description.Length)],
            systemPrompt,
            isAiGenerated = false
        });
    }

    public record GenerateConfigRequest(string Description);

    private record AiConfigDto(string Name, string? Type, string? Description, string? SystemPrompt);
}
