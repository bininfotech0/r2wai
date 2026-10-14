namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Scoped, per-request record of the calling Assistant's enabled-tool-id list (the same list
/// SemanticKernelService.GetOrCreateKernelAsync already uses to decide which functions to attach to
/// the kernel), so AiFunctionAuditFilter can re-check membership at call time as a defense-in-depth
/// guard — not because the kernel-build-time filtering is known to be bypassable today, but because
/// today it's the *only* place that enforcement happens; this gives it a second, independent check.
/// Null means "not configured" (every existing assistant with no explicit selection), matching the
/// kernel-build-time semantics exactly: unfiltered, not empty/deny-all.
/// </summary>
public interface IEnabledToolScope
{
    IReadOnlyCollection<Guid>? EnabledToolIds { get; set; }

    /// <summary>The calling assistant's own allow-list of assistants it may hand a task off to via
    /// AgentDelegationPlugin — set alongside <see cref="EnabledToolIds"/> wherever a chat turn
    /// starts. Unlike EnabledToolIds, never null-means-all: an empty collection (including the
    /// default, unset value) means no delegation is allowed, since this is a new capability with no
    /// prior "every assistant" behavior to preserve.</summary>
    IReadOnlyCollection<Guid> DelegatableAssistantIds { get; set; }
}
