# R2WAI — Complete Product UX Analysis Prompt

> **How to use:** Copy everything from the horizontal rule below into an AI chat (Claude, ChatGPT, etc.) and send it. The AI will return a structured analysis of every R2WAI feature from the perspective of ease-of-use vs. complexity.

---

## SYSTEM CONTEXT

You are a senior UX analyst specializing in enterprise software evaluation. You will perform a complete, structured analysis of **R2WAI** — an Enterprise AI Work Execution Platform.

### What is R2WAI?

R2WAI is a self-hosted, multi-tenant enterprise platform that combines:

- **AI Assistant Studio** — Build domain-specific AI assistants backed by LLMs (Ollama, OpenAI, Azure OpenAI) using Semantic Kernel
- **RAG Knowledge Bases** — Upload documents and enable semantic search with vector embeddings (pgvector)
- **Visual Workflow Automation** — Drag-and-drop workflow designer (Elsa 3.x) with human-in-the-loop approval chains
- **Approval Engine** — Policy-based multi-level approvals with SLA tracking and escalation
- **Enterprise Chatbots** — Embeddable website chatbots backed by assistants and knowledge bases
- **Integrations Marketplace** — 20+ connectors (Salesforce, Slack, Jira, GitHub, etc.)
- **Operations Center** — Real-time monitoring, AI usage analytics, audit logs

**Tech stack (for context):** .NET 10 / Blazor Server / MudBlazor UI / PostgreSQL + pgvector / Redis / MinIO / SignalR / SSE streaming

**Scale:** 42 UI pages, 15 API controllers, 64 permission flags, 5 user roles

---

## USER ROLES

Analyze every feature through the lens of these 5 roles:

| Role | Who They Are | Primary Goal |
|------|-------------|--------------|
| **Administrator** | IT admin / Platform operator | Configure the system, manage users, models, security |
| **Business User** | Knowledge worker, analyst | Chat with AI, build assistants, upload docs, create workflows |
| **Approver** | Manager, compliance officer | Review and approve/reject pending workflow steps |
| **Operator** | DevOps / monitoring staff | Read-only visibility into workflows, metrics, health |
| **Viewer** | Stakeholder, auditor | View their own conversations and workflow history only |

---

## USER KNOWLEDGE TIERS

In addition to roles, analyze every feature through these **3 IT knowledge levels** that exist in the real market:

| Tier | Label | Who They Are | Tech Comfort Level | Example Users |
|------|-------|-------------|-------------------|---------------|
| **Tier 1** | Base | Non-technical end users | Can use email, web apps, Microsoft Office. Cannot read error messages, has no concept of APIs or tokens | HR staff, sales reps, administrative assistants, general employees |
| **Tier 2** | Medium | Semi-technical power users | Comfortable with dashboards, can follow a setup guide step-by-step, understands concepts like "link", "upload", "connect". Cannot configure APIs but can fill in a form with a URL | Business analysts, operations managers, marketing managers, team leads |
| **Tier 3** | High IT | Technical / IT-savvy users | Comfortable with APIs, endpoint URLs, authentication tokens, configuration files, command-line basics, error logs | IT admins, developers, DevOps engineers, solutions architects, system integrators |

**For every feature, answer:**
- Can a **Base** user complete this independently?
- Can a **Medium** user complete this with a short guide?
- Does this require **High IT** knowledge?
- What is the **minimum tier** needed to use this feature successfully?

---

## ANALYSIS TASK

For each of the 10 major feature areas below, produce a complete UX analysis. Use the exact output format specified at the end of this prompt.

---

### FEATURE 1 — Authentication & Onboarding

**Pages:** Login, Register, ForgotPassword, ResetPassword, Profile
**Capabilities:**
- Email/password login with JWT (15-min access + 7-day refresh tokens)
- TOTP-based MFA (setup, disable, backup codes)
- Azure Entra ID / Microsoft SSO via OAuth2
- Password reset via email token
- Profile management with avatar upload
- First-time user invite flow (admin sends invite email, user sets password)

