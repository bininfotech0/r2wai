# Automation Standards

## V1 objective

Provide useful business automation without making the initial product a complex workflow-engine replacement.

## Triggers

```text
Schedule
Webhook
Manual
Agent-triggered
```

## Actions

```text
Call API
Query database
Run agent
Condition
Transform
Notify
Approval
```

## Natural language automation

Example:

> Every morning check overdue invoices and notify finance.

Proposed flow:

```text
Schedule
→ Get invoices
→ Filter overdue
→ Summarize
→ Notify
→ Audit
```

The user should review generated automation before activation.

## Execution

Every automation execution should have:
- execution ID
- status
- start/end
- steps
- errors
- retries
- result
- audit

## Safety

Automation must respect:
- identity
- tenant
- workspace
- permissions
- policy
- approval
- connection security

## Reliability

Use:
- timeouts
- retry policy
- idempotency where required
- cancellation
- failure handling

## Scope

Defer advanced visual workflow features unless justified by real product requirements.
