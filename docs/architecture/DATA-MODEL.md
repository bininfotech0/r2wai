# Data Model

## Current state (relevant entities)

Tenant isolation: a reflective EF Core global query filter (`ApplicationDbContext.OnModelCreating`)
auto-applies tenant + soft-delete filtering to any entity with a `TenantId` property, fail-closed
(`TenantId != null && match`) as of this session's tenant-isolation work. Entities without a
`TenantId` column (`AccessRequest`, `BackgroundJob`, `MessageAttachment`, `TestCaseResult`,
`UserRole`, `WorkflowStepExecution`, `KnowledgeBaseSource`) are safe by construction — reached only
via a tenant-filtered parent, verified by tracing every direct-by-id access path, not assumed.
`vector_embeddings` has no tenant column either; isolation is via a non-guessable
`collection_name` plus the same parent-KB tenant check.

Core workflow/execution entities: `Workflow`/`WorkflowVersion`/`WorkflowInstance`/
`WorkflowStepExecution` (R2WAI's own durable state, Elsa-independent except
`WorkflowInstance.ElsaInstanceId`), `ApprovalRequest` (workflow-independent, nullable
`WorkflowId`/`WorkflowInstanceId`, supports multi-level chains), `BackgroundJob` (generic
fire-and-forget job with atomic claim/backoff/dead-letter, no leasing/checkpointing yet).

Versioning: `AssistantVersion`/`KnowledgeBaseVersion` — immutable JSON `ConfigSnapshot` +
`IsPublished`/`PublishedAt`, forward-only rollback (unpublish old, create+publish new version
copying the target snapshot). Not yet enforced at serve time for anything that consumes it.

Application/Department: `ConnectedApplication`/`Department` still exist and are load-bearing
(frozen per ADR-0001), but every core content entity (`AssistantDefinition`, `KnowledgeBase`,
`Workflow`, `ToolDefinition`, `TestCase`, `ModelConfiguration`) has a **nullable** `ApplicationId`
— none of them require one to be created.

## Target additions (ported from `docs/audit/R2WAI-IQ200-AUDIT-2026-09-20.md` §55)

| Table | Purpose | Plan phase |
|---|---|---|
| `ToolExecution` | `(TenantId, WorkflowInstanceId, WorkflowStepExecutionId, IdempotencyKey)` unique, `Status{Prepared,Sent,Succeeded,Failed,Unknown}` — the concrete crash-duplication fix | Phase 5 |
| `BackgroundJob.LeaseOwner`/`LeaseExpiresAt` (additive columns) | reclaim a crashed-mid-processing job instead of leaving it stuck | Phase 5 |
| `WorkflowStepExecution` attempt count (additive column) | a retry currently overwrites the row with no history | Phase 5 |
| `McpServerConnection` | tenant-owned MCP server config, explicit allowlist | Phase 3 |
| `ApiKey.Scopes` gains an assistant-scoped value | publish a specific assistant under a specific key | Phase 1 |

**Explicitly not planned:** `Capability`/`CapabilityVersion` (a full input/output-schema-typed
replacement for `ToolDefinition`), hash-chained/WORM `AuditEvent` export, layered
`PolicyVersion`/`AttributeAssignment` (ABAC) — all real target-architecture items from the source
audit, but with no concrete triggering defect found yet; building them now would be speculative
infrastructure ahead of a real need.
