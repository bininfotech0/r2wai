using System.Text.Json;
using R2WAI.Infrastructure.AI.Memory;
using R2WAI.Infrastructure.AI.ModelGateway;

namespace R2WAI.Infrastructure.Services.BackgroundJobs;

/// <summary>
/// The opt-in learning pass: reads one finished chat turn and saves up to three durable things the
/// user said about themselves into their Personal partition, held for review unless the user turned
/// review off. Runs in the background so it never adds to a reply's latency.
///
/// Runs without an ambient tenant, so every query filters by the payload's tenant and user
/// explicitly with the global filters off. The model is the one that answered the turn, resolved
/// with the same tenant and data-classification checks as a chat turn: the learning pass must not
/// send a turn to a provider the chat itself would not have used.
///
/// Conservative by design: a candidate with personal identifiers is dropped; a memory the user wrote
/// is never overwritten; with review on, an existing memory is never changed silently.
/// </summary>
public sealed class LearnUserMemoryJobHandler(
    ApplicationDbContext db,
    IAIService aiService,
    IEncryptionService encryption,
    ILogger<LearnUserMemoryJobHandler> logger) : IBackgroundJobHandler
{
    // Room for a reasoning model's thinking plus three short items; bounds what one pass can cost.
    private const int MaxOutputTokens = 1500;

    public string JobType => BackgroundJobTypes.LearnUserMemory;

    public async Task HandleAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<LearnUserMemoryJobPayload>(payloadJson)
            ?? throw new InvalidOperationException($"Malformed {nameof(LearnUserMemoryJobPayload)}");

        var settings = await db.UserMemorySettings.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == payload.UserId && s.TenantId == payload.TenantId && !s.IsDeleted, ct);
        // Re-checked here, not only at enqueue: switching learning off stops turns already queued.
        if (settings is not { MemoryEnabled: true, AutoLearnEnabled: true })
            return;

        var all = await db.UserMemories.IgnoreQueryFilters()
            .Where(m => m.UserId == payload.UserId && m.TenantId == payload.TenantId && !m.IsDeleted)
            .ToListAsync(ct);
        if (all.Count >= UserMemoryService.MaxMemoriesPerUser)
            return;
        var personal = all.Where(m => m.AssistantId == null).ToList();

        var prompt = UserMemoryPrompt.BuildLearningPrompt(
            personal.OrderByDescending(m => m.ModifiedAt ?? m.CreatedAt).Take(50).ToList(),
            payload.UserMessage, payload.Reply);
        var modelConfig = await ResolveModelAsync(payload.TenantId, payload.ModelConfigurationId, ct);
        string raw;
        try
        {
            raw = await aiService.GenerateResponseAsync(prompt, modelConfig: modelConfig, maxTokens: MaxOutputTokens, temperature: 0.1, ct: ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Best-effort: a missed turn costs nothing, while retrying would queue the same slow call
            // again on a model the user's live chats are waiting for (one local CPU model, say).
            logger.LogWarning(ex, "Memory learning for user {UserId} skipped: the model call failed", payload.UserId);
            return;
        }
        var candidates = UserMemoryPrompt.ParseLearned(raw);

        var status = settings.ReviewLearned ? UserMemoryStatus.PendingReview : UserMemoryStatus.Active;
        var added = 0;
        var updated = 0;
        foreach (var candidate in candidates)
        {
            if (PiiDetector.ContainsPii(candidate.Content) || PiiDetector.ContainsPii(candidate.Key))
                continue;
            if (personal.Any(m => string.Equals(m.Content, candidate.Content, StringComparison.OrdinalIgnoreCase)))
                continue;

            var sameKey = candidate.Key is null ? null : personal.FirstOrDefault(m => m.Key == candidate.Key);
            if (sameKey is not null)
            {
                if (sameKey.Source != UserMemorySource.Learned || settings.ReviewLearned)
                    continue;
                sameKey.Update(candidate.Category, candidate.Key, candidate.Content);
                updated++;
                continue;
            }

            if (all.Count + added >= UserMemoryService.MaxMemoriesPerUser)
                break;

            var memory = new UserMemory(Guid.NewGuid(), payload.TenantId, payload.UserId, null, candidate.Category,
                candidate.Key, candidate.Content, UserMemorySource.Learned, status, payload.ConversationId);
            db.UserMemories.Add(memory);
            personal.Add(memory);
            added++;
        }

        if (added + updated > 0)
            await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Memory learning for user {UserId}: {Candidates} candidate(s), {Added} added ({Status}), {Updated} updated",
            payload.UserId, candidates.Count, added, status, updated);
    }

    // Mirrors ModelConfigurationResolver.ResolveAsync, which can't be used here: its repository
    // applies the ambient-tenant filter, and a background job has no ambient tenant.
    private async Task<ResolvedModelConfig?> ResolveModelAsync(Guid tenantId, Guid? modelConfigurationId, CancellationToken ct)
    {
        var configs = db.ModelConfigurations.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive && !m.IsDeleted);
        var config = modelConfigurationId is { } id
            ? await configs.FirstOrDefaultAsync(m => m.Id == id, ct)
            : await configs.FirstOrDefaultAsync(m => m.IsDefault, ct);
        return config is null ? null : ModelConfigurationResolver.ToResolved(config, encryption, logger);
    }
}
