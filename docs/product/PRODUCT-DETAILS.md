# R2WAI — Product Details

> **Status as of 2026-10-11**, checked against the code in this repository, including work that is
> not yet committed. Status words used below:
> **Live** = works end to end today. **Partial** = real but with a stated gap.
> **Not built** = configuration or placeholder only.
> Product direction and principles: [PRODUCT-VISION.md](PRODUCT-VISION.md). Technical gap list:
> [audit/FEATURE-MATRIX.md](../audit/FEATURE-MATRIX.md).

## 1. What R2WAI is

R2WAI is a **self-hosted, multi-tenant enterprise AI agent platform**. An organization connects its
existing business systems and documents, builds AI assistants on top of them, governs what those
assistants may do, and publishes them to employees and website visitors. Everything runs on
infrastructure the organization controls, including fully local AI models.

**Built for** government and regulated organizations, and any business that wants AI assistants
without sending its data to an outside AI service.

**Core journey:** Connect → Create agent → Configure → Test → Publish → Monitor.

**Key differentiators**

- **Data stays in-house.** Runs on-premises with local models (Ollama). Cloud models are optional
  and never used silently.
- **The model is not trusted to act alone.** Every tool call passes a governance gateway: risk level,
  role checks, confirmation, human approval and a permanent execution record.
- **Knowledge and live data are kept apart.** Documents are answered through permission-aware search.
  Live records are only reached through approved, audited tools.
- **India-ready compliance controls.** Detects and blocks or redacts Aadhaar, PAN, phone and email
  numbers, and stores Aadhaar only as a one-way hash.

## 2. Who uses it

| Role | What they see |
|---|---|
| **User** (employee) | Personal AI assistant, knowledge search, own activity history, notifications, profile |
| **Admin** | Everything a user sees, plus agents, connections, knowledge, automations, publishing, activity and monitoring, users, API keys |
| **Super Admin** | Everything an admin sees, plus AI model setup, raw tools and APIs, security and policy settings, tenant management |
| **Website visitor** | Only the published chat widget. No account needed. |

The main navigation is **Home, Agents, Connections, Knowledge, Automations, Publish, Activity,
Settings**. Phone-sized screens get a bottom navigation bar.

## 3. Modules and features

### 3.1 Home

Live dashboard with real usage numbers, a setup checklist and recent activity. **Live.**

### 3.2 Agents (AI assistants)

| Feature | Status |
|---|---|
| Create, edit, clone and archive assistants. Starter types: General, HR, IT, Procurement, Finance, Legal, Coaching Center, plus CMS and ERP templates such as WordPress, Shopify, Salesforce and SAP | Live |
| AI-generated starting configuration from a short description | Live |
| System prompt with placeholders filled per request: tenant, workspace, user name, user role, current date. The editor warns about unsupported placeholders | Live |
| Prompt template library per assistant type, and prompt change history | Live |
| Assign knowledge bases, tools and the AI model per assistant | Live |
| Versions: publish, roll back, unpublish. Each published version is an immutable snapshot | Live |
| Readiness check before publishing | Live |
| Playground for testing the real execution pipeline, with streaming replies | Live |
| Test cases and test runs, single or "run all", with stored results | Live |
| Choice of agent engine per tenant: Semantic Kernel or Microsoft Agent Framework | Live |

### 3.3 Connections

| Feature | Status |
|---|---|
| **Connected systems:** register business applications, auto-discover their APIs, keep configuration versions with rollback | Live |
| **REST APIs:** manual setup or OpenAPI/Swagger import with "analyze, then commit". Discovered operations are never switched on automatically | Live |
| **Default safety for discovered operations:** GET is low risk. POST and PUT are medium risk and need confirmation. DELETE is high risk and needs confirmation and approval | Live |
| **Auth for outbound APIs:** none, API key, OAuth, JWT, Microsoft Entra ID | Live |
| **MCP servers (Model Context Protocol):** connect, test, discover tools, choose which to commit | Partial. HTTP transport only, no local stdio servers |
| **Server-side request forgery protection** on outbound calls, and admin review before activation | Live |
| **Tool types:** HTTP, email, database, script, custom, MCP | Live (database and script tools not audited in depth) |
| **Business capabilities catalog** | Live |

