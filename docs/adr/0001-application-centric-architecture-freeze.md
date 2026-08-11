# ADR-0001 — Application-Centric Architecture Freeze

- **Status:** Accepted
- **Date:** 2026-08-10
- **Related:** [PLATFORM-IMPLEMENTATION-PLAN.md](../implementation/PLATFORM-IMPLEMENTATION-PLAN.md), [ARCHITECTURE.md](../../ARCHITECTURE.md)

## Context

R2WAI was built as a collection of AI features (assistant, chatbot, knowledge base, workflow, model) that were **not centered on a real product scenario**. The Government AI direction requires the platform to connect existing government applications and layer AI/automation on top of them — without rebuilding the applications.

The existing domain has no `Application` or `Department` entity, and several overlapping concepts (`Chatbot` vs `AssistantDefinition` vs `Agent`), plus standalone studios that duplicate configuration surfaces.

## Decision

Freeze the following for the re-platforming effort. **No feature development proceeds until this is signed off.**

1. **`Application` is the central domain entity.** Everything (APIs, assistant, knowledge, navigation, tools, workflows, policies, security, monitoring) is configured per application. Ownership: `Tenant → Department → Application`.
2. **`Department` is a first-class entity** between `Tenant` and every application-scoped entity.
3. **The entity class is named `ConnectedApplication`** (`R2WAI.Domain.Entities.ConnectedApplication`), conceptually the "Application". The simple class name `Application` is unusable here: the `R2WAI.Application` namespace shadows it (CS0118) everywhere in the `R2WAI.*` namespace tree, because global usings are resolved *after* enclosing-namespace member lookup. A per-file `using` alias would work but is a footgun for the platform's most-used entity. User-facing concepts, table, DbSet, controller and folders keep the name `Applications`.
4. **`Chatbot` is merged into `Assistant → Channels`** (Phase 2 migration, not a new concept).
5. **The LLM never calls application APIs directly.** A server-side Tool/API Gateway enforces authorization (RBAC + ABAC), risk policy, confirmation, approval, and audit.
6. **RAG and live API data are separate sources** combined by the assistant, never conflated.
7. **Public is an access context, not a role** in the RBAC hierarchy.
8. **Model management is Administration → Model Governance** (super admin approves; department admin selects from approved models).
9. **Studios collapse to four areas:** Application Studio, Assistant Studio, Workflow Studio, Operations Center. Administration is a platform section.
10. **No production configuration is hard-deleted.** Lifecycle: `Draft → Discovering → Configuring → Testing → Published → Disabled → Archived` with Disable/Archive/Soft Delete/Rollback.

## Consequences

- New entities (`Department`, `Application`) carry `TenantId` and inherit `BaseEntity<Guid>`, so tenant isolation and soft-delete query filters apply automatically.
- New CQRS feature folders are grouped under `Features/Applications`, `Features/Departments`, etc.
- Legacy chat-first concepts remain in the codebase until migration but are not extended.
- The `R2WAI.Application` project namespace keeps its name (the entity type name is unambiguous in practice).
