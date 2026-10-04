# Backend Development Standards

## Scope

Applies to the existing R2WAI .NET backend.

## Before coding

Inspect:
- project structure
- existing patterns
- dependency injection
- controllers
- application services
- domain services
- repositories
- validation
- exception handling
- authorization
- tests

## Principles

- Preserve Clean Architecture boundaries where they exist.
- Keep business rules out of controllers.
- Keep domain invariants in the domain layer.
- Keep infrastructure concerns in Infrastructure.
- Prefer existing abstractions over new duplicates.
- Use async APIs appropriately.
- Propagate CancellationToken for cancellable operations.
- Validate input at boundaries.
- Use structured logging.
- Never log secrets.

## API

Controllers/endpoints should:
- validate request shape
- authorize
- delegate to application logic
- return stable contracts
- avoid business logic

## Services

Before adding a service:
1. Search for equivalent interfaces/classes.
2. Determine whether the existing service can be extended.
3. Check dependency direction.
4. Add only the smallest required abstraction.

## Errors

Do not:
- swallow exceptions
- use empty catch blocks
- return success after failed operations
- leak internal stack traces to clients

## AI execution

AI calls must remain behind controlled application abstractions.

Tools must pass through:
- schema validation
- authorization
- policy
- approval where applicable
- audit

## Database

Do not access the database directly from controllers.

## Tests

New backend behavior should have appropriate unit/integration tests.
