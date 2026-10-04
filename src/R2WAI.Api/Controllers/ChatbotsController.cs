using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Assistants.Commands;
using R2WAI.Application.Features.Chatbots;
using R2WAI.Application.Features.Chatbots.Commands;
using R2WAI.Application.Features.Chatbots.Queries;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ChatbotsController(
    IMediator mediator,
    ApplicationDbContext dbContext,
    IAIService aiService,
    IKnowledgeBaseService knowledgeBaseService,
    IAiUsagePolicyService aiUsagePolicyService,
    IPiiPolicyService piiPolicyService,
    IModelConfigurationResolver modelConfigResolver,
    IStorageService storageService,
    IRepository<AuditLog> auditLogRepo,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    ILogger<ChatbotsController> logger) : ControllerBase
{
    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 #56. Deliberately much tighter than
    // DocumentsController's authenticated upload (50 MB, includes Office formats): this route is
    // anonymous, reachable by anyone who can load the widget, so the blast radius of one request
    // has to be bounded harder. 5 MB and images/PDF/plain-text only — no Office documents (macro
    // risk, no legitimate use case for a support-chat attachment) and no archives.
    private const long MaxAttachmentSize = 5 * 1024 * 1024; // 5 MB
    private static readonly HashSet<string> AllowedAttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf", "text/plain",
    };
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChatbotCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating chatbot: {Name}", command.Name);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? assistantId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new GetChatbotsQuery { Page = page, PageSize = pageSize, AssistantId = assistantId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetChatbotByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChatbotCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteChatbotCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    public record UpdateChatbotWidgetRequest(string EmbedScript, string WidgetSettings);

    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 #50/#51 — persists the widget deployment
    // page's appearance config + generated embed snippet so they survive a page reload or a
    // second device, instead of existing only in that one browser session's local state.
    [HttpPut("{id:guid}/widget")]
    public async Task<IActionResult> UpdateWidget(Guid id, [FromBody] UpdateChatbotWidgetRequest request, CancellationToken ct = default)
    {
        var command = new UpdateChatbotWidgetCommand { Id = id, EmbedScript = request.EmbedScript, WidgetSettings = request.WidgetSettings };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    public record SetChatbotStatusRequest(ChatbotStatus Status);

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetChatbotStatusRequest request, CancellationToken ct = default)
    {
        var command = new SetChatbotStatusCommand { Id = id, Status = request.Status };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/channels")]
    public async Task<IActionResult> GetChannels(Guid id, CancellationToken ct = default)
    {
        var query = new GetChatbotChannelsQuery { ChatbotId = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/channels/{channel}")]
    public async Task<IActionResult> ConnectChannel(Guid id, string channel, [FromBody] System.Text.Json.JsonElement payload, CancellationToken ct = default)
    {
        var command = new ConnectChatbotChannelCommand
        {
            ChatbotId = id,
            Channel = channel,
            PayloadJson = payload.GetRawText(),
        };
        await mediator.Send(command, ct);
        return Ok();
    }

    [HttpDelete("{id:guid}/channels/{channel}")]
    public async Task<IActionResult> DisconnectChannel(Guid id, string channel, CancellationToken ct = default)
    {
        var command = new DisconnectChatbotChannelCommand { ChatbotId = id, Channel = channel };
        await mediator.Send(command, ct);
        return Ok();
    }

    [HttpGet("{id:guid}/usage")]
    public async Task<IActionResult> GetUsage(Guid id, CancellationToken ct = default)
    {
        var query = new GetChatbotUsageQuery { ChatbotId = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/webhook-key")]
    public async Task<IActionResult> GetWebhookKey(Guid id, CancellationToken ct = default)
    {
        var configuredHost = configuration["ApiPublicUrl"] ?? configuration["ApiBaseUrl"];
        var baseUrl = !string.IsNullOrWhiteSpace(configuredHost)
            ? configuredHost.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host}";
        var query = new GetWebhookKeyInfoQuery { ChatbotId = id, WebhookUrl = $"{baseUrl}/api/v1/chatbots/{id}/webhook" };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/webhook-key/regenerate")]
    public async Task<IActionResult> RegenerateWebhookKey(Guid id, CancellationToken ct = default)
    {
        var command = new RegenerateWebhookKeyCommand { ChatbotId = id };
        var result = await mediator.Send(command, ct);
        logger.LogInformation("Webhook API key regenerated for chatbot {ChatbotId}", id);
        return Ok(new
        {
            Key = result.RawKey,
            result.KeyPrefix,
            Message = "Store this key securely — it cannot be retrieved again."
        });
    }

    public record ChatbotPublicInfo(string Name, string? WelcomeMessage, bool VoiceEnabled, string[]? SuggestedQuestions);

    // Anonymous: the embeddable widget (ChatbotWidget.razor) runs on external, unauthenticated
    // websites and only needs enough to render its header/greeting — never the prompt template
    // or internal config IDs, which GetById exposes to authenticated tenant users.
    [HttpGet("{id:guid}/public-info")]
    [AllowAnonymous]
    [EnableCors("PublicChatbotCors")]
    public async Task<IActionResult> GetPublicInfo(Guid id, CancellationToken ct = default)
    {
        // IgnoreQueryFilters: [AllowAnonymous] means there is no ambient tenant_id claim, so the
        // fail-closed tenant filter (P0-5) would otherwise match zero rows for every anonymous
        // caller here — the real security boundary for this widget is Status == Active plus the
        // origin allowlist/webhook-key checks below, not the ambient filter. Same reasoning as
        // AuthController.Refresh/StatusHub/ChatHub.
        var chatbot = await dbContext.Chatbots
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chatbot is null || chatbot.Status != ChatbotStatus.Active)
            return NotFound(new { error = "Chatbot not found" });

        if (!IsRequestOriginAllowed(chatbot))
            return StatusCode(403, new { error = "This origin is not authorized to access this chatbot." });

        await RecordWidgetSeenAsync(chatbot.Id, ct);

        // SuggestedQuestions is stored as a JSON-encoded string[] (see the client's
        // encodeSuggestedQuestionsText) — configurable in the admin UI since it shipped, but never
        // once exposed here, so the widget had no way to render the starter questions at all.
        string[]? suggestedQuestions = null;
        if (!string.IsNullOrWhiteSpace(chatbot.SuggestedQuestions))
        {
            try
            {
                suggestedQuestions = System.Text.Json.JsonSerializer.Deserialize<string[]>(chatbot.SuggestedQuestions);
            }
            catch (System.Text.Json.JsonException)
            {
                // Malformed stored value — surface no starter questions rather than fail the whole call.
            }
        }

        return Ok(new ChatbotPublicInfo(chatbot.Name, chatbot.WelcomeMessage, chatbot.VoiceEnabled, suggestedQuestions));
    }

    public record ChatbotChatRequest(string Message);

    // Anonymous for the same reason as GetPublicInfo above — this is the endpoint the public
    // embed widget calls to actually send a message.
    [HttpPost("{id:guid}/chat")]
    [AllowAnonymous]
    [EnableCors("PublicChatbotCors")]
    public async Task<IActionResult> Chat(Guid id, [FromBody] ChatbotChatRequest request, CancellationToken ct = default)
    {
        // IgnoreQueryFilters — see GetPublicInfo above for why this is required, not just safe.
        var chatbot = await dbContext.Chatbots
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        // Same "looks not-found" treatment as an actually-missing id — a Draft/Paused chatbot
        // shouldn't be distinguishable from a nonexistent one to an anonymous caller. This is the
        // "Publish" lifecycle's actual enforcement: without it, Draft/Paused was a UI label only.
        if (chatbot is null || chatbot.Status != ChatbotStatus.Active)
            return NotFound(new { error = "Chatbot not found" });

        if (!IsRequestOriginAllowed(chatbot))
            return StatusCode(403, new { error = "This origin is not authorized to access this chatbot." });

        // Policy Engine: same tenant-configured daily request cap enforced on authenticated
        // assistant chat — doubly important here, since this endpoint is [AllowAnonymous] and is
        // the most exposed AI-cost surface in the app (any visitor to a site embedding the widget).
        if (!await aiUsagePolicyService.IsUnderCapAsync(chatbot.TenantId, ct))
        {
            logger.LogWarning("Denied public chatbot chat for tenant {TenantId} — exceeds this tenant's configured AiUsage policy cap", chatbot.TenantId);
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), chatbot.TenantId, AuditAction.Execute, "GlobalPolicy",
                "AiUsage", userId: null, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "exceeds daily AiUsage cap", source = "ChatbotsController.Chat", chatbotId = id })),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            return StatusCode(429, new { error = "This service has reached its configured daily usage limit. Please try again tomorrow." });
        }
        await aiUsagePolicyService.RecordRequestAsync(chatbot.TenantId, ct);

        var effectiveMessage = await CheckPiiAndGetEffectiveMessageAsync(chatbot, request.Message, "ChatbotsController.Chat", ct);
        if (effectiveMessage is null)
            return StatusCode(451, new { error = "This message appears to contain personal information this tenant's policy doesn't allow sending to the assistant. Please remove it and try again." });

        var context = await SearchKnowledgeBaseContextAsync(chatbot, effectiveMessage, ct);
        var systemPrompt = chatbot.PromptTemplate ?? "You are a helpful AI assistant.";
        var modelConfig = await modelConfigResolver.ResolveAsync(chatbot.ModelConfigurationId, chatbot.TenantId, ct);
        // enableTools stays false: this endpoint is [AllowAnonymous] for the public embed
        // widget, and must never expose mutating tools (start_workflow, submit_approval_request,
        // notify_approver) to unauthenticated website visitors.
        var reply = await aiService.ChatAsync(effectiveMessage, context, systemPrompt, enableTools: false, modelConfig: modelConfig, ct: ct);
        await IncrementMessagesServedAsync(chatbot.Id, ct);

        return Ok(new { reply });
    }

    // Policy Engine: same tenant-configured PII rule ChatWithAssistantCommand enforces on the
    // authenticated assistant chat — shared across Chat/StreamChat/Webhook below since all three
    // hit this same [AllowAnonymous]-reachable AI-cost surface. Returns null when the policy
    // blocks the message (already denied + audited); otherwise the message to actually use
    // (redacted if the policy called for it, unchanged otherwise).
    private async Task<string?> CheckPiiAndGetEffectiveMessageAsync(Chatbot chatbot, string message, string source, CancellationToken ct)
    {
        var piiResult = await piiPolicyService.CheckAsync(message, chatbot.TenantId, ct);
        if (piiResult.Blocked)
        {
            logger.LogWarning("Denied {Source} for tenant {TenantId} — message contains PII ({Types}) and this tenant's policy blocks it",
                source, chatbot.TenantId, string.Join(", ", piiResult.DetectedTypes));
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), chatbot.TenantId, AuditAction.Execute, "GlobalPolicy",
                "Pii", userId: null, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "message contains PII", types = piiResult.DetectedTypes, source, chatbotId = chatbot.Id })),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            return null;
        }
        if (piiResult.DetectedTypes.Count > 0)
        {
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), chatbot.TenantId, AuditAction.Execute, "GlobalPolicy",
                "Pii", userId: null, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "redacted", types = piiResult.DetectedTypes, source, chatbotId = chatbot.Id })),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return piiResult.ProcessedText;
    }

    // Chatbot.AllowedOrigins is an opt-in per-tenant allowlist (see ChatbotOriginPolicy) — the
    // browser's own CORS check can't be the real gate here since PublicChatbotCors is
    // deliberately open (a chatbot with no restriction configured must still be embeddable on any
    // site), so the actual decision has to be made per-request against the specific chatbot. Note
    // this only stops browser embedding on an unapproved site — a direct, non-browser HTTP call
    // can set any Origin header it likes, so this is a widget-embedding control, not a substitute
    // for the webhook's real API-key auth.
    private bool IsRequestOriginAllowed(Chatbot chatbot)
    {
        var allowedOrigins = ChatbotOriginPolicy.TryParseAllowedOrigins(chatbot.AllowedOrigins);
        var requestOrigin = Request.Headers.Origin.ToString();
        return ChatbotOriginPolicy.IsOriginAllowed(allowedOrigins, requestOrigin);
    }

    // The honest signal behind the deployment page's "Installation status": rather than actively
    // probing the customer's site (a new outbound-request/SSRF surface for a "nice to have"
    // status light) or requiring a DNS TXT record (proves domain control, not that the widget is
    // actually live), this records that a real browser, from an allowed origin, already loaded the
    // widget and reached this endpoint — the same signal StatusRow shows the admin. Best-effort:
    // ExecuteUpdateAsync is relational-only (EF Core InMemory, used by the fast API test suite,
    // throws), and losing a page-load's timestamp is never worth failing the widget's own load.
    private async Task RecordWidgetSeenAsync(Guid chatbotId, CancellationToken ct)
    {
        if (!dbContext.Database.IsRelational())
            return;

        var origin = Request.Headers.Origin.ToString();
        await dbContext.Chatbots
            .IgnoreQueryFilters()
            .Where(c => c.Id == chatbotId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.WidgetLastSeenAt, DateTime.UtcNow)
                .SetProperty(c => c.WidgetLastSeenOrigin, origin), ct);
    }

    // Backs docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 #54's "widget volume" figure. Relational-only
    // atomic increment (c.Count + 1 in the UPDATE itself), same IsRelational() guard and reasoning as
    // RecordWidgetSeenAsync above — this must never fail or slow down an actual reply being served,
    // and a lost increment under the (test-only) InMemory provider is an acceptable trade for that.
    private async Task IncrementMessagesServedAsync(Guid chatbotId, CancellationToken ct)
    {
        if (!dbContext.Database.IsRelational())
            return;

        await dbContext.Chatbots
            .IgnoreQueryFilters()
            .Where(c => c.Id == chatbotId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.TotalMessagesServed, c => c.TotalMessagesServed + 1), ct);
    }

    private async Task<string?> SearchKnowledgeBaseContextAsync(Chatbot chatbot, string message, CancellationToken ct)
    {
        if (!chatbot.KnowledgeBaseId.HasValue)
            return null;

        try
        {
            var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                chatbot.KnowledgeBaseId.Value, message, 1, 5, ct, expectedTenantId: chatbot.TenantId);

            return searchResult.Items.Count > 0
                ? string.Join("\n\n", searchResult.Items.Select(i => i.Content))
                : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to search knowledge base for chatbot {ChatbotId}", chatbot.Id);
            return null;
        }
    }

    // Streaming counterpart to Chat above — same [AllowAnonymous] rationale and the same
    // enableTools: false restriction. Mirrors AssistantsController's {id}/chat/stream so the
    // embed widget gets the same token-by-token UX as the internal Playground instead of a
    // blocking spinner.
    [HttpPost("{id:guid}/chat/stream")]
    [AllowAnonymous]
    [EnableCors("PublicChatbotCors")]
    public async Task StreamChat(Guid id, [FromBody] ChatbotChatRequest request, CancellationToken ct = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var streamCt = linkedCts.Token;

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        // IgnoreQueryFilters — see GetPublicInfo above for why this is required, not just safe.
        var chatbot = await dbContext.Chatbots
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, streamCt);

        if (chatbot is null || chatbot.Status != ChatbotStatus.Active)
        {
            await WriteSseEventAsync("error", new { message = "Chatbot not found" }, streamCt);
            return;
        }

        if (!IsRequestOriginAllowed(chatbot))
        {
            await WriteSseEventAsync("error", new { message = "This origin is not authorized to access this chatbot." }, streamCt);
            return;
        }

        // Policy Engine: same tenant-configured daily request cap enforced on the non-streaming
        // Chat action above.
        if (!await aiUsagePolicyService.IsUnderCapAsync(chatbot.TenantId, streamCt))
        {
            logger.LogWarning("Denied public chatbot StreamChat for tenant {TenantId} — exceeds this tenant's configured AiUsage policy cap", chatbot.TenantId);
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), chatbot.TenantId, AuditAction.Execute, "GlobalPolicy",
                "AiUsage", userId: null, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "exceeds daily AiUsage cap", source = "ChatbotsController.StreamChat", chatbotId = id })),
                streamCt);
            await unitOfWork.SaveChangesAsync(streamCt);
            await WriteSseEventAsync("error", new { message = "This service has reached its configured daily usage limit. Please try again tomorrow." }, streamCt);
            return;
        }
        await aiUsagePolicyService.RecordRequestAsync(chatbot.TenantId, streamCt);

        var effectiveMessage = await CheckPiiAndGetEffectiveMessageAsync(chatbot, request.Message, "ChatbotsController.StreamChat", streamCt);
        if (effectiveMessage is null)
        {
            await WriteSseEventAsync("error", new { message = "This message appears to contain personal information this tenant's policy doesn't allow sending to the assistant. Please remove it and try again." }, streamCt);
            return;
        }

        string? context = null;
        List<CitationDto>? citations = null;

        if (chatbot.KnowledgeBaseId.HasValue)
        {
            try
            {
                var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                    chatbot.KnowledgeBaseId.Value, effectiveMessage, 1, 5, streamCt, expectedTenantId: chatbot.TenantId);

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
                logger.LogWarning(ex, "Failed to search knowledge base for chatbot {ChatbotId}", id);
            }
        }

        var systemPrompt = chatbot.PromptTemplate ?? "You are a helpful AI assistant.";
        var modelConfig = await modelConfigResolver.ResolveAsync(chatbot.ModelConfigurationId, chatbot.TenantId, streamCt);

        try
        {
            await foreach (var chunk in aiService.StreamChatAsync(effectiveMessage, context, systemPrompt, enableTools: false, modelConfig: modelConfig, ct: streamCt))
            {
                await WriteSseEventAsync("chunk", new { content = chunk }, streamCt);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Same fix as AssistantsController.StreamChat — see its comment for the full
            // reasoning. This is the public, anonymous widget path, so a graceful error here
            // matters even more: an unauthenticated visitor has no other way to know what happened.
            logger.LogWarning(ex, "AI provider failed mid-stream for chatbot {ChatbotId}", id);
            await WriteSseEventAsync("error", new { message = "The AI service failed while generating a response. Please try again." }, ct);
            return;
        }

        if (citations is { Count: > 0 })
        {
            await WriteSseEventAsync("citations", new { citations }, streamCt);
        }

        await IncrementMessagesServedAsync(chatbot.Id, streamCt);
        await WriteSseEventAsync("done", new { message = "Stream complete" }, streamCt);
    }

    public record ChatbotFeedbackRequest(string Rating);

    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 #55. Anonymous like Chat/StreamChat above — same
    // origin/status gate, no auth token exists for a public embed. Not linked to a specific reply:
    // no Message id exists for these anonymous paths to reference (see TotalMessagesServed's own
    // comment), so this is a lifetime tally, not per-message feedback history.
    [HttpPost("{id:guid}/feedback")]
    [AllowAnonymous]
    [EnableCors("PublicChatbotCors")]
    public async Task<IActionResult> SubmitFeedback(Guid id, [FromBody] ChatbotFeedbackRequest request, CancellationToken ct = default)
    {
        if (request.Rating is not ("up" or "down"))
            return BadRequest(new { error = "rating must be \"up\" or \"down\"." });

        var chatbot = await dbContext.Chatbots
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chatbot is null || chatbot.Status != ChatbotStatus.Active)
            return NotFound(new { error = "Chatbot not found" });

        if (!IsRequestOriginAllowed(chatbot))
            return StatusCode(403, new { error = "This origin is not authorized to access this chatbot." });

        // Relational-only atomic increment — see RecordWidgetSeenAsync's comment for why this is
        // guarded rather than unconditional (EF Core InMemory, used by the fast API test suite,
        // doesn't support ExecuteUpdateAsync). A no-op under InMemory is an acceptable trade for a
        // best-effort tally; real deployments are always relational.
        if (dbContext.Database.IsRelational())
        {
            var query = dbContext.Chatbots.IgnoreQueryFilters().Where(c => c.Id == id);
            await (request.Rating == "up"
                ? query.ExecuteUpdateAsync(s => s.SetProperty(c => c.PositiveFeedbackCount, c => c.PositiveFeedbackCount + 1), ct)
                : query.ExecuteUpdateAsync(s => s.SetProperty(c => c.NegativeFeedbackCount, c => c.NegativeFeedbackCount + 1), ct));
        }

        return Ok();
    }

    public record ChatbotAttachmentResponse(string Url, string FileName, string ContentType, long SizeBytes);

    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 #56. Anonymous like Chat/StreamChat/SubmitFeedback
    // above — same origin/status gate. No Message entity exists for these anonymous paths (see
    // TotalMessagesServed's comment), so this does not attach to a specific conversation turn: it
    // uploads the file, returns its URL, and the caller (the widget) includes that URL in the next
    // chat message it sends — the same "attach then reference" shape as most embeddable widgets.
    // Known, accepted limitation, consistent with this endpoint's siblings: no per-embed rate limit
    // beyond the origin allowlist below (ChatbotEmbedDialog already surfaces that gap to admins for
    // Chat/StreamChat; this shares the same boundary, not a new one).
    [HttpPost("{id:guid}/messages/attachment")]
    [AllowAnonymous]
    [EnableCors("PublicChatbotCors")]
    [RequestSizeLimit(MaxAttachmentSize)]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        if (file.Length > MaxAttachmentSize)
            return BadRequest(new { error = $"File size exceeds the {MaxAttachmentSize / (1024 * 1024)} MB limit." });

        if (!AllowedAttachmentContentTypes.Contains(file.ContentType))
            return BadRequest(new { error = $"File type '{file.ContentType}' is not supported." });

        var safeFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName != file.FileName
            || safeFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return BadRequest(new { error = "Invalid file name." });

        var chatbot = await dbContext.Chatbots
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chatbot is null || chatbot.Status != ChatbotStatus.Active)
            return NotFound(new { error = "Chatbot not found" });

        if (!IsRequestOriginAllowed(chatbot))
            return StatusCode(403, new { error = "This origin is not authorized to access this chatbot." });

        // Never trust the client-supplied name for the stored path — a random name sidesteps path
        // traversal/collision entirely regardless of how well safeFileName was validated above; the
        // original name is preserved only in the response, for the widget to display.
        var storedName = $"{Guid.NewGuid()}{Path.GetExtension(safeFileName)}";

        await using var stream = file.OpenReadStream();
        var url = await storageService.UploadFileAsync(stream, storedName, file.ContentType,
            folder: $"chatbot-attachments/{id}", ct);

        logger.LogInformation("Widget attachment uploaded for chatbot {ChatbotId}: {FileName} ({Size} bytes)",
            id, safeFileName, file.Length);

        return Ok(new ChatbotAttachmentResponse(url, safeFileName, file.ContentType, file.Length));
    }

    public record ChatbotWebhookRequest(string Message);
    public record ChatbotWebhookReply(string Reply);

    // Closes the gap the Webhook Key panel on the chatbot detail page used to disclose honestly:
    // a key could be issued and rotated, but no route ever consumed it. This is the generic
    // HTTP channel — send { "message": "..." } with the key in X-Webhook-Key, get { "reply": "..." }
    // back. A specific messaging provider (WhatsApp/Slack/Telegram) still needs its own adapter
    // in front of this to translate that provider's payload shape and verify its own signature;
    // this is the real, working core those adapters would call into.
    //
    // Deliberately NOT X-API-Key: ApiKeyAuthenticationMiddleware inspects that header on every
    // request app-wide and rejects outright (401, before routing) if it doesn't match a real
    // admin-issued ApiKey row — confirmed live, this endpoint's own chatbot-scoped key check
    // never even ran. A distinct header name is what keeps the two key spaces from colliding.
    [HttpPost("{id:guid}/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(Guid id, [FromBody] ChatbotWebhookRequest request, CancellationToken ct = default)
    {
        // IgnoreQueryFilters — see GetPublicInfo above for why this is required, not just safe. The
        // real security boundary here is the fixed-time webhook-key hash comparison a few lines down.
        var chatbot = await dbContext.Chatbots
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chatbot is null || chatbot.Status != ChatbotStatus.Active)
            return NotFound(new { error = "Chatbot not found" });

        if (string.IsNullOrEmpty(chatbot.WebhookApiKeyHash))
            return Unauthorized(new { error = "This chatbot has no webhook key configured." });

        if (!Request.Headers.TryGetValue("X-Webhook-Key", out var providedKey) || string.IsNullOrWhiteSpace(providedKey))
            return Unauthorized(new { error = "Missing X-Webhook-Key header." });

        var providedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(providedKey.ToString())));
        var storedHashBytes = Encoding.UTF8.GetBytes(chatbot.WebhookApiKeyHash);
        var providedHashBytes = Encoding.UTF8.GetBytes(providedHash);
        if (storedHashBytes.Length != providedHashBytes.Length
            || !CryptographicOperations.FixedTimeEquals(storedHashBytes, providedHashBytes))
        {
            logger.LogWarning("Invalid webhook API key attempt for chatbot {ChatbotId}", id);
            return Unauthorized(new { error = "Invalid API key." });
        }

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        // Policy Engine: same tenant-configured daily request cap enforced on the widget-facing
        // chat endpoints above — this route is just as capable of driving real AI spend.
        if (!await aiUsagePolicyService.IsUnderCapAsync(chatbot.TenantId, ct))
        {
            logger.LogWarning("Denied chatbot webhook message for tenant {TenantId} — exceeds this tenant's configured AiUsage policy cap", chatbot.TenantId);
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), chatbot.TenantId, AuditAction.Execute, "GlobalPolicy",
                "AiUsage", userId: null, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "exceeds daily AiUsage cap", source = "ChatbotsController.Webhook", chatbotId = id })),
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            return StatusCode(429, new { error = "This service has reached its configured daily usage limit. Please try again tomorrow." });
        }
        await aiUsagePolicyService.RecordRequestAsync(chatbot.TenantId, ct);

        var effectiveMessage = await CheckPiiAndGetEffectiveMessageAsync(chatbot, request.Message, "ChatbotsController.Webhook", ct);
        if (effectiveMessage is null)
            return StatusCode(451, new { error = "This message appears to contain personal information this tenant's policy doesn't allow sending to the assistant." });

        var context = await SearchKnowledgeBaseContextAsync(chatbot, effectiveMessage, ct);
        var systemPrompt = chatbot.PromptTemplate ?? "You are a helpful AI assistant.";
        var modelConfig = await modelConfigResolver.ResolveAsync(chatbot.ModelConfigurationId, chatbot.TenantId, ct);
        // enableTools stays false: same rationale as Chat above — a message arriving from an
        // external channel is no more trusted than an anonymous widget visitor.
        var reply = await aiService.ChatAsync(effectiveMessage, context, systemPrompt, enableTools: false, modelConfig: modelConfig, ct: ct);
        await IncrementMessagesServedAsync(chatbot.Id, ct);

        return Ok(new ChatbotWebhookReply(reply));
    }

    private async Task WriteSseEventAsync(string eventType, object data, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var sseMessage = $"event: {eventType}\ndata: {json}\n\n";
        await Response.WriteAsync(sseMessage, ct);
        await Response.Body.FlushAsync(ct);
    }
}