**Analyze:** Is the login and onboarding experience simple for a new enterprise user? Does MFA add too much friction? How does SSO change the experience?
**Tier question:** Can a Base user log in and set up their profile without IT help? What is the minimum knowledge tier to independently complete MFA setup?

---

### FEATURE 2 — AI Assistant Studio

**Pages:** Assistants.razor, AssistantPlayground.razor, TemplateLibrary.razor
**Capabilities:**
- Create assistants from 6 templates (HR, IT, Finance, Legal, Procurement, General)
- Write custom system prompts or use AI-generated prompt suggestions
- Link knowledge bases for RAG (Retrieval Augmented Generation)
- Register custom tools (REST API calls, email, workflow triggers)
- Lifecycle: Draft → Publish → Unpublish → Archive
- Playground: test the assistant in a live chat before publishing
- Track token usage per assistant

**Analyze:** Can a non-technical Business User create a useful assistant without understanding LLM prompting? Does the template system reduce complexity enough? Is the publish lifecycle intuitive?
**Tier question:** Can a Base user create a useful assistant using only templates? Does Medium tier cover custom prompts? Is tool registration (REST API, webhook triggers) strictly High IT territory?

---

### FEATURE 3 — Knowledge Base & Document Management

**Pages:** KnowledgeBases.razor, Documents.razor
**Capabilities:**
- Create named knowledge bases with configurable embedding models
- Upload individual or bulk documents (PDF, DOCX, XLSX, PPTX, TXT, Markdown, JSON, CSV — 50MB limit, 20 files max bulk)
- Async processing pipeline: upload → OCR → chunk → embed → index in pgvector
- Document status tracking (Processing, Ready, Error) with auto-refresh polling
- Retry failed document processing via Reindex button
- Semantic search with relevance scoring and source citations
- Add sources from URLs or raw content
- Link knowledge bases to assistants and chatbots

**Analyze:** Does the asynchronous processing (users must wait for "Ready" status) cause UX friction? Is the embedding model selection confusing for non-technical users? Is bulk upload easy enough?
**Tier question:** Can a Base user upload files and wait for processing with no explanation? Does "embedding model" selection require High IT knowledge? What is the minimum tier to configure and use a knowledge base end-to-end?

---

### FEATURE 4 — Chat & Conversations

**Pages:** Conversations.razor
**Capabilities:**
- Create named conversations (optionally tagged by module)
- Send messages with optional file attachments (up to 50MB)
- Real-time streaming responses via Server-Sent Events (SSE)
- Suggested next actions after each response
- Conversation history with pagination
- Idempotency keys to prevent duplicate message processing
- Module-based conversation filtering (chat per department or project)

**Analyze:** Is day-to-day AI chat intuitive for a Business User? Is the streaming UX (watching tokens arrive) familiar enough from consumer AI tools? Does file attachment work naturally?
**Tier question:** Is Chat a Base-tier feature (anyone can use it)? Does "module tagging" require Medium understanding? Which parts of chat, if any, require High IT knowledge?

---

### FEATURE 5 — Workflow Designer & Automation

**Pages:** Workflows.razor, WorkflowDesigner.razor, WorkflowInstanceDetail.razor, Schedules.razor
**Capabilities:**
- Visual drag-and-drop designer powered by Elsa 3.x
- 5 pre-built templates: Invoice Approval, Purchase Request, Employee Onboarding, Travel Request, Vendor Approval
- Step types: AI generation, human approval, API call, email notification, conditional branching
- Lifecycle: Draft → Publish → Unpublish → Archive (with version management)
- Manual or scheduled execution (cron-triggered)
- Webhook trigger with custom JSON payload
- Instance execution tracking with step-level logs
- Retry failed steps individually
- Workflow versioning (create new version from existing)

**Analyze:** Can a Business Analyst design a useful workflow without coding? Do the 5 templates cover common enterprise scenarios? Is the publish/version lifecycle clear? Is retry-on-failure intuitive?
**Tier question:** Can a Base user run a workflow from a template? Does designing a custom workflow from scratch require Medium or High IT knowledge? Is webhook triggering (custom JSON payload) exclusively a High IT concern?

