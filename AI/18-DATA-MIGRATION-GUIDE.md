# R2WAI 2.0 Data Migration Guide

## Purpose

Guide migration from legacy R2WAI concepts to the simplified Workspace-first model.

## Target

```text
Tenant
↓
Workspace
├── Agents
├── Connections
├── Knowledge
├── Automations
├── Publish
└── Activity
```

## Important

Do not assume direct mappings before inspecting the actual database and code.

## Legacy concepts

Potential legacy concepts include:
- Department
- Application
- old workflow structures
- old connector structures

These must be inventoried before removal.

## Migration process

```text
Inventory
↓
Map relationships
↓
Identify API dependencies
↓
Identify UI dependencies
↓
Identify authorization dependencies
↓
Define target model
↓
Write migration
↓
Migrate
↓
Regression test
↓
Remove legacy only when safe
```

## Migration table

| Legacy concept | Current usage | Target | Migration | Removal condition |
|---|---|---|---|---|
| Department | TBD / VERIFY | Workspace/permissions where appropriate | TBD | TBD |
| Application | TBD / VERIFY | Connection or workspace resource depending on actual semantics | TBD | TBD |
| Legacy workflow | TBD / VERIFY | Automation/execution model | TBD | TBD |

## Rule

Never delete a legacy table/entity simply because it is no longer visible in the new UI.
First verify runtime, API, data and authorization dependencies.
