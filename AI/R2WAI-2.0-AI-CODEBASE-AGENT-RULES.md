# R2WAI 2.0 — AI Coding Agent Rules for Existing Codebase

> **Repository:** `C:\Users\LENOVO\Bots\R2WAI`  
> **Purpose:** Operating contract for AI coding agents working on the existing R2WAI codebase.

## 1. MASTER RULE

**INSPECT → UNDERSTAND → REUSE → PLAN → IMPLEMENT → BUILD → TEST → REVIEW → DOCUMENT**

This is an existing application. Never assume a requested feature is missing.

Before changing code:
1. Search the repository.
2. Find existing entities, services, controllers, components, hooks, providers, policies, tests and migrations.
3. Reuse existing implementations where possible.
4. Identify the smallest safe change.
5. Only then write code.

Never rewrite the application merely to implement a feature.

---

## 2. DO NOT

Never:
- rewrite the whole backend or frontend without explicit evidence
- create duplicate services, controllers, entities, repositories or components
- invent APIs, database tables, configuration keys or environment variables
- claim tests/builds succeeded unless actually verified
- claim a feature is implemented without inspecting the code
- silently remove existing functionality
- bypass authentication, authorization, policy or approval
- expose secrets
- use fake production data
- hardcode customer-specific business logic
- add arbitrary code/SQL/HTTP/filesystem execution
- suppress exceptions just to make a feature appear successful

If something is unknown, mark it **TBD / VERIFY**.

---

# 3. R2WAI 2.0 PRODUCT DIRECTION

R2WAI is an:

> **Enterprise AI Operating Platform that turns existing enterprise systems, knowledge and business processes into governed AI agents and automations.**

Core journey:

```text
Connect → Discover → Build → Test → Publish → Monitor → Improve
```

Positioning:

> **AI for companies that already have software.**

R2WAI should work with existing:
- REST APIs
- OpenAPI / Swagger
- MCP
- PostgreSQL
- SQL Server
- Oracle
- Webhooks
- Enterprise applications
- Documents
- Internal applications

---

# 4. FINAL PRODUCT HIERARCHY

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

Do **not** reintroduce Department as a mandatory hierarchy.

Do **not** create:

```text
Workspace → Department → Application → Agent
```

External enterprise systems are **Connections**.

Primary navigation:

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

---

# 5. EXISTING ARCHITECTURE — VERIFY FIRST

Known repository structure:

```text
src/
├── R2WAI.Api
├── R2WAI.Application
├── R2WAI.Domain
├── R2WAI.Infrastructure
├── R2WAI.Client
└── R2WAI.Widget
```

Known technology direction includes:
- .NET 10 / ASP.NET Core
- Clean Architecture
- React + TypeScript + MUI + Vite
- PostgreSQL + EF Core + pgvector
- Redis where applicable
- Docker
- JWT / enterprise SSO where implemented
- Semantic Kernel
- Existing workflow infrastructure

**Important:** These are architectural references. Verify the current repository before claiming a capability exists.

---

# 6. SOURCE-OF-TRUTH ORDER

Use evidence in this order:

1. Current source code
2. Current database model and migrations
3. Current tests
4. Current API/OpenAPI contracts
5. Current configuration
6. Current documentation
7. Product requirements
8. General knowledge

If documentation conflicts with code, report the discrepancy and verify the current implementation.

---

# 7. ARCHITECTURE PRINCIPLE

Use:

> **Strongly Typed Core + Dynamic Configuration + Schema-Driven Extensions + Controlled Provider Registry**

Do not make everything JSON.

Use strongly typed entities for core business behavior. Use JSON/JSONB only where dynamic configuration or metadata genuinely requires it.

Dynamic configuration may cover:
- Workspaces
- Agents and versions
- Prompts/instructions
- Models/providers
- Knowledge sources
- Connections
- Tools and tool schemas
- Tool permissions
- Policies
- Approvals
- Automations
- Triggers/actions/conditions
- Publishing
- UI metadata
- Limits and retention

Trusted code must own:
- Authentication
- Authorization
- Tenant/workspace isolation
- Secret handling
- Database access
- Network security / SSRF protection
- Execution engine
- Validation
- Audit
- Core domain invariants
- Infrastructure adapters

---

# 8. SECURITY PIPELINE

Every dynamic capability must pass through:

```text
Identity
↓
Tenant
↓
Workspace
↓
Resource Authorization
↓
Policy
↓
Validation
↓
Approval if required
↓
Execution
↓
Audit
```

The LLM must never bypass this pipeline.

---

# 9. LLM IS NOT THE API CONTRACT

**OpenAPI/schema is the source of truth. The LLM is the reasoning layer.**

Correct pattern:

```text
OpenAPI / Tool Schema
↓
Operation Schema
↓
Tool Registry
↓
LLM
↓
Structured Arguments
↓
Parameter Resolver
↓
Parameter Validator
↓
Policy
↓
Tool Gateway
↓
Execution
```

The model must not invent required fields, types, enums, endpoints, authorization rules or database schemas.

---

# 10. PARAMETER RESOLUTION

Priority:

```text
1. Explicit user input
2. Conversation context
3. Authenticated user context
4. Workspace context
5. Connection context
6. Approved knowledge
7. Configured default
8. Ask the user
```

Support:
- nested objects
- arrays
- required/optional fields
- types
- enums
- min/max
- formats
- validation rules

Reuse equivalent existing services before creating new ones. Candidate abstractions include:

```text
IParameterResolver
IParameterValidator
IOperationSchemaProvider
IToolRegistry
IToolExecutor
IPolicyEvaluator
```

---

# 11. CONNECTION ENGINE

Priority:

```text
OpenAPI / Swagger
REST
Webhook
MCP
PostgreSQL
SQL Server
Oracle
```

Future curated connectors may include SAP, Salesforce, ServiceNow, Jira, Microsoft and Google services.

Do not build a huge connector marketplace before the universal connection engine is reliable.

Use provider abstractions rather than scattering `if type == ...` logic through the codebase.

Existing provider type = normally configuration.

New protocol/provider type = implementation + registration + tests.

---

# 12. AGENTS

Agents are first-class runtime resources.

An Agent may include:

```text
Agent
AgentVersion
Instructions
Model configuration
Knowledge bindings
Tool bindings
Permissions
Policies
Starter prompts
Tests
Publishing
Activity
Status
```

Production agents must support versioning:

```text
Draft → Test → Approved → Published → Deprecated
```

Do not silently mutate the production version.

---

# 13. AGENT AUTOPILOT

Example:

> Create an HR assistant that answers policy questions, checks leave balance and submits leave requests.

Autopilot may propose:

```text
Agent
Instructions
Knowledge
Tools
Permissions
Authentication
Starter prompts
Tests
Publishing options
```

But:

> **Autopilot proposes. Runtime validates. User approves publication.**

Never allow generated configuration to bypass governance.

---

# 14. KNOWLEDGE / RAG

Preferred pipeline:

```text
PDF / DOCX / PPTX / XLSX / HTML / TXT / MD
↓
Extraction / OCR
↓
Canonical Markdown
↓
Structure detection
↓
Chunking
↓
Metadata
↓
Embedding / Index
↓
Hybrid retrieval
↓
Reranking
↓
Citations
```

Users should not manually convert PDFs to Markdown.

Keep original source references for citations.

RAG is a capability inside the Agent Runtime, not the entire R2WAI product.

Use:
- RAG for knowledge
- Tools for live enterprise state
- Agents for orchestration
- Policies for control
- Activity/audit for traceability

---

# 15. PLAYGROUND

The Playground should be simple like a modern AI chat product while exposing safe enterprise execution visibility.

Show:
- conversation
- sources
- selected tools
- parameter validation
- policy result
- approval status
- execution status
- latency
- result
- errors

Do not expose private chain-of-thought.

Show safe traces such as:

```text
✓ Tool selected
✓ Parameters validated
✓ Policy passed
✓ API executed
✓ Result received
```

---

# 16. AUTOMATIONS

V1 should stay simple.

Triggers:
```text
Schedule
Webhook
Manual
Agent-triggered
```

Actions:
```text
Call API
Query database
Run agent
Condition
Transform
Notify
Approval
```

Natural-language automation may generate a proposal:

```text
Schedule
↓
Get invoices
↓
Filter overdue
↓
Summarize
↓
Notify
↓
Audit
```

User reviews and enables it.

---

# 17. PUBLISHING

Initial channels:

```text
Website Widget
REST API
Internal Application
```

Preferred flow:

```text
Agent
↓
Readiness Check
↓
Publish
↓
Embed/API
```

One agent should support multiple channels.

---

# 18. AGENT READINESS

Every agent should have a readiness view:

```text
Knowledge        ✓
Tools            ✓
Authentication   ✓
Permissions      ✓
Parameters       ✓
Tests            ✓
Security         ✓
Publishing       ✓
```

If blocked, explain exactly what must be fixed.

---

# 19. ACTIVITY / AUDIT

Minimum execution information:

```text
Execution ID
Timestamp
User
Workspace
Agent
Tool
Status
Duration
Approval
Error
Outcome
```

Do not display fake metrics.

Only show metrics backed by real data.

---

# 20. GOVERNANCE

Required enterprise controls:
- Tenant isolation
- Workspace isolation
- RBAC
- Permissions
- Tool permissions
- Connection permissions
- Audit
- Approvals
- Policy evaluation
- Rate limiting
- Secret handling
- Retention
- Agent lifecycle
- Publishing controls