---

### FEATURE 6 — Approval Engine

**Pages:** Approvals.razor, Inbox.razor
**Capabilities:**
- Create approval policies (minimum approver count, assigned roles, SLA duration)
- Multi-level approval chains (sequential or parallel)
- SLA escalation: when overdue, auto-escalate to designated escalation roles
- Approve or reject with optional comment
- Role-based approval inbox (see only your assigned approvals)
- Background service monitors overdue approvals every minute
- Workflow automatically resumes on approval decision (via WorkflowBridge)
- Track status: Pending, Approved, Rejected, Escalated

**Analyze:** Is the approver experience simple (just approve/reject)? Is creating an approval policy complex for an Administrator? Is the SLA escalation logic transparent to users?
**Tier question:** Is approving/rejecting a Base-tier action any office worker can do? Does setting up an approval policy with SLA rules require Medium or High IT knowledge? Can a Base user understand why an approval was escalated?

---

### FEATURE 7 — Chatbot Builder & Deployment

**Pages:** Chatbots.razor, ChatbotWidget.razor
**Capabilities:**
- Create named chatbots with custom welcome messages and suggested starter questions
- Link to an AI model configuration (the "brain") — required for the chatbot to respond
- Optionally link to a knowledge base for RAG context
- Customize system prompt overlay
- Generate embed code (JavaScript snippet) for deployment to any website
- Chatbot widget loads as floating button on the host website
- Real-time streaming chat in the widget
- Inline setup guide shown when prerequisites (AI model, knowledge base) are missing

**Analyze:** Is the two-component linkage (chatbot → AI model + optional knowledge base) confusing for a Product Manager who just wants a simple FAQ bot? Is the embed code deployment straightforward?
**Tier question:** Can a Base user configure and test a chatbot inside the platform? Does embedding it on a website (copy-paste JavaScript snippet) require Medium or High IT knowledge? Is the component mental model (chatbot → AI model + KB) accessible to Medium tier?

---

### FEATURE 8 — Integrations Marketplace

**Pages:** Integrations.razor, Tools.razor
**Capabilities:**
- 20+ pre-configured connector templates (Salesforce, Slack, Jira, GitHub, HubSpot, ServiceNow, etc.)
- Configure endpoint URL, authentication type (Bearer token, Basic auth, API key header)
- Network security validation: blocks private IP ranges and internal domains
- Test connectivity with a live ping
- Toggle integrations active/inactive
- Integrations are available as callable steps in workflow designer
- REST API abstraction with custom tool definitions

**Analyze:** Is finding and connecting a specific integration (e.g., Slack) straightforward for an IT Admin? Is the endpoint URL + auth configuration too technical for a Business User? Is integration testing reassuring?
**Tier question:** Is this feature entirely High IT territory (endpoint URLs, Bearer tokens, OAuth2)? Can a Medium user connect a simple integration with a step-by-step guide? Should Base users ever touch this feature?

---

### FEATURE 9 — Operations & Monitoring

**Pages:** Operations.razor, AiOperations.razor, AuditLogs.razor, ErrorLogs.razor, Reports.razor, UsageAnalytics.razor
**Capabilities:**
- System health dashboard (database, cache, API status — green/yellow/red)
- Real-time workflow execution monitoring
- AI usage analytics: token consumption per model, conversation counts, response times
- Cost reports: estimated LLM costs based on token usage
- Compliance reports: audit trail with filtering by user, action, entity, date range
- Export audit logs (CSV or JSON)
- Error log viewer with stack trace access
- Prometheus metrics endpoint (/metrics)
- Correlation ID tracking across requests

**Analyze:** Is the operations dashboard useful for an Operator who is not an AI expert? Are the cost and usage reports clear for a manager? Is the audit log export sufficient for a Compliance Officer?
**Tier question:** Can a Base-tier manager read and understand AI cost and usage reports? Does interpreting Prometheus metrics or error logs with stack traces require High IT knowledge? What is the minimum tier for meaningful use of this section?

