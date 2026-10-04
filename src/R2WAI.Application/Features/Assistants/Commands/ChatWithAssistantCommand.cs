using FluentValidation;

namespace R2WAI.Application.Features.Assistants.Commands;

public record ChatWithAssistantCommand : IRequest<ChatWithAssistantResult>
{
    public Guid AssistantId { get; init; }
    public string Message { get; init; } = string.Empty;
    public Guid? ConversationId { get; init; }
}

public record ChatWithAssistantResult(
    Guid ConversationId,
    string Reply,
    int TokensUsed,
    List<CitationDto>? Citations = null,
    long DurationMs = 0,
    List<FunctionCallTraceDto>? FunctionCalls = null);

public record CitationDto(string SourceName, string Content, float Score, int Index);

public class ChatWithAssistantCommandValidator : AbstractValidator<ChatWithAssistantCommand>
{
    public ChatWithAssistantCommandValidator()
    {
        RuleFor(v => v.AssistantId).NotEmpty();
        RuleFor(v => v.Message).NotEmpty().MaximumLength(10000);
    }
}

public class ChatWithAssistantCommandHandler(
    IRepository<AssistantDefinition> assistantRepo,
    IRepository<Conversation> conversationRepo,
    IRepository<Message> messageRepo,
    IRepository<KnowledgeBase> kbRepo,
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
    ILogger<ChatWithAssistantCommandHandler> logger) : IRequestHandler<ChatWithAssistantCommand, ChatWithAssistantResult>
{
    public async Task<ChatWithAssistantResult> Handle(ChatWithAssistantCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        traceCollector.Clear();
        var assistant = await assistantRepo.GetByIdAsync(command.AssistantId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.AssistantId);

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        // Draft/Archived assistants are configuration-in-progress, not yet released — only Admin/
        // SystemAdmin (who manage assistants) may chat with them. Published is the only status a
        // plain User can reach; this was previously unchecked, so any authenticated user who knew or
        // guessed a Draft assistant's ID could chat with it.
        if (assistant.PublishStatus != PublishStatus.Published
            && !currentUser.Roles.Contains("Admin") && !currentUser.Roles.Contains("SystemAdmin"))
        {
            throw new UnauthorizedAccessException($"Assistant '{assistant.Name}' is not published.");
        }

        // Policy Engine: an optional, tenant-configured daily request cap — additive tightening, a
        // tenant with no "AiUsage" policy configured sees byte-identical behavior. Checked before any
        // conversation/message is persisted or the AI is actually called, so a denied request costs
        // nothing and doesn't leave a half-formed conversation behind.
        if (!await aiUsagePolicyService.IsUnderCapAsync(tenantId, cancellationToken))
        {
            logger.LogWarning("Denied chat request for tenant {TenantId} — exceeds this tenant's configured AiUsage policy cap", tenantId);
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "GlobalPolicy",
                "AiUsage", userId, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "exceeds daily AiUsage cap" })),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new ChatWithAssistantResult(command.ConversationId ?? Guid.Empty,
                "This tenant has reached its configured daily AI usage limit. Please try again tomorrow, or ask an administrator to raise the limit.",
                0);
        }
        await aiUsagePolicyService.RecordRequestAsync(tenantId, cancellationToken);

        // Policy Engine: an optional, tenant-configured PII rule — same additive-tightening
        // philosophy as AiUsage above. "block" denies before anything is persisted; "redact"
        // replaces the message everywhere downstream (persisted history, KB search, and what
        // actually reaches the AI provider) with the scrubbed version, so raw Aadhaar/PAN/phone/
        // email text never leaves this handler once a tenant has opted in.
        var piiResult = await piiPolicyService.CheckAsync(command.Message, tenantId, cancellationToken);
        if (piiResult.Blocked)
        {
            logger.LogWarning("Denied chat request for tenant {TenantId} — message contains PII ({Types}) and this tenant's policy blocks it",
                tenantId, string.Join(", ", piiResult.DetectedTypes));
            await auditLogRepo.AddAsync(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Execute, "GlobalPolicy",
                "Pii", userId, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "denied", reason = "message contains PII", types = piiResult.DetectedTypes })),
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
                "Pii", userId, metadata: System.Text.Json.JsonSerializer.Serialize(new { status = "redacted", types = piiResult.DetectedTypes })),
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
                $"Chat with {assistant.Name}",
                "assistant",
                assistant.Id);
            await conversationRepo.AddAsync(conversation, cancellationToken);
        }

        var userMessage = conversation.AddMessage(Guid.NewGuid(), null, MessageRole.User, effectiveMessage);
        userMessage.AddDomainEvent(new MessageCreatedEvent(
            userMessage.Id, conversation.Id, tenantId, userId, effectiveMessage, MessageRole.User));
        // Existing conversations are loaded (tracked Unchanged), not Add()-ed: EF Core cannot tell a
        // client-generated-Guid child discovered only via navigation fixup is new rather than existing,
        // and defaults to Modified — which throws DbUpdateConcurrencyException (0 rows) on save.
        await messageRepo.AddAsync(userMessage, cancellationToken);

        // Policy Engine-style additive layer, same as every other "configured but was never
        // consulted" gap this pass found: the Behavior tab's response-style/length/clarification
        // knobs and temperature/max-tokens overrides previously had zero runtime effect.
        var behaviorSettings = assistant.GetBehaviorSettings();
        var modelConfig = await modelConfigResolver.ResolveAsync(assistant.ModelConfigurationId, tenantId, cancellationToken);
        if (modelConfig is not null && behaviorSettings is not null && (behaviorSettings.Temperature.HasValue || behaviorSettings.MaxOutputTokens.HasValue))
            modelConfig = modelConfig with
            {
                Temperature = behaviorSettings.Temperature ?? modelConfig.Temperature,
                MaxTokens = behaviorSettings.MaxOutputTokens ?? modelConfig.MaxTokens
            };

        string? context = null;
        List<CitationDto>? citations = null;
        var insufficientEvidenceNotice = false;
        if (assistant.KnowledgeBaseId.HasValue)
        {
            try
            {
                IReadOnlyList<SearchResultDto> items;
                // R2WAI 2.0 §6 — opt-in per assistant (Behavior tab). Standard mode's single search
                // call is untouched below; Agentic mode adds the bounded evidence-check-then-
                // rewrite-once pass from IAgenticRetrievalOrchestrator on top of the same search.
                if (behaviorSettings?.RetrievalMode == "Agentic")
                {
                    var retrieval = await agenticRetrieval.RetrieveAsync(
                        assistant.KnowledgeBaseId.Value, effectiveMessage, modelConfig, cancellationToken);
                    items = retrieval.Items;
                    insufficientEvidenceNotice = !retrieval.EvidenceSufficient;
                    logger.LogInformation(
                        "Agentic RAG for assistant {AssistantId}: {Iterations} iteration(s), sufficient={Sufficient}, queries={Queries}",
                        assistant.Id, retrieval.IterationsUsed, retrieval.EvidenceSufficient, string.Join(" -> ", retrieval.QueriesTried));
                }
                else
                {
                    var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                        assistant.KnowledgeBaseId.Value, effectiveMessage, 1, 5, cancellationToken);
                    items = searchResult.Items;
                }

                if (items.Count > 0)
                {
                    context = string.Join("\n\n", items.Select(i => i.Content));
                    citations = items
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
                logger.LogWarning(ex, "Failed to search knowledge base {KBId} for assistant {AssistantId}",
                    assistant.KnowledgeBaseId.Value, assistant.Id);
            }
        }

        var basePrompt = assistant.SystemPrompt
            ?? await promptTemplateService.GetActiveTemplateAsync(assistant.Type, tenantId, cancellationToken);
        var systemPrompt = basePrompt
            + $"\n\n[Assistant context: your assistant ID is {assistant.Id}, your name is \"{assistant.Name}\"" +
              (!string.IsNullOrWhiteSpace(assistant.Description)
                  ? $", and your purpose is: {assistant.Description}"
                  : $", and you are a {assistant.Type} assistant") +
              ". This is everything you need to know about yourself — you do not need to call get_assistant_context " +
              "to answer questions about what you do or who you are. If you need an assistant ID for some other tool, " +
              "use the ID above directly, never guess or leave it blank.]";

        // R2WAI 2.0 §6.3 — "if evidence is insufficient, the agent must state that it cannot answer
        // reliably". A real instruction, not a UI label: only added when Agentic mode actually ran
        // its bounded retries and still came up short, so it never fires for Standard mode or for a
        // confident Agentic match.
        if (insufficientEvidenceNotice)
            systemPrompt += "\n\n[Retrieval note: the knowledge base search for this question, including a " +
                "rewritten retry, did not turn up a confident match. If the context below does not clearly " +
                "answer the question, say so plainly rather than guessing — do not invent facts, sources, or details.]";

        var behaviorAddendum = behaviorSettings?.BuildPromptAddendum();
        if (!string.IsNullOrEmpty(behaviorAddendum))
            systemPrompt += $"\n\n[Response style: {behaviorAddendum}]";
        if (behaviorSettings?.CitationsEnabled == false)
            citations = null;

        var reply = await aiService.ChatAsync(
            effectiveMessage,
            context,
            systemPrompt,
            enableTools: true,
            modelConfig: modelConfig,
            enabledToolIds: assistant.GetEnabledToolIds(),
            ct: cancellationToken);

        var assistantMessage = conversation.AddMessage(Guid.NewGuid(), null, MessageRole.Assistant, reply);
        assistantMessage.AddDomainEvent(new MessageCreatedEvent(
            assistantMessage.Id, conversation.Id, tenantId, userId, reply, MessageRole.Assistant));
        await messageRepo.AddAsync(assistantMessage, cancellationToken);
        assistant.IncrementUsageCount();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        stopwatch.Stop();
        var functionCalls = traceCollector.GetTrace().ToList();

        return new ChatWithAssistantResult(conversation.Id, reply, 0, citations, stopwatch.ElapsedMilliseconds, functionCalls);
    }
}
