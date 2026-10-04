# Security & Governance Standards

## Security pipeline

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

## Required controls

- authentication
- authorization
- RBAC
- tenant isolation
- workspace isolation
- tool permissions
- connection permissions
- audit
- approvals
- policies
- rate limiting
- secret handling
- retention
- publishing controls

## Secrets

Never commit:
- API keys
- passwords
- bearer tokens
- database credentials
- private keys

Never log secrets.

Reuse the existing secret-management architecture.

## SSRF

Any server-side URL execution must be protected against SSRF.

Review:
- protocols
- destination allow/deny policy
- private addresses
- redirects
- DNS rebinding
- timeouts
- response limits

## Authorization

Frontend controls are not security.

Every protected operation must enforce server-side authorization.

## Tenant isolation

Every customer-owned query must respect tenant/workspace ownership.

## Tool governance

Before tool execution verify:
- tool exists
- agent can use it
- user can use it
- connection is allowed
- parameters are valid
- policy passes
- approval is satisfied

## Audit

Record sufficient operational context without logging sensitive values unnecessarily.

## Data retention

Retention must be configurable where appropriate and should not accidentally remove records required for security or compliance.

## Autopilot

Autopilot must never silently:
- grant permissions
- bypass policy
- expose secrets
- publish dangerous actions
- modify production versions
- execute arbitrary code