---

### FEATURE 10 — Admin & Settings

**Pages:** AdminUsers, AdminRoles, AdminPermissions, AdminModels, AdminApiKeys, AdminSecurity, AdminWebhooks, AdminContentModeration, Settings, TenantSettings
**Capabilities:**
- User CRUD: create, update, deactivate, reset password, send invite email
- Role management: 5 default roles + custom roles with 64 permission flags (bitfield)
- Permission matrix: assign/revoke fine-grained permissions per role
- LLM model configuration: add models (Ollama endpoint with autocomplete model suggestions, OpenAI key, Azure deployment)
- Model connection testing: send a sandbox prompt to verify the model works
- API key generation with encrypted storage and scoped permissions
- MFA policy enforcement (require MFA for all users)
- Content moderation rules (block/allow topics, profanity filters)
- Webhook endpoint registration for inbound events
- Tenant-level feature flags

**Analyze:** Is the permission matrix (64 flags across 5 roles) manageable for an IT Admin or overwhelming? Is model configuration (Ollama endpoint, OpenAI key, Azure deployment ID) too technical for a non-developer? Does the "test connection" feature reduce anxiety around model setup?
**Tier question:** Is Admin & Settings exclusively High IT territory? Can Medium tier handle user management and role assignment? Is configuring an LLM model (Ollama endpoint, API key, embedding model name) something only High IT can do correctly?

---

## ONBOARDING FRICTION ASSESSMENT

Separately analyze:

**Path to first value** — What is the minimum number of steps a brand-new Business User must complete before they can have their first useful AI conversation? List each step explicitly.

**Hidden complexity traps** — List the 5 features that appear simple but hide significant complexity underneath (e.g., a feature that looks like "just upload a file" but actually requires understanding embedding models, processing pipelines, etc.).

**First-login experience** — Based on the Home.razor dashboard (KPI metrics, recent activity, AI insights, system health, Getting Started guide), is the starting screen welcoming and oriented for a new user, or does it assume too much prior knowledge?

---

## USER JOURNEY COMPLEXITY HEATMAP

Produce a visual complexity map using this table format. Rate each journey:
- **[EASY]** — Any user with basic computer skills can do it without help
- **[MEDIUM]** — Requires some training or a one-time setup guide
- **[COMPLEX]** — Requires technical knowledge, IT support, or significant learning investment

For the **Min Tier** column use: `Base` / `Medium` / `High IT`

| User Journey | Primary Role | Complexity | Min Tier Required | Key Friction Points |
|---|---|---|---|---|
| Login with email/password | All | | | |
| Login with Microsoft SSO | All | | | |
| Set up MFA (TOTP) | All | | | |
| Have a chat conversation with AI | Business User | | | |
| Attach a file to a chat message | Business User | | | |
| Create an assistant from a template | Business User | | | |
| Write a custom system prompt | Business User | | | |
| Upload a document to a knowledge base | Business User | | | |
| Wait for document processing to complete | Business User | | | |
| Run a semantic search on a knowledge base | Business User | | | |
| Create a workflow from a template | Business User | | | |
| Design a custom workflow from scratch | Business User | | | |
| Publish a workflow | Business User | | | |
| Approve a pending approval request | Approver | | | |
| View SLA status of an approval | Approver | | | |
| Create an approval policy | Administrator | | | |
| Build and deploy a website chatbot | Business User | | | |
| Connect a Slack integration | Administrator | | | |
| Configure an LLM model | Administrator | | | |
| Set up user roles and permissions | Administrator | | | |
| View AI usage cost report | Operator | | | |
| Export audit logs for compliance | Operator / Viewer | | | |
| Generate an RFP response (proposal) | Business User | | | |

---

## REQUIRED OUTPUT FORMAT

Structure your response in these exact sections:

