# Execution and Workflows

## Current state

`IWorkflowBridge` (`src/R2WAI.Application/Common/Interfaces/IWorkflowBridge.cs`,
implementation `src/R2WAI.Api/Services/WorkflowBridge.cs`, ~489 lines) is the clean seam: only
`WorkflowsController`/`ApprovalsController` depend on it; `NoOpWorkflowBridge` already exists and
proves the seam is swappable. Elsa-specific code is confined to `src/R2WAI.Api/Workflows/*` +
`WorkflowBridge.cs` + `Program.cs`'s `AddElsa` call — nothing in the Application layer or any other
controller references Elsa directly.

**Elsa's native bookmark-based resume is broken** for this app's dynamically-rebuilt Flowcharts —
confirmed against Elsa 3.7.0/3.7.1's own persisted state, a genuine upstream bug, not a call-site
mistake. The workaround: every "resume" (approval resume, retry, delayed continuation) discards the
old Elsa run and republishes+runs a **brand-new** Elsa definition containing only the remaining
steps, keeping the same `step-{index}` activity IDs so notification handlers still correlate.
**Elsa is therefore already reduced to a stateless single-pass executor; R2WAI's own
`WorkflowInstance`/`WorkflowStepExecution` tables are the real durable state.**

`BackgroundJob`/`BackgroundJobProcessor` gives durable, race-safe (atomic conditional
`ExecuteUpdateAsync` claim), backoff-retried (30s/2m/10m/30m/1h), dead-lettered job processing for
fire-and-forget jobs. **Closed 2026-09-30 (Phase 5):** lease-expiry reclaim — `LeaseOwner`/
`LeaseExpiresAt` columns, the due-jobs query now also picks up a `Processing` row whose lease has
expired, so a job whose claiming replica crashed mid-run is retried/dead-lettered again instead of
staying stuck forever. Verified with a real crash simulation (Testcontainers Postgres, the actual
`BackgroundJobProcessor` running for real): an abandoned lease is reclaimed and processed exactly
once on the next poll; an unexpired lease is correctly left alone.

`WorkflowDelayResumeBackgroundService`'s claim got the same fix, in `WorkflowInstance`'s own shape:
it used to clear `PendingResumeAt` itself as the claim signal, which meant a crash between the
claim and `ContinueDelayedWorkflowAsync` finishing left the instance permanently stuck — cleared
`PendingResumeAt`, `PendingResumeStepIndex` still set, `Status` still `Running` throughout,
indistinguishable from a normal in-progress instance. Now the claim grants a
`PendingResumeLeaseExpiresAt` lease instead, preserving `PendingResumeAt` as the durable "still
needs resuming" signal; a stale lease is picked back up by the next sweep. Also verified with a
real crash simulation.

Mid-job checkpointing and per-tool-call idempotency keys remain out of scope for the general case —
see below for what Phase 5 actually found and fixed instead of the plan's original assumption.

## Target

Ported from `docs/audit/R2WAI-IQ200-AUDIT-2026-09-20.md` §56 — the execution model an eventual
durable ledger implements: **Propose** (model returns a structured proposal, validated against a
schema) → **Decide** (identity→tenant→permission→policy→risk, deny by default) → **Approve if
required** (create `ApprovalRequest(PayloadHash)`, release the worker, resume by event) →
**Prepare** (insert `ToolExecution(Prepared, IdempotencyKey)` in the same transaction as step
state) → **Send** (through the egress guard with the idempotency key; never auto-retry
non-idempotent targets) → **Record** (`Succeeded`/`Failed`/`Unknown` on timeout) → **Verify**
(reconcile `Unknown` executions via a read check before any retry) → **Audit** (same transaction)
→ **Recover** (workers lease steps, expired leases reclaimed and reconciled) → **Cancel**
(cooperative token; in-flight `Sent` executions reconciled, not assumed).