### 3.4 Knowledge

| Feature | Status |
|---|---|
| Knowledge bases with versions and rollback | Live |
| Document upload, single or bulk: PDF, Word, Excel, PowerPoint, text, CSV, Markdown | Live |
| Web page (URL) sources, re-indexed on demand | Partial. No scheduled re-crawl |
| Hybrid search: vector plus keyword, weighted 70/30, on PostgreSQL pgvector | Live |
| Agentic retrieval: one bounded extra search with a rewritten query when the first one is weak | Live |
| Document tools: summarize, extract structured data, compare two documents, ask a question about a document | Live |
| Source citations are returned with streamed answers | Partial. The website widget does not show them |
| Tenant-isolated retrieval. One tenant can never search another tenant's data | Live |

### 3.5 Automations (workflows)

| Feature | Status |
|---|---|
| Visual drag-and-drop builder with Action, Process, Condition, Email and Approval steps, plus timed delays | Live |
| Start manually, from an assistant, or from an inbound webhook | Live |
| Draft, publish, versions, archive and restore | Live |
| Instance history with per-step results and retry | Live |
| Delays that survive a server restart | Live |
| Workflow templates with per-tenant overrides | Live |

The workflow engine is Elsa 3. Moving workflows onto the platform's own durable execution ledger
is on the roadmap.

### 3.6 Publish (channels)

| Channel | Status |
|---|---|
| **Website chat widget:** one script tag, a self-contained bundle of about 20 KB. Covers streaming replies, welcome message, suggested questions, voice input and read-aloud, file attachments, thumbs up or down feedback, and a "New chat" button | Live |
| **Widget controls:** draft, active or paused status, allowed-website list, "installed on site" detection, message counts. The tenant's daily AI usage cap also applies | Live |
| **Conversation memory:** the bot remembers the current conversation, up to its last 20 messages. Memory is kept for 7 days, then deleted | Live (new, uncommitted) |
| **Generic webhook channel:** HTTP request in, reply out, protected by a per-chatbot key. Supports conversation memory through a session ID | Live |
| **Assistant as an API:** API-key access to a published assistant, pinned to its published version | Live |
| **WhatsApp, Microsoft Teams, Slack, SMS** | Not built. Credentials can be saved, but incoming messages are not yet routed to the bot |

Built-in website chat rules on every public channel cover anonymous visitors, plain-text replies,
no actions and prompt-injection resistance. They are added automatically to each chatbot's own
prompt. Public channels can never run tools.

### 3.7 Activity and monitoring

| Feature | Status |
|---|---|
| **Activity:** one list of workflow runs and AI tool calls, with filters and a detail view per call | Live |
| **Tool execution ledger:** every tool call recorded as denied, awaiting approval, prepared, succeeded, failed or unknown | Live (new) |
| **Confirmations:** pending approvals by user or role, approve or reject, multi-level approval policies, automatic escalation of overdue requests | Live |
| **Monitor:** health checks, request metrics, daily trends, AI usage statistics, error log | Live |
| **Reports:** generate and download. Audit log export | Live |
| **Notifications:** in-app inbox with unread count, and email | Live |

### 3.8 Settings and administration

| Feature | Status |
|---|---|
| Users: invite, edit, roles, unlock, reset two-factor login, bulk delete. Access requests with approve or reject | Live |
| Custom roles with fine-grained permissions | Live |
| Tenants: create, edit, delete. Plan tiers Free and Premium | Live |
| **AI models:** add models per provider, test the connection, store API keys encrypted, set a fallback provider | Live |
| API keys with scopes: create, rotate, enable or disable | Live |
| Outbound webhooks for platform events | Live |
| Developer page with copy-ready API and widget samples | Live |
| Content moderation settings | Live |
| Usage analytics | Live |

