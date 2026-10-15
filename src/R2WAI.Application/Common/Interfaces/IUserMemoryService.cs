namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Structured, user-owned memory across conversations (see <see cref="UserMemory"/>). Every member
/// acts for the signed-in user only; nothing here can read or write another user's memories.
/// </summary>
public interface IUserMemoryService
{
    /// <summary>
    /// Called by a chat entry point before the model call. Returns the prompt block listing what the
    /// agent may remember about the user (null when there is nothing to add or memory is off for this
    /// user or agent), marks those memories used, and sets <see cref="IUserMemoryScope"/> so the
    /// kernel attaches the agent's memory tools when the agent and the user both allow them.
    /// assistantId null is a chat with no agent: it reads the Personal partition only.
    /// </summary>
    Task<string?> PrepareTurnAsync(Guid? assistantId, AssistantBehaviorSettings? agentSettings, CancellationToken ct = default);

    /// <summary>
    /// Queues the opt-in learning pass over one finished turn. A no-op unless the user switched
    /// automatic learning on and the agent reads memory. Never throws: learning must not fail a chat.
    /// modelConfigurationId is the model that answered (null: the tenant default).
    /// </summary>
    Task QueueLearningAsync(Guid? assistantId, AssistantBehaviorSettings? agentSettings, Guid? modelConfigurationId,
        Guid? conversationId, string userMessage, string reply, CancellationToken ct = default);

    Task<UserMemoryOverviewDto> GetOverviewAsync(CancellationToken ct = default);
    Task<UserMemoryDto> CreateAsync(SaveUserMemoryRequest request, CancellationToken ct = default);
    Task<UserMemoryDto> UpdateAsync(Guid id, SaveUserMemoryRequest request, CancellationToken ct = default);
    Task<UserMemoryDto> ApproveAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>partition: "all", "personal", "pending" or an agent's id. Returns how many were removed.</summary>
    Task<int> ClearAsync(string partition, CancellationToken ct = default);

    Task<UserMemorySettingsDto> UpdateSettingsAsync(UserMemorySettingsDto settings, CancellationToken ct = default);
    Task<UserMemoryExportDto> ExportAsync(CancellationToken ct = default);

    /// <summary>Agent tool: save or replace a note under key in the calling agent's own partition.</summary>
    Task<string> RememberAsync(string key, string note, string? category, CancellationToken ct = default);

    /// <summary>Agent tool: remove the note under key from the calling agent's own partition.</summary>
    Task<string> ForgetAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Per-request record of which agent is answering and whether it may keep notes, set by
/// <see cref="IUserMemoryService.PrepareTurnAsync"/> and read when the tool kernel is built and when
/// a memory tool runs. AgentNotesEnabled is false unless a turn explicitly enabled it.
/// </summary>
public interface IUserMemoryScope
{
    Guid? AssistantId { get; set; }
    bool AgentNotesEnabled { get; set; }
}

public record UserMemoryDto(
    Guid Id,
    Guid? AssistantId,
    UserMemoryCategory Category,
    string? Key,
    string Content,
    UserMemorySource Source,
    UserMemoryStatus Status,
    bool IsPinned,
    Guid? SourceConversationId,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    DateTime? LastUsedAt)
{
    public static UserMemoryDto From(UserMemory m) => new(
        m.Id, m.AssistantId, m.Category, m.Key, m.Content, m.Source, m.Status, m.IsPinned,
        m.SourceConversationId, m.CreatedAt, m.ModifiedAt, m.LastUsedAt);
}

public record UserMemorySettingsDto(bool MemoryEnabled, bool AutoLearnEnabled, bool ReviewLearned, bool AgentNotesEnabled)
{
    public static UserMemorySettingsDto From(UserMemorySettings s) =>
        new(s.MemoryEnabled, s.AutoLearnEnabled, s.ReviewLearned, s.AgentNotesEnabled);
}

/// <summary>AssistantId null is the Personal partition. AssistantName is null for it and for an
/// agent the user can no longer see.</summary>
public record UserMemoryPartitionDto(Guid? AssistantId, string? AssistantName, int Count, int PendingCount);

public record UserMemoryOverviewDto(
    UserMemorySettingsDto Settings,
    IReadOnlyList<UserMemoryPartitionDto> Partitions,
    IReadOnlyList<UserMemoryDto> Items,
    int Limit);

public record SaveUserMemoryRequest(
    UserMemoryCategory Category,
    string? Key,
    string Content,
    bool IsPinned = false,
    bool Paused = false);

public record UserMemoryExportDto(DateTime ExportedAt, UserMemorySettingsDto Settings, IReadOnlyList<UserMemoryDto> Items);
