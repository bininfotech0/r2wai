# R2WAI 2.0 — Architecture

## Architecture principle

```text
Strongly Typed Core
+
Dynamic Configuration
+
Schema-Driven Extensions
+
Controlled Provider Registry
```

## Logical architecture

```text
Experience Layer
├── Web Console
├── Agent Playground
├── Website Widget
└── REST API

Control Plane
├── Organization / Tenant
├── Workspace
├── Users / Roles
├── Agents
├── Connections
├── Knowledge
├── Automations
├── Publishing
└── Policies

Agent Runtime
├── Intent / Planning
├── Context Resolver
├── Knowledge Retrieval
├── Tool Selection
├── Parameter Resolution
├── Validation
├── Policy Evaluation
├── Approval
├── Execution
└── Response

Integration Engine
├── OpenAPI / REST
├── MCP
├── Webhooks
├── Database providers
└── Provider Registry

Knowledge Engine
├── Extraction
├── OCR
├── Normalization
├── Chunking
├── Embeddings
├── Retrieval
├── Reranking
└── Citations

Automation Engine
├── Triggers
├── Conditions
├── Actions
├── Scheduling
└── Execution Ledger

Governance
├── Identity
├── Authorization
├── Policy
├── Approval
├── Audit
├── Secrets
└── Rate Limits
```

## Repository layering

Expected Clean Architecture direction:

```text
API
 ↓
Application
 ↓
Domain
 ↑
Infrastructure
```

Verify the exact implementation in the repository before changing it.

## Core rule

Customer-specific business agents should normally be configuration and metadata, not custom backend classes.

New protocol/provider types require controlled implementation and registration.

## Execution path

```text
User
→ Agent
→ Intent
→ Context
→ Knowledge / Tools
→ Parameters
→ Validation
→ Policy
→ Approval
→ Execution
→ Result validation
→ Response
→ Audit
```

## Scale direction

```text
Web / Widget
→ Load Balancer / Gateway
→ API
→ Queue
→ Execution Workers
→ Model Gateway
→ Model providers
```

Do not prematurely introduce distributed infrastructure without a concrete requirement.
