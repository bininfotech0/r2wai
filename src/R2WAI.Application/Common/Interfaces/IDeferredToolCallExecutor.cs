using R2WAI.Domain.Entities;

namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// The resume half of the deferred-tool-call approval flow — see DeferredToolCallPayload for the
/// create half. Called from ApprovalsController.Approve alongside the existing workflow-resume
/// branch, for the case a workflow-bound approval never covers: a standalone AI tool call that was
/// paused because it needed human approval.
/// </summary>
public interface IDeferredToolCallExecutor
{
    /// <summary>
    /// If <paramref name="request"/> carries a deferred dynamic-tool-call payload, actually invokes it
    /// for real and returns a short, human-readable result to record against the request. Returns null
    /// if it isn't one of these (a workflow-bound approval, a built-in-tool denial that was never
    /// wrapped in a payload, or garbage in Data) — the caller no-ops rather than guessing.
    /// </summary>
    Task<string?> TryExecuteAsync(ApprovalRequest request, CancellationToken ct = default);
}
