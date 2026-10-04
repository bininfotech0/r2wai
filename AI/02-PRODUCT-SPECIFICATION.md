# R2WAI 2.0 — Product Specification

## Product

**R2WAI 2.0 — Enterprise AI Operating Platform**

## Product promise

> Connect your systems. Give AI knowledge and tools. Build agents. Automate work. Publish anywhere. Keep control.

## Positioning

> AI for companies that already have software.

R2WAI sits above existing enterprise systems rather than requiring customers to replace them.

## Primary journey

```text
Connect
→ Discover
→ Build
→ Test
→ Publish
→ Monitor
→ Improve
```

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

## Primary navigation

```text
Home
Agents
Connections
Knowledge
Automations
Publish
Activity
Settings
```

## Core capabilities

### Agents
- Agent creation
- Agent instructions
- Agent versions
- Tools
- Knowledge
- Permissions
- Policies
- Tests
- Publishing
- Activity

### Connections
- OpenAPI / Swagger
- REST
- MCP
- Webhooks
- PostgreSQL
- SQL Server
- Oracle
- Future curated providers

### Knowledge
- Document upload
- Extraction
- OCR where required
- Markdown normalization
- Chunking
- Metadata
- Embeddings
- Hybrid retrieval
- Reranking
- Citations

### Automations
- Schedule
- Webhook
- Manual
- Agent-triggered
- API actions
- Database actions
- Conditions
- Notifications
- Approvals

### Publishing
- Website widget
- REST API
- Internal applications
- Future channels

### Governance
- Tenant isolation
- Workspace isolation
- RBAC
- Tool permissions
- Connection permissions
- Policies
- Approvals
- Audit
- Rate limits
- Retention
- Secret handling

## Flagship product experience

### API Autopilot

```text
Swagger URL
→ Discover
→ Understand
→ Generate tools
→ Validate
→ Test
→ Activate
```

### Agent Autopilot

User describes a business requirement.

R2WAI proposes:
- agent
- instructions
- knowledge
- tools
- permissions
- authentication
- starter prompts
- tests

User reviews before publication.

## Deployment

### R2WAI Cloud

Managed SaaS.

### R2WAI Private

Self-hosted deployment for customers requiring infrastructure and data control.

Both use the same product concepts and core architecture.

## Product principles

1. Simple outside.
2. Powerful inside.
3. Existing systems first.
4. Business outcomes first.
5. Governed by default.
6. Configuration over customer-specific code.
7. Do not add features merely to increase feature count.
