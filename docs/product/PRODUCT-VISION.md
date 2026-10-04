# Product Vision

> **Source:** ported from `ROADMAP.md` §1 (revised 2026-09-29). This is the current, canonical
> product direction — it supersedes any older document (including `docs/audit/`'s source-roadmap
> retention section) that makes Department or organizational Application mandatory.

R2WAI 2.0 is a self-hosted, application-centric enterprise AI Agent Platform for government and
regulated organizations. It connects existing business applications and APIs, discovers available
capabilities, and helps administrators configure, test, govern, and publish assistants and
agent-powered workflows without building a separate AI application for every use case.

**Product principle:** Application first; agent execution is governed; data remains under the
deploying organization's control.

## Non-negotiable design principles

1. **Self-hosted and sovereign by default.** Support on-premises and offline/air-gapped
   deployments where the selected model and supporting components permit it. No silent cloud
   fallback or outbound data transfer.
2. **The model is not an authority.** Models propose tool calls; R2WAI authenticates the caller
   and enforces tenant, department, application, role, record, action, risk, and approval policies
   before execution.
3. **One governed integration path.** API tools, MCP tools, and platform capabilities use a shared
   authorization, policy, credential, audit, timeout, and execution boundary. (Current state:
   `AiFunctionAuditFilter` — real but Semantic-Kernel-shaped; extracting it into a
   runtime-neutral `IToolGateway` is the current implementation plan's Phase 2, a prerequisite for
   MCP.)
4. **Separate knowledge from live transactions.** Documents and policies are retrieved through
   permission-aware RAG. Current records and transactions are accessed only through authorized
   tools/APIs.
5. **Human accountability for consequential actions.** Require confirmation or human approval
   based on tool risk and policy; record decisions and outcomes.
6. **Incremental modernization.** Preserve working modules, data, API contracts, and tenant data
   where possible. Use additive migrations, compatibility adapters, feature flags, and regression
   tests. Do not perform a destructive rewrite.
7. **No simulated production capability.** Health, usage, evaluation, execution status, and audit
   data must come from real persisted events or actual probes. Label estimates and unsupported
   features clearly.

## Status against this vision (2026-09-30)

Substantially aligned already: Department/Application are not required to create an agent,
knowledge base, workflow, or tool (verified — all have nullable `ApplicationId`); a real,
Application-independent Connections module exists; tenant isolation is fail-closed and
extensively tested; no fabricated health/usage data was found in the areas audited. Not yet
aligned: MCP and Agent Framework don't exist yet (principle 3 above); the durable execution ledger
that would make "record decisions and outcomes" durable across a crash (principle 5) doesn't fully
exist yet either. See `docs/audit/FEATURE-MATRIX.md` for the full gap list and the implementation
plan for the closure sequence.