Frontend filtering is never a substitute for server-side authorization.

---

# 21. SECRET AND NETWORK SECURITY

Never put secrets in:
- source code
- committed config
- frontend bundles
- logs
- test fixtures

For URL/network execution, enforce SSRF and network controls:
- allowed protocols
- destination policy
- private-network restrictions
- redirects
- DNS concerns
- credentials
- response limits
- timeouts

Reuse existing security infrastructure where available.

---

# 22. DATABASE RULES

Before a database change:

1. Inspect entities.
2. Inspect migrations.
3. Inspect indexes.
4. Inspect relationships.
5. Inspect global filters.
6. Verify tenant/workspace isolation.
7. Search for an existing field/table/concept.
8. Design the smallest safe migration.

Never delete data merely to make a migration pass.

---

# 23. API RULES

Before adding an endpoint:

1. Search controllers.
2. Search application commands/queries.
3. Search route names.
4. Search frontend clients.
5. Check authorization.
6. Check validation.
7. Check response contracts.
8. Check tests.

Prefer evolving an existing endpoint over creating a duplicate.

---

# 24. FRONTEND RULES

Reuse:
- existing design system
- components
- API clients
- routing
- authentication
- state management
- validation
- error handling

Do not create a second component library or state architecture without strong evidence.

Prefer:

```text
API
↓
Typed client
↓
View model
↓
Component
```

No fake production data.

---

# 25. UI/UX DIRECTION

Goal:

> **Simple like a modern AI product + powerful like an enterprise control plane.**

Use:
- whitespace
- clear hierarchy
- subtle borders
- restrained accent color
- accessible contrast
- light/dark themes
- responsive layouts
- keyboard accessibility
- progressive disclosure

Avoid:
- legacy admin-portal density
- excessive KPI cards
- neon AI effects
- giant first-screen configuration forms
- fake activity
- fake analytics
- showing every technical option immediately

Home should be a command center:

```text
Good morning

What do you want your AI to do?

[ Describe what you want to automate... ]

Continue where you left off
Needs attention
Agents
Recent activity
Platform health
```

---

# 26. WHITE / AI-ASSISTED CODING MODEL

Every coding task follows:

```text
INSPECT
↓
PLAN
↓
IMPLEMENT
↓
COMPILE
↓
TEST
↓
REVIEW
↓
DOCUMENT
```

Do not ask an AI coding agent to blindly “build R2WAI completely”.

The coding agent must work incrementally against the existing repository.

---

# 27. CHANGE SAFETY

Before editing, identify:

```text
Feature
Module
Acceptance criteria
Affected layers
Security impact
Database impact
API impact
UI impact
Tests required
```

Then search the repository.

After editing, verify:

```text
Build
Tests
Lint/type checks
Migration safety
API compatibility
Authorization
Tenant isolation
UI behavior
```

---

# 28. EXISTING WORKFLOW / AI FRAMEWORKS

If Elsa or another workflow framework already exists, do not remove it simply because the target architecture discusses a different future execution model.

First inspect:
- current dependencies
- production usage
- tests
- migration impact
- replacement value

Likewise, if Semantic Kernel exists, do not migrate frameworks merely because another framework is newer.

Framework migration requires:

```text
Assess → Prototype → Compare → Plan → Migrate → Regression test
```

Never claim a migration is complete without code evidence.

---

# 29. PERFORMANCE / SCALE

Long-running AI execution should be designed toward:

```text
Web / Widget
↓
API
↓
Execution Queue
↓
Workers
↓
Model Gateway
↓
Models
```

Use where appropriate:
- streaming
- async processing
- caching
- bounded retries
- timeouts
- cancellation
- backpressure

Do not prematurely implement massive distributed infrastructure.

---

# 30. MODEL GATEWAY

Avoid coupling the product to one model provider.

Potential providers:

```text
OpenAI
Azure OpenAI
Ollama
Other approved providers
```

Use a controlled provider abstraction.

Do not expose unnecessary model complexity to normal users.

---

# 31. AUTOPILOT SAFETY

Autopilot may generate:
- agents
- instructions
- tools
- mappings
- tests
- automation proposals
- configuration

It must not silently:
- grant permissions
- expose secrets
- publish dangerous tools
- bypass approvals
- modify production versions
- execute unrestricted code
- access unrestricted networks

---

# 32. SELF-IMPROVEMENT

Never silently modify production.

Correct pattern:

```text
Execution
↓
Telemetry
↓
Evaluation
↓
Failure detection
↓
Improvement recommendation
↓
New version
↓
Test
↓
Approval
↓
Production
```

---

# 33. BUILD NOW

P0 / launch-critical:

