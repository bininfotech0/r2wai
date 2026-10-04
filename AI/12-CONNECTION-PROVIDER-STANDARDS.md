# Connection Provider Standards

## Goal

Allow R2WAI to connect to business systems without scattering connector-specific logic throughout the application.

## Provider pattern

Use existing repository abstractions where available.

Potential conceptual contracts:

```text
IConnectionProvider
IConnectionProviderFactory
IConnectionProviderRegistry
```

## Provider lifecycle

```text
Define
→ Register
→ Configure
→ Validate
→ Test
→ Activate
→ Execute
→ Monitor
```

## Providers

Priority:
- OpenAPI
- REST
- MCP
- Webhook
- PostgreSQL
- SQL Server
- Oracle

## New provider rule

A new protocol/provider normally requires:
- implementation
- registration
- configuration schema
- validation
- security review
- tests
- documentation

Do not claim a new provider is configuration-only if it requires runtime code.

## Provider isolation

Provider-specific behavior should remain within the provider boundary.

Do not spread provider-specific conditionals across:
- controllers
- domain entities
- UI components
- unrelated services

## Credentials

Credentials must remain server-side and must never be exposed to the browser unless a deliberate architecture requires it.

## Test requirements

Every provider should have:
- configuration validation
- connectivity test
- authorization test
- failure tests
- timeout tests
- audit tests where applicable
