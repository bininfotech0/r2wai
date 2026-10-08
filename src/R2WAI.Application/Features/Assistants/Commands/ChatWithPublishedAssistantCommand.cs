using FluentValidation;
using R2WAI.Application.Common.AI;

namespace R2WAI.Application.Features.Assistants.Commands;

/// <summary>
/// The standalone "publish an assistant as its own REST endpoint" path (implementation plan
/// Phase 1) — deliberately separate from <see cref="ChatWithAssistantCommand"/>, which always
/// resolves the *live* AssistantDefinition. This command requires a real, published
/// <see cref="AssistantVersion"/> and resolves every piece of runtime config (system prompt, model,
/// knowledge base, tools, behavior settings) from that version's immutable
/// <see cref="AssistantVersion.ConfigSnapshot"/> — so editing the live assistant after publishing
/// never silently changes what this endpoint serves. Mirrors ChatWithAssistantCommandHandler's
/// pipeline (PII policy, usage cap, agentic RAG, tool-enabled chat, conversation persistence) as
/// closely as the snapshot's captured fields allow.
/// </summary>
public record ChatWithPublishedAssistantCommand : IRequest<ChatWithAssistantResult>
{
    public Guid AssistantId { get; init; }
    public string Message { get; init; } = string.Empty;
    public Guid? ConversationId { get; init; }
}

public class ChatWithPublishedAssistantCommandValidator : AbstractValidator<ChatWithPublishedAssistantCommand>
{
    public ChatWithPublishedAssistantCommandValidator()
    {
        RuleFor(v => v.AssistantId).NotEmpty();
        RuleFor(v => v.Message).NotEmpty().MaximumLength(10000);
    }
}