### EXECUTIVE SUMMARY
2–3 sentences: overall verdict on whether R2WAI leans toward easy-to-use or complex, and for which user types.

### ROLE-BY-ROLE VERDICT
For each of the 5 roles: one paragraph covering which features serve them well, where they'll struggle, and an overall difficulty rating (Beginner-Friendly / Intermediate / Expert-Required).

### TIER-BY-TIER VERDICT
For each of the 3 knowledge tiers, give a clear verdict:

**Base Tier (no IT knowledge)**
- Which features can they use independently?
- Which features will block them or cause confusion?
- What % of the platform is accessible to them?
- Recommendation: what onboarding help do they need?

**Medium Tier (some IT familiarity)**
- Which features open up compared to Base?
- Where does their knowledge hit a ceiling?
- What % of the platform is accessible to them?
- Recommendation: what documentation or guided setup covers the gap?

**High IT Tier**
- Which features are exclusively theirs?
- Is the platform's complexity appropriate for this tier or over-engineered?
- What % of the platform is accessible to them?
- Recommendation: any features that could be simplified even for this tier?

### FEATURE SCORING TABLE
Fill in this table for all 10 features:

| # | Feature | Primary User | Ease Score (1=Hard, 5=Easy) | Min Tier (Base/Medium/High IT) | Top Complexity Driver | Top Simplicity Aid | Verdict |
|---|---|---|---|---|---|---|---|
| 1 | Authentication & Onboarding | All | | | | | |
| 2 | AI Assistant Studio | Business User | | | | | |
| 3 | Knowledge Base & Documents | Business User | | | | | |
| 4 | Chat & Conversations | Business User | | | | | |
| 5 | Workflow Designer | Business User | | | | | |
| 6 | Approval Engine | Approver / Admin | | | | | |
| 7 | Chatbot Builder | Business User | | | | | |
| 8 | Integrations Marketplace | Administrator | | | | | |
| 9 | Operations & Monitoring | Operator | | | | | |
| 10 | Admin & Settings | Administrator | | | | | |

### TIER ACCESSIBILITY SUMMARY
Fill in this table — for each feature, mark which tiers can use it independently:

| Feature | Base User (no IT) | Medium User (some IT) | High IT User |
|---|---|---|---|
| Authentication & Onboarding | | | |
| AI Assistant Studio (templates) | | | |
| AI Assistant Studio (custom prompts + tools) | | | |
| Knowledge Base — upload documents | | | |
| Knowledge Base — configure embedding model | | | |
| Chat & Conversations | | | |
| Workflow — run from template | | | |
| Workflow — design from scratch | | | |
| Workflow — webhook trigger | | | |
| Approval — approve/reject | | | |
| Approval — create policy + SLA | | | |
| Chatbot — build inside platform | | | |
| Chatbot — embed on website | | | |
| Integrations — configure & connect | | | |
| Operations & Reports — read dashboards | | | |
| Operations — Prometheus / error logs | | | |
| Admin — manage users & roles | | | |
| Admin — configure LLM models | | | |

Use: `✅ Yes, independently` / `⚠️ With a guide` / `❌ Cannot do alone`

### JOURNEY COMPLEXITY HEATMAP
Fill in the completed table (from the heatmap section above).

### ONBOARDING FRICTION REPORT
- Steps to first AI conversation (numbered list)
- Hidden complexity traps (numbered list of 5)
- First-login screen verdict

### TOP 3 EASY WINS
Features or flows that are done well and create immediate user delight. Explain why each works.

### TOP 3 PAIN POINTS
The most likely sources of user drop-off or support tickets. For each: describe the problem, who it affects, and a concrete UX recommendation to fix it.

### PROGRESSIVE DISCLOSURE RECOMMENDATION
Which features should be shown to users immediately (Level 1), after they've completed onboarding (Level 2), and only after they've reached advanced maturity with the platform (Level 3)?

### FINAL VERDICT
One clear sentence: Is R2WAI **easy to use**, **moderately complex**, or **complex** — and for which audience?
