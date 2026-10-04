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
}