public class ChatWithPublishedAssistantCommandHandler(
    IRepository<AssistantDefinition> assistantRepo,
    IRepository<AssistantVersion> versionRepo,
    IRepository<Conversation> conversationRepo,
    IRepository<Message> messageRepo,
    IKnowledgeBaseService knowledgeBaseService,
    IAgenticRetrievalOrchestrator agenticRetrieval,
    IAIService aiService,
    IPromptTemplateService promptTemplateService,
    IModelConfigurationResolver modelConfigResolver,
    IAiUsagePolicyService aiUsagePolicyService,
    IPiiPolicyService piiPolicyService,
    IRepository<AuditLog> auditLogRepo,
    IChatTraceCollector traceCollector,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    ILogger<ChatWithPublishedAssistantCommandHandler> logger,
    IPromptRenderer? promptRenderer = null) : IRequestHandler<ChatWithPublishedAssistantCommand, ChatWithAssistantResult>
{
    public async Task<ChatWithAssistantResult> Handle(ChatWithPublishedAssistantCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        traceCollector.Clear();

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var assistant = await assistantRepo.GetByIdAsync(command.AssistantId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.AssistantId);

        if (assistant.TenantId != tenantId)
            throw new NotFoundException(nameof(AssistantDefinition), command.AssistantId);

        // The real gate for this endpoint: a published AssistantVersion, not AssistantDefinition's
        // own PublishStatus flag. The two are independent today — PublishStatus.Published (set by
        // POST /assistants/{id}/publish) is a simple counter with no snapshot behind it, while only
        // an AssistantVersion row (created via CreateAssistantVersionCommand with Publish=true) has
        // the immutable config this endpoint needs to serve safely. An assistant can be
        // "Published" in the library UI without ever having one — that combination is rejected here
        // rather than silently falling back to live config, which would defeat the whole point.
        var versions = await versionRepo.FindAsync(
            v => v.AssistantDefinitionId == assistant.Id && v.IsPublished, cancellationToken);
        var publishedVersion = versions.FirstOrDefault()
            ?? throw new ValidationException("assistantId",
                $"Assistant '{assistant.Name}' has no published version. Publish a version before calling it as an API.");

        var snapshot = AssistantVersionSnapshotService.Deserialize(publishedVersion.ConfigSnapshot);

        if (!await aiUsagePolicyService.IsUnderCapAsync(tenantId, cancellationToken))
        {
            logger.LogWarning("Denied published-assistant chat for tenant {TenantId} — exceeds this tenant's configured AiUsage policy cap", tenantId);
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "GlobalPolicy",
                "AiUsage", userId, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "exceeds daily AiUsage cap", source = "PublishedAssistant" })),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ChatWithAssistantResult(command.ConversationId ?? Guid.Empty,
                "This tenant has reached its configured daily AI usage limit. Please try again tomorrow, or ask an administrator to raise the limit.",
                0);
        }
        await aiUsagePolicyService.RecordRequestAsync(tenantId, cancellationToken);

        var piiResult = await piiPolicyService.CheckAsync(command.Message, tenantId, cancellationToken);
        if (piiResult.Blocked)
        {
            logger.LogWarning("Denied published-assistant chat for tenant {TenantId} — message contains PII ({Types}) and this tenant's policy blocks it",
                tenantId, string.Join(", ", piiResult.DetectedTypes));
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "GlobalPolicy",
                "Pii", userId, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "message contains PII", types = piiResult.DetectedTypes, source = "PublishedAssistant" })),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ChatWithAssistantResult(command.ConversationId ?? Guid.Empty,
                "This message appears to contain personal information this tenant's policy doesn't allow sending to the assistant. Please remove it and try again.",
                0);
        }
        var effectiveMessage = piiResult.ProcessedText;
        if (piiResult.DetectedTypes.Count > 0)
        {
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "GlobalPolicy",
                "Pii", userId, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "redacted", types = piiResult.DetectedTypes, source = "PublishedAssistant" })),
                cancellationToken);
        }

        Conversation conversation;
        if (command.ConversationId.HasValue)
        {
            conversation = await conversationRepo.GetByIdAsync(command.ConversationId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Conversation), command.ConversationId.Value);
        }
        else
        {
            conversation = new Conversation(
                Guid.NewGuid(), tenantId, userId,
                $"Published API chat with {assistant.Name}",
                "published-assistant",
                assistant.Id);
            await conversationRepo.AddAsync(conversation, cancellationToken);
        }

        var userMessage = conversation.AddMessage(Guid.NewGuid(), null, MessageRole.User, effectiveMessage);
        userMessage.AddDomainEvent(new MessageCreatedEvent(
            userMessage.Id, conversation.Id, tenantId, userId, effectiveMessage, MessageRole.User));
        await messageRepo.AddAsync(userMessage, cancellationToken);

        var behaviorSettings = AssistantVersionSnapshotService.ParseBehaviorSettings(snapshot.Settings);
        var modelConfig = await modelConfigResolver.ResolveAsync(snapshot.ModelConfigurationId, tenantId, cancellationToken);
        if (modelConfig is not null && behaviorSettings is not null && (behaviorSettings.Temperature.HasValue || behaviorSettings.MaxOutputTokens.HasValue))
            modelConfig = modelConfig with
            {
                Temperature = behaviorSettings.Temperature ?? modelConfig.Temperature,
                MaxTokens = behaviorSettings.MaxOutputTokens ?? modelConfig.MaxTokens
            };

        string? context = null;
        List<CitationDto>? citations = null;
        var insufficientEvidenceNotice = false;
        if (snapshot.KnowledgeBaseId.HasValue)
        {
            try
            {
                IReadOnlyList<KnowledgeBases.DTOs.SearchResultDto> items;
                if (behaviorSettings?.RetrievalMode == "Agentic")
                {
                    var retrieval = await agenticRetrieval.RetrieveAsync(
                        snapshot.KnowledgeBaseId.Value, effectiveMessage, modelConfig, cancellationToken);
                    items = retrieval.Items;
                    insufficientEvidenceNotice = !retrieval.EvidenceSufficient;
                }
                else
                {
                    var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                        snapshot.KnowledgeBaseId.Value, effectiveMessage, 1, 5, cancellationToken);
                    items = searchResult.Items;
                }

                if (items.Count > 0)
                {
                    context = string.Join("\n\n", items.Select(i => i.Content));
                    citations = items
                        .Select((item, index) => new CitationDto(item.SourceName ?? "Unknown", item.Content, (float)item.Score, index + 1))
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to search knowledge base {KBId} for published assistant {AssistantId}",
                    snapshot.KnowledgeBaseId.Value, assistant.Id);
            }
        }

        var basePrompt = snapshot.SystemPrompt
            ?? await promptTemplateService.GetActiveTemplateAsync(snapshot.Type, tenantId, cancellationToken);
        // Fill {{tenant.name}}-style placeholders an admin wrote into the prompt; the model used to
        // receive them as literal braces.
        if (promptRenderer is not null)
            basePrompt = await promptRenderer.RenderAsync(basePrompt, tenantId, assistant.Name, cancellationToken);
        var systemPrompt = basePrompt
            + $"\n\n[Assistant context: your assistant ID is {assistant.Id}, your name is \"{snapshot.Name}\""
            + (!string.IsNullOrWhiteSpace(snapshot.Description)
                ? $", and your purpose is: {snapshot.Description}"
                : $", and you are a {snapshot.Type} assistant")
            + ". This is everything you need to know about yourself.]";

        if (insufficientEvidenceNotice)
            systemPrompt += "\n\n[Retrieval note: the knowledge base search for this question, including a " +
                "rewritten retry, did not turn up a confident match. If the context below does not clearly " +
                "answer the question, say so plainly rather than guessing — do not invent facts, sources, or details.]";

        var behaviorAddendum = behaviorSettings?.BuildPromptAddendum();
        if (!string.IsNullOrEmpty(behaviorAddendum))
            systemPrompt += $"\n\n[Response style: {behaviorAddendum}]";
        if (behaviorSettings?.CitationsEnabled == false)
            citations = null;

        string reply;
        int? tokensUsed;
        using (var usage = AiTokenUsageScope.Begin())
        {
            reply = await aiService.ChatAsync(
                effectiveMessage,
                context,
                systemPrompt,
                enableTools: true,
                modelConfig: modelConfig,
                enabledToolIds: AssistantVersionSnapshotService.ParseEnabledToolIds(snapshot.Tools),
                ct: cancellationToken);
            tokensUsed = usage.TotalTokens;
        }

        var assistantMessage = conversation.AddMessage(Guid.NewGuid(), null, MessageRole.Assistant, reply, tokensUsed: tokensUsed);
        assistantMessage.AddDomainEvent(new MessageCreatedEvent(
            assistantMessage.Id, conversation.Id, tenantId, userId, reply, MessageRole.Assistant));
        await messageRepo.AddAsync(assistantMessage, cancellationToken);
        // Same counter the draft-chat path bumps — without it, published traffic never showed up in
        // the dashboard's per-agent usage ranking.
        assistant.IncrementUsageCount();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        stopwatch.Stop();
        var functionCalls = traceCollector.GetTrace().ToList();

        return new ChatWithAssistantResult(conversation.Id, reply, tokensUsed ?? 0, citations, stopwatch.ElapsedMilliseconds, functionCalls);
    }
}
