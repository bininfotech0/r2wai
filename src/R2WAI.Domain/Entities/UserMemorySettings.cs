using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A user's own switches for <see cref="UserMemory"/>. A user with no row gets the defaults below:
/// memory on (it only ever holds what the user saved or approved), automatic learning off, and
/// learned memories held for review once learning is switched on.
/// </summary>
public sealed class UserMemorySettings : BaseEntity<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Master switch: when off, no agent reads or writes any of this user's memories.</summary>
    public bool MemoryEnabled { get; private set; } = true;

    /// <summary>Opt-in: after a chat turn, pick out durable facts and preferences the user stated.</summary>
    public bool AutoLearnEnabled { get; private set; }

    /// <summary>Learned memories wait for the user's approval before any agent sees them.</summary>
    public bool ReviewLearned { get; private set; } = true;

    /// <summary>Agents whose memory mode allows it may keep notes in their own partition.</summary>
    public bool AgentNotesEnabled { get; private set; } = true;

    private UserMemorySettings() { }

    public UserMemorySettings(Guid id, Guid tenantId, Guid userId)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
    }

    public static UserMemorySettings Defaults(Guid tenantId, Guid userId) => new(Guid.Empty, tenantId, userId);

    public void Update(bool memoryEnabled, bool autoLearnEnabled, bool reviewLearned, bool agentNotesEnabled)
    {
        MemoryEnabled = memoryEnabled;
        AutoLearnEnabled = autoLearnEnabled;
        ReviewLearned = reviewLearned;
        AgentNotesEnabled = agentNotesEnabled;
        MarkAsModified();
    }
}
