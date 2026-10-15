namespace R2WAI.Domain.Enums;

/// <summary>What kind of thing a memory records. Drives grouping in the Memory page and the
/// heading each memory is listed under in an agent's prompt.</summary>
public enum UserMemoryCategory
{
    Profile,
    Preference,
    Instruction,
    Fact,
    Goal
}

/// <summary>Who wrote a memory: the user (Manual), the platform's opt-in extraction pass over a
/// conversation (Learned), or an agent writing into its own partition through its memory tools.</summary>
public enum UserMemorySource
{
    Manual,
    Learned,
    Agent
}

/// <summary>Only Active memories reach an agent. Learned memories wait in PendingReview when the
/// user asked to review them first; Paused is the user switching one off without deleting it.</summary>
public enum UserMemoryStatus
{
    Active,
    PendingReview,
    Paused
}
