# Database Standards

## Current target direction

PostgreSQL + EF Core is the documented target direction. Verify the current repository before making assumptions.

## Before schema changes

Inspect:
- entities
- DbContext
- configurations
- migrations
- indexes
- relationships
- query filters
- seed data
- existing data dependencies

## Tenant / workspace isolation

Verify that customer-owned records have an explicit ownership boundary.

Do not rely on frontend filtering.

## Migrations

Every migration must:
- have a clear purpose
- preserve required data
- be reversible where practical
- include required indexes/constraints
- be tested against representative data
- be reviewed for performance

Never delete data merely to make a migration pass.

## JSON / JSONB

Use JSON/JSONB for genuinely dynamic configuration or metadata.

Do not create one giant generic table for all business entities.

## Indexing

Indexes should be based on actual query patterns.

Review:
- tenant/workspace filters
- foreign keys
- lookup fields
- timestamps
- execution/activity queries
- unique constraints

## Concurrency

Use appropriate concurrency handling for:
- agent versions
- publishing
- executions
- approvals
- configuration changes

## Retention

Execution/audit/usage data may become very large.

Use documented retention/archival strategies when justified.

## Database completion gate

A database change is not complete until:
- migration exists
- migration has been reviewed
- application behavior is tested
- isolation is verified
- indexes are reviewed
