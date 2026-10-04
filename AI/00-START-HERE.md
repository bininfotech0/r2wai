# R2WAI AI Development — START HERE

## Purpose

This folder is the operating knowledge base for AI coding agents working on the existing R2WAI repository.

Repository:
`C:\Users\LENOVO\Bots\R2WAI`

## Mandatory reading order

Before modifying code:

1. `01-AI-CODEBASE-AGENT-RULES.md`
2. `02-PRODUCT-SPECIFICATION.md`
3. `03-PRODUCT-ROADMAP.md`
4. `04-ARCHITECTURE.md`
5. `05-CODEBASE-MAP.md`
6. Read the task-specific standards below.

### Backend task

Read:
- `06-BACKEND-STANDARDS.md`
- `08-DATABASE-STANDARDS.md`
- `09-API-INTEGRATION-STANDARDS.md`
- `14-SECURITY-GOVERNANCE.md`
- `15-TESTING-QA-STANDARDS.md`

### Frontend task

Read:
- `07-FRONTEND-UI-UX-STANDARDS.md`
- `14-SECURITY-GOVERNANCE.md`
- `15-TESTING-QA-STANDARDS.md`

### Agent / AI task

Read:
- `10-AI-AGENT-RUNTIME.md`
- `11-KNOWLEDGE-RAG-STANDARDS.md`
- `12-CONNECTION-PROVIDER-STANDARDS.md`
- `14-SECURITY-GOVERNANCE.md`
- `15-TESTING-QA-STANDARDS.md`

### Deployment task

Read:
- `16-DEVOPS-DEPLOYMENT.md`
- `17-CONFIGURATION-ENVIRONMENT.md`
- `19-OBSERVABILITY-PERFORMANCE.md`

## Mandatory workflow

```text
INSPECT
→ UNDERSTAND
→ REUSE
→ PLAN
→ IMPLEMENT
→ BUILD
→ TEST
→ REVIEW
→ DOCUMENT
```

Never start by writing code.

## Repository truth

Current source code is the primary source of truth.

If this documentation conflicts with the repository:
1. Do not silently choose one.
2. Record the discrepancy.
3. Inspect the current implementation.
4. Update the appropriate AI documentation after the decision.

Unknown information must be marked:

`TBD / VERIFY`

## Completion

A coding task is not complete merely because code was written.

Use:
`20-DEFINITION-OF-DONE.md`

## Task control

Work from:
`tasks/CURRENT-TASK.md`

Use:
`tasks/TASK-TEMPLATE.md`

After completion, move the task into:
`tasks/COMPLETED/`

## Architecture decisions

Use:
`decisions/ADR-TEMPLATE.md`

Update:
`decisions/ADR-INDEX.md`

## Reports

Maintain:
- `reports/CODEBASE-AUDIT.md`
- `reports/FEATURE-GAP-ANALYSIS.md`
- `reports/TECHNICAL-DEBT.md`

Do not fabricate repository facts.

## Final instruction

If uncertain:

> STOP AND INSPECT. DO NOT GUESS.
