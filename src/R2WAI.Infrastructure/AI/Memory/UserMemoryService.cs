namespace R2WAI.Infrastructure.AI.Memory;

public sealed class UserMemoryScope : IUserMemoryScope
{
    public Guid? AssistantId { get; set; }
    public bool AgentNotesEnabled { get; set; }
}

/// <summary>
/// Memory belongs to a person, so it only applies to a person signed in interactively. An API-key
/// request runs as the key's creator (ApiKeyAuthenticationMiddleware) on behalf of whoever is calling
/// the integration, so it neither sees nor writes that creator's memories, and can't manage them.
/// </summary>
public sealed class UserMemoryService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IUserMemoryScope scope,
    IBackgroundJobQueue jobQueue,
    TimeProvider clock,
    IHttpContextAccessor httpContextAccessor,
    ILogger<UserMemoryService> logger) : IUserMemoryService
{
    /// <summary>Across every partition. Keeps the Memory page and the prompt block bounded.</summary>
    public const int MaxMemoriesPerUser = 200;
    public const int MaxNotesPerAgent = 50;
    private const int MaxPromptItems = 40;
    private const int MaxLearningInputChars = 4000;

    private const string PiiRejection =
        "Memories can't contain Aadhaar or PAN numbers, phone numbers or email addresses.";

    private UserMemorySettings? _settings;

    public async Task<string?> PrepareTurnAsync(Guid? assistantId, AssistantBehaviorSettings? agentSettings, CancellationToken ct = default)
    {
        scope.AssistantId = assistantId;
        scope.AgentNotesEnabled = false;

        if (PersonId is not { } userId || !AssistantBehaviorSettings.ReadsUserMemory(agentSettings))
            return null;

        try
        {
            var settings = await GetSettingsAsync(userId, ct);
            if (!settings.MemoryEnabled) return null;

            var notesEnabled = assistantId is not null && settings.AgentNotesEnabled
                && AssistantBehaviorSettings.WritesUserMemory(agentSettings);

            var memories = await db.UserMemories.AsNoTracking()
                .Where(m => m.UserId == userId && m.Status == UserMemoryStatus.Active
                    && (m.AssistantId == null || (assistantId != null && m.AssistantId == assistantId)))
                .OrderByDescending(m => m.IsPinned)
                .ThenByDescending(m => m.ModifiedAt ?? m.CreatedAt)
                .Take(MaxPromptItems)
                .ToListAsync(ct);

            if (memories.Count > 0)
            {
                // A bulk update, so reading memory never adds a row to the audit log on every turn.
                var ids = memories.Select(m => m.Id).ToList();
                var now = clock.GetUtcNow().UtcDateTime;
                await db.UserMemories.Where(m => ids.Contains(m.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.LastUsedAt, now), ct);
            }

            scope.AgentNotesEnabled = notesEnabled;
            return UserMemoryPrompt.BuildBlock(memories, notesEnabled);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Memory personalises an answer; it is never a reason to fail one.
            logger.LogWarning(ex, "Failed to load user memory for user {UserId}; answering without it", userId);
            scope.AgentNotesEnabled = false;
            return null;
        }
    }

    public async Task QueueLearningAsync(Guid? assistantId, AssistantBehaviorSettings? agentSettings, Guid? modelConfigurationId,
        Guid? conversationId, string userMessage, string reply, CancellationToken ct = default)
    {
        if (PersonId is not { } userId || currentUser.TenantId is not { } tenantId) return;
        if (!AssistantBehaviorSettings.ReadsUserMemory(agentSettings) || string.IsNullOrWhiteSpace(userMessage)) return;

        try
        {
            var settings = await GetSettingsAsync(userId, ct);
            if (!settings.MemoryEnabled || !settings.AutoLearnEnabled) return;

            await jobQueue.EnqueueAsync(BackgroundJobTypes.LearnUserMemory,
                new LearnUserMemoryJobPayload(tenantId, userId, assistantId, conversationId, modelConfigurationId,
                    ForPayload(userMessage), ForPayload(reply)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to queue memory learning for user {UserId}", userId);
        }
    }

    public async Task<UserMemoryOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        var settings = await GetSettingsAsync(userId, ct);

        var items = await db.UserMemories.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.IsPinned)
            .ThenByDescending(m => m.ModifiedAt ?? m.CreatedAt)
            .ToListAsync(ct);

        var agentIds = items.Where(m => m.AssistantId != null).Select(m => m.AssistantId!.Value).Distinct().ToList();
        var names = agentIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.AssistantDefinitions.AsNoTracking()
                .Where(a => agentIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var partitions = new List<UserMemoryPartitionDto>
        {
            new(null, null,
                items.Count(m => m.AssistantId == null),
                items.Count(m => m.AssistantId == null && m.Status == UserMemoryStatus.PendingReview)),
        };
        partitions.AddRange(agentIds
            .Select(id => new UserMemoryPartitionDto(id, names.GetValueOrDefault(id),
                items.Count(m => m.AssistantId == id),
                items.Count(m => m.AssistantId == id && m.Status == UserMemoryStatus.PendingReview)))
            .OrderBy(p => p.AssistantName ?? "￿"));

        return new UserMemoryOverviewDto(UserMemorySettingsDto.From(settings), partitions,
            items.Select(UserMemoryDto.From).ToList(), MaxMemoriesPerUser);
    }

    /// <summary>Manual memories always land in the Personal partition: agent partitions are the agents' to write.</summary>
    public async Task<UserMemoryDto> CreateAsync(SaveUserMemoryRequest request, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var (key, content) = Validate(request);

        if (await db.UserMemories.CountAsync(m => m.UserId == userId, ct) >= MaxMemoriesPerUser)
            throw new ValidationException("Content",
                $"You can keep up to {MaxMemoriesPerUser} memories. Delete some you no longer need first.");
        await EnsureKeyIsFreeAsync(userId, null, key, null, ct);

        var memory = new UserMemory(Guid.NewGuid(), tenantId, userId, null, request.Category, key, content,
            UserMemorySource.Manual, request.Paused ? UserMemoryStatus.Paused : UserMemoryStatus.Active,
            isPinned: request.IsPinned);
        db.UserMemories.Add(memory);
        await db.SaveChangesAsync(ct);
        return UserMemoryDto.From(memory);
    }

    public async Task<UserMemoryDto> UpdateAsync(Guid id, SaveUserMemoryRequest request, CancellationToken ct = default)
    {
        var memory = await FindOwnAsync(id, ct);
        var (key, content) = Validate(request);
        await EnsureKeyIsFreeAsync(memory.UserId, memory.AssistantId, key, memory.Id, ct);

        memory.Update(request.Category, key, content);
        memory.SetPinned(request.IsPinned);
        memory.SetPaused(request.Paused);
        await db.SaveChangesAsync(ct);
        return UserMemoryDto.From(memory);
    }

    public async Task<UserMemoryDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var memory = await FindOwnAsync(id, ct);
        memory.Approve();
        await db.SaveChangesAsync(ct);
        return UserMemoryDto.From(memory);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var memory = await FindOwnAsync(id, ct);
        db.UserMemories.Remove(memory);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> ClearAsync(string partition, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        var query = db.UserMemories.Where(m => m.UserId == userId);
        query = partition.ToLowerInvariant() switch
        {
            "all" => query,
            "personal" => query.Where(m => m.AssistantId == null),
            "pending" => query.Where(m => m.Status == UserMemoryStatus.PendingReview),
            _ when Guid.TryParse(partition, out var assistantId) => query.Where(m => m.AssistantId == assistantId),
            _ => throw new ValidationException("partition",
                "Use 'all', 'personal', 'pending' or an agent's id."),
        };

        var removed = await query.ExecuteDeleteAsync(ct);

        // A bulk delete skips the per-row audit in SaveChanges; record that it happened (never what was removed).
        db.AuditLogs.Add(new AuditLog(Guid.NewGuid(), tenantId, AuditAction.Delete, nameof(UserMemory), partition, userId,
            metadata: System.Text.Json.JsonSerializer.Serialize(new { removed })));
        await db.SaveChangesAsync(ct);
        return removed;
    }

    public async Task<UserMemorySettingsDto> UpdateSettingsAsync(UserMemorySettingsDto settings, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        var row = await db.UserMemorySettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (row is null)
        {
            row = new UserMemorySettings(Guid.NewGuid(), tenantId, userId);
            db.UserMemorySettings.Add(row);
        }
        row.Update(settings.MemoryEnabled, settings.AutoLearnEnabled, settings.ReviewLearned, settings.AgentNotesEnabled);
        await db.SaveChangesAsync(ct);
        _settings = row;
        return UserMemorySettingsDto.From(row);
    }

    public async Task<UserMemoryExportDto> ExportAsync(CancellationToken ct = default)
    {
        var overview = await GetOverviewAsync(ct);
        return new UserMemoryExportDto(clock.GetUtcNow().UtcDateTime, overview.Settings, overview.Items);
    }

    public async Task<string> RememberAsync(string key, string note, string? category, CancellationToken ct = default)
    {
        if (await GetAgentNotesContextAsync(ct) is not { } notesContext)
            return "Notes are not available in this conversation.";
        var (tenantId, userId, assistantId) = notesContext;

        var normalizedKey = UserMemory.NormalizeKey(key);
        if (normalizedKey is null) return "Not saved: give the note a short snake_case key.";
        var content = note?.Trim() ?? string.Empty;
        if (content.Length == 0) return "Not saved: the note is empty.";
        if (content.Length > UserMemory.MaxContentLength)
            return $"Not saved: keep notes under {UserMemory.MaxContentLength} characters.";
        if (PiiDetector.ContainsPii(content) || PiiDetector.ContainsPii(normalizedKey))
            return "Not saved: notes can't contain ID numbers, phone numbers or email addresses.";

        var parsedCategory = Enum.TryParse<UserMemoryCategory>(category, ignoreCase: true, out var c) && Enum.IsDefined(c)
            ? c
            : UserMemoryCategory.Fact;

        var existing = await db.UserMemories.FirstOrDefaultAsync(
            m => m.UserId == userId && m.AssistantId == assistantId && m.Key == normalizedKey, ct);
        if (existing is not null)
        {
            existing.Update(parsedCategory, normalizedKey, content);
            await db.SaveChangesAsync(ct);
            return $"Updated note '{normalizedKey}'.";
        }

        if (await db.UserMemories.CountAsync(m => m.UserId == userId && m.AssistantId == assistantId, ct) >= MaxNotesPerAgent)
            return $"Not saved: you already keep {MaxNotesPerAgent} notes about this user. Forget one you no longer need first.";
        if (await db.UserMemories.CountAsync(m => m.UserId == userId, ct) >= MaxMemoriesPerUser)
            return "Not saved: this user's memory is full.";

        db.UserMemories.Add(new UserMemory(Guid.NewGuid(), tenantId, userId, assistantId, parsedCategory,
            normalizedKey, content, UserMemorySource.Agent, UserMemoryStatus.Active));
        await db.SaveChangesAsync(ct);
        return $"Saved note '{normalizedKey}'.";
    }

    public async Task<string> ForgetAsync(string key, CancellationToken ct = default)
    {
        if (await GetAgentNotesContextAsync(ct) is not { } notesContext)
            return "Notes are not available in this conversation.";
        var (_, userId, assistantId) = notesContext;

        var normalizedKey = UserMemory.NormalizeKey(key);
        var existing = normalizedKey is null
            ? null
            : await db.UserMemories.FirstOrDefaultAsync(
                m => m.UserId == userId && m.AssistantId == assistantId && m.Key == normalizedKey, ct);
        if (existing is null) return $"There is no note named '{normalizedKey ?? key}'.";

        db.UserMemories.Remove(existing);
        await db.SaveChangesAsync(ct);
        return $"Forgot note '{normalizedKey}'.";
    }

    // Re-checked at call time, not only when the tools were attached: the user may have switched
    // agent notes off in another tab while this turn was running.
    private async Task<(Guid TenantId, Guid UserId, Guid AssistantId)?> GetAgentNotesContextAsync(CancellationToken ct)
    {
        if (!scope.AgentNotesEnabled || scope.AssistantId is not { } assistantId
            || PersonId is not { } userId || currentUser.TenantId is not { } tenantId)
            return null;

        _settings = null;
        var settings = await GetSettingsAsync(userId, ct);
        return settings.MemoryEnabled && settings.AgentNotesEnabled ? (tenantId, userId, assistantId) : null;
    }

    private async Task<UserMemorySettings> GetSettingsAsync(Guid userId, CancellationToken ct) =>
        _settings ??= await db.UserMemorySettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct)
            ?? UserMemorySettings.Defaults(currentUser.TenantId ?? Guid.Empty, userId);

    private Guid RequireUser() => PersonId ?? throw new UnauthorizedAccessException();

    private bool IsApiKeyRequest =>
        string.Equals(httpContextAccessor.HttpContext?.User.FindFirst("auth_method")?.Value, "api_key", StringComparison.Ordinal);

    /// <summary>The signed-in person's id, or null for an anonymous or API-key request.</summary>
    private Guid? PersonId => IsApiKeyRequest ? null : currentUser.UserId;

    private async Task<UserMemory> FindOwnAsync(Guid id, CancellationToken ct)
    {
        var userId = RequireUser();
        return await db.UserMemories.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(UserMemory), id);
    }

    private static (string? Key, string Content) Validate(SaveUserMemoryRequest request)
    {
        var content = request.Content?.Trim() ?? string.Empty;
        if (content.Length == 0)
            throw new ValidationException("Content", "Write what should be remembered.");
        if (content.Length > UserMemory.MaxContentLength)
            throw new ValidationException("Content",
                $"Keep a memory under {UserMemory.MaxContentLength} characters.");
        if (!Enum.IsDefined(request.Category))
            throw new ValidationException("Category", "Choose a category.");

        var key = UserMemory.NormalizeKey(request.Key);
        if (PiiDetector.ContainsPii(content) || PiiDetector.ContainsPii(request.Key))
            throw new ValidationException("Content", PiiRejection);
        return (key, content);
    }

    private async Task EnsureKeyIsFreeAsync(Guid userId, Guid? assistantId, string? key, Guid? excludeId, CancellationToken ct)
    {
        if (key is null) return;
        var clash = await db.UserMemories.AnyAsync(
            m => m.UserId == userId && m.AssistantId == assistantId && m.Key == key && (excludeId == null || m.Id != excludeId), ct);
        if (clash)
            throw new ValidationException("Key",
                $"Another memory is already named '{key}'. Edit that one, or use a different name.");
    }

    // The job row outlives the turn, so it never holds raw identifiers: not every chat path runs the
    // tenant's PII policy first, and nothing with an identifier could be learned anyway.
    private static string ForPayload(string text) =>
        PiiDetector.Redact(text.Length <= MaxLearningInputChars ? text : text[..MaxLearningInputChars]);
}
