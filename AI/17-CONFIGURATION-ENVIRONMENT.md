# Configuration & Environment Standards

## Environments

```text
Development
Test
Staging
Production
```

## Configuration principle

Configuration belongs outside source code when it varies by environment.

Potential categories:
- database
- cache
- model provider
- AI settings
- storage
- authentication
- external services
- feature flags
- retention
- limits

## Secrets

Secrets must use the repository's approved secret mechanism.

Never commit secrets.

Never put server secrets into frontend bundles.

## Configuration validation

Application startup should fail clearly when mandatory configuration is invalid.

Do not silently use insecure defaults in production.

## Feature flags

Use feature flags where a feature must be rolled out safely.

Do not create a feature flag for every trivial code path.

## Environment changes

When adding a configuration key:
1. Search for an existing equivalent.
2. Define the purpose.
3. Define required/optional behavior.
4. Update documentation.
5. Update deployment configuration if required.
6. Add validation/tests.

Never invent environment variable names without inspecting project conventions.