**A real, load-bearing correction from this phase, not just narrower scope:** the plan assumed the
one concrete crash-duplication scenario worth closing was a workflow-step external tool call
(API Call / Email steps), and that a `ToolExecution(Prepared/Sent/Succeeded/Failed/Unknown,
IdempotencyKey)` entity would close it. A repository research pass (tracing Elsa 3.7.0's actual
activity-execution pipeline, not guessing) found this doesn't hold:

- `ApiCallNodeProvider`/`EmailNodeProvider` hand off entirely to Elsa's own built-in activities
  (`SendHttpRequest`/`SendEmail`). Elsa's `ActivityExecuting`/`ActivityExecuted` notifications only
  bracket the *whole* `ExecuteAsync` call — R2WAI has no code running between "the HTTP request/
  email was dispatched" and "the response came back" for these two step types, so a `Sent` state
  (the one that actually matters — it's what would distinguish "crashed before the call left the
  box" from "crashed after it landed, response lost") **cannot be written** without first replacing
  these with R2WAI-owned custom Elsa activities (mirroring `InvokeSemanticKernelActivity`'s existing
  shape) — real, separate, non-trivial surface area (reimplementing HTTP/SMTP semantics under
  R2WAI's own `ExecuteAsync`), not a column-and-entity-sized change. `ApiCallNodeProvider`'s own
  doc comment already flagged exactly this gap before this phase.
- `AiGenerateNodeProvider`/`ApprovalNodeProvider` *do* already run R2WAI-owned custom activities
  (`InvokeSemanticKernelActivity`, `ApprovalStepActivity`) — a `Sent` write is cheap there — but
  neither has the acute "real-world, hard-to-undo side effect" risk profile API Call/Email do
  (`ApprovalService`'s own claim path, `ClaimDecisionAsync`, was independently confirmed already
  atomic-claim-protected).
- The actual concrete, already-exploitable crash-duplication bug the research pass found instead:
  `NotifyApproversJobHandler` emails/notifies every approver in a loop with **no per-recipient
  tracking** — a crash or thrown exception mid-loop (now *more* reachable than before this phase,
  since `BackgroundJobProcessor`'s own new lease-reclaim means a crashed run is retried where it
  previously just stuck forever) re-ran the whole loop, re-notifying every already-notified
  approver. Closed with `ApprovalNotificationDispatch` — a small, real idempotency-key table
  (unique on `ApprovalRequestId`+`EscalationLevel`+`ApproverId`), checked before each send and
  written immediately after — the same *shape* of fix the plan wanted, correctly targeted at where
  the evidence actually pointed. Verified with real tests (`IntegrationTestBase`, InMemory EF):
  a full retry notifies nobody a second time; a partial-completion retry notifies only the
  approver(s) not yet marked; escalation level is part of the dedup key, not conflated with the
  initial request.
- `WorkflowStepExecution.AttemptCount` (additive column, as planned) — incremented on every
  `Start()`, including a retry re-running the same row (`WorkflowBridge.RetryFailedStepAsync` reuses
  the existing row by `(WorkflowInstanceId, StepIndex)`, confirmed by tracing
  `StepStatusNotificationHandler.ResolveStepExecutionAsync`) — real, low-risk observability value
  regardless of the `ToolExecution` question above.

**Still genuinely open, flagged rather than built blind:** a real `Sent`-state ledger for API Call/
Email workflow steps needs the custom-Elsa-activity investment described above — a separate, larger
piece of work with no current concrete trigger beyond the theoretical risk, matching this phase's
own "not a speculative universal idempotency framework" boundary. Building a `ToolExecution` entity
that only covered AI Generate/Approval (the two step types where it was cheap) while leaving API
Call/Email uncovered would have been a misleadingly-named half-measure — not built.

The full Propose→Cancel model above remains the long-term target; building the rest of it now,
without a further concrete triggering defect beyond what this phase found, would be speculative.

**Elsa removal:** not planned in the current phase set. The seam is proven swappable
(`NoOpWorkflowBridge`), but actual removal waits until the ledger is proven and any active Elsa
processes are safely drained — per this codebase's own established caution against destructive
workflow-engine changes (a prior session explicitly reversed course on immediate Elsa deletion for
exactly this reason).
