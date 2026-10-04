# R2WAI 2.0 — AI Coding Agent Rules

## Master rule

This is an existing codebase.

**Inspect → Understand → Reuse → Plan → Implement → Build → Test → Review → Document**

Never rewrite working architecture without evidence.

## Never

- invent APIs, tables, classes, configuration keys or commands
- duplicate existing services/components/entities
- bypass authorization, policy or approval
- expose secrets
- use fake production data
- claim tests passed without running them
- claim implementation exists without inspecting it
- silently remove behavior
- add arbitrary code/SQL/HTTP/filesystem execution
- introduce customer-specific hardcoded business logic

## Product direction

R2WAI is:

> Enterprise AI Operating Platform that turns existing enterprise systems, knowledge and business processes into governed AI agents and automations.

Core journey:

```text
Connect → Discover → Build → Test → Publish → Monitor → Improve
```

Positioning:

> AI for companies that already have software.

## Product hierarchy

```text
Organization / Tenant
└── Workspace
    ├── Agents
    ├── Connections
    ├── Knowledge
    ├── Automations
    ├── Publish
    └── Activity
```

Do not restore Department as a mandatory hierarchy.

Do not create:
`Workspace → Department → Application → Agent`

## Dynamic architecture

Use:

> Strongly Typed Core + Dynamic Configuration + Schema-Driven Extensions + Controlled Provider Registry

Dynamic does not mean arbitrary runtime code.

Trusted code owns:
- authentication
- authorization
- tenant/workspace isolation
- secrets
- database access
- network security
- execution engine
- validation
- audit
- core invariants

## Execution security

```text
Identity
→ Tenant
→ Workspace
→ Resource Authorization
→ Policy
→ Validation
→ Approval
→ Execution
→ Audit
```

## LLM rule

OpenAPI/schema is the contract. The LLM is the reasoning layer.

```text
Schema
→ Tool Registry
→ LLM
→ Structured Arguments
→ Parameter Resolver
→ Validator
→ Policy
→ Tool Gateway
→ Execution
```

## AI Autopilot

Autopilot may propose configuration, tools, tests and agents.

Autopilot must not silently:
- grant permissions
- expose secrets
- bypass policy
- bypass approval
- publish dangerous tools
- modify production versions
- execute unrestricted code

## Coding task report

After implementation report:
- Changed
- Reused
- Added
- Database
- API
- UI
- Security
- Tests
- Risks / TBD

## Final rule

If uncertain:

**STOP AND INSPECT. DO NOT GUESS.**