```text
Agent
Agent Versioning
Connection Engine
OpenAPI Discovery
Parameter Resolver
Tool Registry
Tool Gateway
Knowledge / RAG
Agent Playground
Agent Autopilot
Publish
Website Widget
Activity
Audit
RBAC
Policies
Approvals
Tenant / Workspace isolation
```

---

# 34. DEFER

Do not make these launch blockers:

```text
Huge connector marketplace
Advanced visual workflow canvas
Multi-agent orchestration
Knowledge graph
Fine-tuning platform
Model training
GPU cluster management
Multi-region
Marketplace
Large mobile channel ecosystem
Complex autonomous-company features
```

---

# 35. DO NOT MAKE CORE

Avoid:
- Department hierarchy
- Application hierarchy
- chatbot-only architecture
- LLM training platform
- silent autonomous self-modification
- customer-specific hardcoded business logic
- fake dashboards
- fake analytics
- unrestricted SQL
- unrestricted HTTP
- unrestricted filesystem access
- unrestricted URL fetching

---

# 36. THREE FLAGSHIP DEMOS

## Employee Assistant

```text
HR documents
+
HR API
+
Employee authentication
+
Leave balance
+
Leave request
+
Approval
```

## API Agent

```text
Swagger
↓
Discover
↓
Generate tools
↓
Resolve parameters
↓
Create agent
↓
Test
↓
Publish
```

## Website AI

```text
Knowledge
↓
Agent
↓
Widget
↓
Answer
↓
Citation
↓
Controlled action
```

---

# 37. TEST STRATEGY

Test:
- parameter resolution
- validation
- authorization
- policy evaluation
- OpenAPI discovery
- API execution
- database providers
- knowledge ingestion
- RAG retrieval
- tool gateway
- audit
- approval
- publishing

End-to-end:

```text
Create workspace
↓
Create agent
↓
Connect API
↓
Discover
↓
Generate tool
↓
Test
↓
Publish
↓
Execute
↓
Audit
```

AI evaluation should include:
- RAG questions
- tool-selection cases
- parameter-resolution cases
- invalid requests
- permission-denied cases
- approval-required cases
- API failures
- citation cases

---

# 38. DEFINITION OF DONE

A feature is complete only when applicable:

```text
[ ] Existing implementation inspected
[ ] Existing patterns reused
[ ] Requirements understood
[ ] Backend implemented
[ ] Frontend implemented
[ ] Authorization implemented
[ ] Tenant/workspace isolation verified
[ ] Validation implemented
[ ] Error handling implemented
[ ] Audit considered
[ ] Database migration reviewed
[ ] Tests added/updated
[ ] Build succeeds
[ ] Tests pass
[ ] UI verified
[ ] Security reviewed
[ ] Documentation updated
```

---

# 39. REQUIRED CHANGE REPORT

After implementation, report:

```text
## Changed
- files/modules

## Reused
- existing services/components/entities

## Added
- new implementation

## Database
- migration required: yes/no
- details

## API
- endpoints changed/added

## UI
- screens/components changed

## Security
- authorization/policy impact

## Tests
- tests added/updated
- results

## Risks / TBD
- unresolved items
```

Never claim more than was verified.

---

# 40. MIGRATION FROM OLD CONCEPTS

If old code contains Department/Application concepts:

```text
Inventory usage
↓
Identify database relationships
↓
Identify API dependencies
↓
Identify UI dependencies
↓
Identify authorization dependencies
↓
Define migration
↓
Migrate
↓
Regression test
↓
Remove only when safe
```

Target:

```text
Tenant
↓
Workspace
↓
Agent / Connection / Knowledge / Automation
```

Do not delete legacy concepts before understanding their dependencies.

---

# 41. CODING AGENT FINAL CHECKLIST

Before every implementation ask:

1. Does this already exist?
2. Where is it implemented?
3. Can I reuse it?
4. Does the current architecture support it?
5. What is the smallest safe change?
6. Does it affect security?
7. Does it affect tenant/workspace isolation?
8. Does it affect the database?
9. Does it affect API compatibility?
10. What tests prove it works?
11. What could this break?
12. Is it actually required for R2WAI 2.0?
13. Am I adding unnecessary complexity?
14. Am I inventing anything I did not verify?

If uncertain:

> **STOP AND INSPECT. DO NOT GUESS.**

---

# 42. MASTER PRODUCT RULE

```text
R2WAI 2.0

Simple outside.
Powerful inside.
Dynamic where safe.
Strongly typed where necessary.
Governed by default.
Reusable by design.
Self-hosted + SaaS.
Existing systems first.
Business outcomes first.
No unnecessary rewrites.
No fake implementation claims.
No uncontrolled autonomy.

CONNECT
→ DISCOVER
→ BUILD
→ TEST
→ PUBLISH
→ MONITOR
→ IMPROVE
```

**End of R2WAI 2.0 AI Coding Agent Rules**
