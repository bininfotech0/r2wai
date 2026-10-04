# API & Integration Standards

## Principle

> OpenAPI/schema is the API contract. The LLM reasons over that contract.

## Supported priority

```text
OpenAPI / Swagger
REST
Webhook
MCP
PostgreSQL
SQL Server
Oracle
```

## Discovery

Discovery should identify:
- operations
- HTTP method
- path
- authentication
- parameters
- required fields
- nested objects
- arrays
- enums
- request body
- response schema
- error responses

## Parameter model

Preserve:
- name
- path
- location
- type
- required
- description
- enum
- constraints
- nested structure
- array item schema

## Parameter source priority

```text
Explicit user input
→ Conversation context
→ Authenticated user context
→ Workspace context
→ Connection context
→ Approved knowledge
→ Default
→ Ask user
```

## Execution

Before execution:

```text
Schema validation
→ Authorization
→ Policy
→ Approval if required
→ Credential injection
→ Tool execution
→ Response validation
→ Audit
```

## HTTP safety

Apply:
- timeouts
- response size limits
- retries only where safe
- idempotency awareness
- redirect controls
- SSRF controls
- destination policy
- credential protection

## API changes

Before adding an endpoint:
- search existing routes
- search clients
- search tests
- check backward compatibility
- check authorization

Avoid duplicate endpoints.

## Provider architecture

Prefer controlled abstractions such as:

```text
IConnectionProvider
IConnectionProviderRegistry
IOperationSchemaProvider
IToolRegistry
IToolExecutor
```

Reuse existing equivalents.