## 4. AI and governance

**Supported model providers**

| Provider | Use |
|---|---|
| Ollama, local and self-hosted | Chat and embeddings. Default local model is qwen3:4b, embeddings use nomic-embed-text |
| OpenAI | Chat, default gpt-4o |
| Azure OpenAI | Chat |
| Z.ai GLM, through an OpenAI-compatible endpoint | Chat |

A model can be set per assistant, with a configurable fallback provider.

**Policy engine.** Each policy is set per tenant from Security & Policies:

| Policy | What it does |
|---|---|
| AI usage | Daily request cap per tenant. Public channels are included |
| PII | Detects Aadhaar, PAN, phone and email. Blocks or redacts them before the model sees them |
| Knowledge | Controls what knowledge an assistant may retrieve |
| Tool execution | Allow or deny tool calls by risk and role |
| Approval | Which actions need human approval, and from whom |
| Data retention | Automatically deletes conversations, messages and documents older than N days |
| Auth | Sign-in rules |
| Agent runtime | Which agent engine the tenant uses |

**Conversation memory.**
- **Signed-in chat:** the last 20 messages, with optional summarization of older ones.
- **Public chatbots:** per-visitor session memory with automatic expiry.

## 5. Security

- **Sign-in:** email and password with short-lived tokens of 15 minutes and refresh tokens of 7 days. Also two-factor login with an authenticator app, Microsoft Entra ID single sign-on, account lockout, and password reset.
- **Tenant isolation:** isolation fails closed. A request without a resolved tenant sees no data, and this is covered by regression tests.
- **Secrets:** model API keys and channel credentials are encrypted at rest. Sensitive fields are masked in audit logs.
- **Audit:** every data change is written to an audit log with the user, IP address and correlation ID.
- **Public endpoints:** each chatbot has an allowed-website list. The tenant's daily usage cap and PII checks also apply, plus tools off, attachments limited to 5 MB of images, PDF or plain text, and rate limiting.
- **Threat model:** see [security/THREAT-MODEL.md](../security/THREAT-MODEL.md).

## 6. Deployment and technology

| Area | Details |
|---|---|
| Backend | ASP.NET Core on .NET 10, clean architecture with domain, application, infrastructure and API layers, MediatR |
| Frontend | React 19, TypeScript, Material UI 9, Vite, React Query, React Flow for the workflow builder, SignalR for live chat |
| Widget | Plain TypeScript, no dependencies, isolated with shadow DOM |
| AI | Semantic Kernel 1.77, Microsoft Agent Framework 1.23, Model Context Protocol SDK 2.2 |
| Workflows | Elsa 3.7 |
| Data | PostgreSQL with pgvector, Redis cache, local disk or MinIO file storage |
| Packaging | Docker Compose with studio, API, PostgreSQL and Redis. Kubernetes manifests with ingress, network policy and config map |
| Operations | Serilog structured logs, health checks, metrics endpoints |
| Tests | .NET unit and integration tests, including Postgres container tests. Client unit tests with Vitest. Browser tests with Playwright |

Hardware note: a small local model on CPU only, such as qwen3:4b, answers in about 20 to 60
seconds. That is fine for evaluation. Production use needs a GPU or a larger hosted model.

## 7. Known gaps

1. **Messaging channels:** WhatsApp, Teams, Slack and SMS adapters are not built.
2. **Chatbot versioning:** the widget uses the chatbot's own settings, not a pinned assistant version. The standalone assistant API is pinned.
3. **MCP:** only HTTP-based MCP servers are supported. Local stdio servers are not.
4. **Knowledge:** web sources have no scheduled re-crawl.
5. **Workflows:** they still run on Elsa rather than the platform's own execution ledger.
6. **Departments:** removed from the UI. The API still exists.
7. **Widget tests:** the new "New chat" button has no browser test yet.

See [ROADMAP.md](../../ROADMAP.md) for sequencing.
