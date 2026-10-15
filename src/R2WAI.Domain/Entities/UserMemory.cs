using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// One thing an agent may remember about a signed-in user across conversations. Owned by the user:
/// they can read, edit, pause and delete every row, whoever wrote it.
///
/// Partitions: AssistantId null is the user's Personal partition, which every agent with memory
/// switched on can read. AssistantId set is that agent's own partition, written only by that agent
/// (through its memory tools) and read only by it. Key, when set, is unique within a partition, so
/// "remember my preferred language" replaces the earlier value instead of piling up duplicates.
///
/// Deleted rows are removed, not soft-deleted: forgetting has to actually forget.
/// </summary>
public sealed class UserMemory : BaseEntity<Guid>
{
    public const int MaxContentLength = 500;
    public const int MaxKeyLength = 64;

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? AssistantId { get; private set; }
    public UserMemoryCategory Category { get; private set; }
    public string? Key { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public UserMemorySource Source { get; private set; }
    public UserMemoryStatus Status { get; private set; }
    public bool IsPinned { get; private set; }
    public Guid? SourceConversationId { get; private set; }
    public DateTime? LastUsedAt { get; private set; }

    private UserMemory() { }

    public UserMemory(Guid id, Guid tenantId, Guid userId, Guid? assistantId, UserMemoryCategory category,
        string? key, string content, UserMemorySource source, UserMemoryStatus status, Guid? sourceConversationId = null,
        bool isPinned = false)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        AssistantId = assistantId;
        Category = category;
        Key = NormalizeKey(key);
        Content = content.Trim();
        Source = source;
        Status = status;
        SourceConversationId = sourceConversationId;
        IsPinned = isPinned;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(UserMemoryCategory category, string? key, string content)
    {
        Category = category;
        Key = NormalizeKey(key);
        Content = content.Trim();
        MarkAsModified();
    }

    public void SetPinned(bool pinned)
    {
        IsPinned = pinned;
        MarkAsModified();
    }

    /// <summary>Active or Paused. Approving a PendingReview memory goes through <see cref="Approve"/>.</summary>
    public void SetPaused(bool paused)
    {
        if (Status == UserMemoryStatus.PendingReview) return;
        Status = paused ? UserMemoryStatus.Paused : UserMemoryStatus.Active;
        MarkAsModified();
    }

    public void Approve()
    {
        if (Status != UserMemoryStatus.PendingReview) return;
        Status = UserMemoryStatus.Active;
        MarkAsModified();
    }

    public void MarkUsed(DateTime utcNow) => LastUsedAt = utcNow;

    /// <summary>Lower-case snake_case, at most <see cref="MaxKeyLength"/> characters; null when blank.</summary>
    public static string? NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var chars = key.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        var normalized = string.Join('_', new string(chars).Split('_', StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length == 0) return null;
        return normalized.Length <= MaxKeyLength ? normalized : normalized[..MaxKeyLength];
    }
}
