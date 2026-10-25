# R2WAI Production Readiness Register — 2026-10-24

## Verdict

R2WAI is **not yet ready to promise reliable service to thousands of users**. This register contains 100 distinct production-readiness items: 32 changes completed in the first implementation pass, 13 source-confirmed gaps still open, 10 operational gates that need a real deployment, 30 carried-forward findings (G076 has been partially revalidated and two access-control subgaps fixed; its full route matrix remains open), and 15 larger product-capability gaps. These are not 100 observed production incidents.

The highest risks before a customer rollout are a complete API authorization review, applying the new migrations after checking existing data, a real restore drill, and measured load/failover behavior. The recent source build and manifest checks do not prove runtime scale or availability.

## How to read this register

- **Fixed** means the change is in the current working tree and the solution compiled. It does not mean a production deployment or security test was run.
- **Open—checked** means the current source or deployment manifest shows the gap.
- **Recheck** means a direct code audit on 2026-10-15 found it; newer edits may have changed it, so verify before scheduling implementation.
- **Deployment gate** means code alone cannot establish the result; it needs a customer-like environment and operational evidence.
- **Capability** means a product-depth item, not a broken function in the current scope.

The earlier [deep audit](PRODUCTION-READINESS-DEEP-AUDIT-2026-10-15.md) contains additional line-level evidence for carried-forward items. This document supersedes its old status claims where this pass changed the code.

## A. Changes completed in this pass

| ID | Priority | Change | Evidence / status |
|---|---|---|---|
| 001 | P0 | Removed the API's direct public host port from production Compose. | `docker/docker-compose.production.yml` — Fixed; config parsed. |
| 002 | P0 | Removed the PostgreSQL host port from production Compose. | `docker/docker-compose.production.yml` — Fixed; config parsed. |
| 003 | P0 | Required Redis authentication in production Compose. | `docker/docker-compose.production.yml`, `.env.production.example` — Fixed. |
| 004 | P0 | Required Redis authentication in the Kubernetes Redis manifest. | `k8s/redis.yaml` — Fixed; Kustomize rendered. |
| 005 | P0 | Set the demo-seeded local Compose profile to Development. | `docker/docker-compose.yml` — Fixed. |
| 006 | P0 | Made production startup reject demo-account seeding. | `src/R2WAI.Infrastructure/Persistence/DatabaseInitializer.cs` — Fixed. |
| 007 | P0 | Made startup fail when the database is unavailable instead of serving a broken API. | `src/R2WAI.Api/Program.cs` — Fixed; solution compiled. |
| 008 | P0 | Require a real bootstrap administrator on an empty non-development database. | `src/R2WAI.Infrastructure/Persistence/DatabaseInitializer.cs` — Fixed. |
| 009 | P1 | Added a production Nginx TLS example with HTTP-to-HTTPS redirect. | `docker/nginx/studio.production.conf`, `docker/docker-compose.production.yml` — Fixed. |
| 010 | P1 | Set the API container to a non-root UID/GID and match the Kubernetes pod security context. | `docker/Dockerfile.api`, `k8s/api-deployment.yaml` — Fixed. |
| 011 | P1 | Added persistent storage to the Kubernetes API pod. | `k8s/api-deployment.yaml` — Fixed for one replica; shared storage remains open at ID 037. |
| 012 | P0 | Closed assistant data exposure: ordinary users get published public metadata only; prompt, version, readiness, and template endpoints require admin access. | `src/R2WAI.Api/Controllers/AssistantsController.cs` — Fixed. |
| 013 | P0 | Upgraded vulnerable OpenAPI/.NET packages and changed CI to fail on High or Critical dependency advisories. | `src/R2WAI.Infrastructure/R2WAI.Infrastructure.csproj`, `tests/R2WAI.Api.Tests/R2WAI.Api.Tests.csproj`, `.github/workflows/ci.yml` — Fixed; fresh solution scan is clean. |
| 014 | P0 | Expanded outbound-address rejection to private, special-use, and IPv6 ranges. | `src/R2WAI.Infrastructure/Security/EgressGuard.cs` — Fixed. |
| 015 | P0 | Validated resolved addresses at socket connect time to resist DNS rebinding. | `src/R2WAI.Infrastructure/Security/EgressGuard.cs` — Fixed. |
| 016 | P1 | Added explicit hostname/CIDR allowlists for approved on-prem destinations. | `src/R2WAI.Infrastructure/Security/EgressGuard.cs`, `docs/deployment/RUNBOOK.md` — Fixed; deployment must still choose narrow ranges. |
| 017 | P0 | Routed workflow HTTP, MCP, OpenAPI import, knowledge URL fetch, and tool HTTP through the guarded client. | `src/R2WAI.Infrastructure/Services/ToolFramework/HttpToolClient.cs` and call sites — Fixed; solution compiled. |
| 018 | P1 | Disabled automatic HTTP redirects in guarded outbound clients. | `src/R2WAI.Infrastructure/Services/ToolFramework/HttpToolClient.cs` — Fixed for SSRF safety; compatibility gap ID 040 remains. |
| 019 | P1 | Reduced sensitive URL logging to the origin/host in outbound integrations. | `src/R2WAI.Infrastructure` outbound clients — Fixed. |
| 020 | P0 | Removed the global webhook-secret fallback and constrained workflow lookup to the endpoint tenant. | `src/R2WAI.Api/Controllers/WorkflowsController.cs` — Fixed. |
| 021 | P0 | Verified webhook signatures over raw request bytes with a timestamp freshness check. | `src/R2WAI.Api/Controllers/WorkflowsController.cs` — Fixed. |
| 022 | P0 | Encrypted webhook secrets and stopped returning the secret from the detail API. | `src/R2WAI.Api/Controllers/WebhooksController.cs`, `src/R2WAI.Domain/Entities/WebhookEndpoint.cs` — Fixed. |
| 023 | P1 | Preserved an existing secret when a webhook edit omits it; improved the dialog with a workflow picker and generated secret. | `src/R2WAI.Client/src/features/admin/dialogs/CreateEditWebhookDialog.tsx`, `src/R2WAI.Client/src/features/settings/components/DeveloperTab.tsx` — Fixed; client build succeeded. |
| 024 | P1 | Replaced the coarse global limiter with pre-auth IP protection and tighter route/user policies. Public widget and webhook chat also enforce a 10,000-character message cap and bounded request bodies before model work. | `src/R2WAI.Api/Middleware/RateLimitingMiddleware.cs`, `src/R2WAI.Api/Program.cs`, `src/R2WAI.Api/Controllers/ChatbotsController.cs` — Fixed in source. |
| 025 | P1 | Made Redis rate-limit increments atomic and the local fallback thread-safe. | `src/R2WAI.Infrastructure/Cache/RedisCacheService.cs`, `src/R2WAI.Infrastructure/Cache/InMemoryCacheService.cs` — Fixed in source. |
| 026 | P1 | Added trusted forwarded-header handling for the configured production gateway. | `src/R2WAI.Api/Program.cs`, `docker/docker-compose.production.yml` — Fixed for the Compose gateway; Kubernetes config remains open at ID 033. |
| 027 | P1 | Made scheduled workflow dispatch replica-safe with a database advisory lock and persisted catch-up cursor. | `src/R2WAI.Api/Services/ScheduledWorkflowTriggerService.cs`, `src/R2WAI.Domain/Entities/Workflow.cs` — Fixed in source. |
| 028 | P1 | Added claims/leases to delayed workflow resumption. | `src/R2WAI.Api/Services/WorkflowDelayResumeBackgroundService.cs` — Fixed in source. |
| 029 | P1 | Made approval decisions conditional database updates to prevent two concurrent decisions winning. | `src/R2WAI.Infrastructure/Services/ApprovalService.cs` — Fixed in source. |
| 030 | P1 | Bounded retention deletion into small batches rather than deleting all expired records in one operation. | `src/R2WAI.Infrastructure/AI/Policies/DataRetentionService.cs` — Fixed in source. |
| 031 | P0 | Removed ambiguous approval-policy selection: approval requests now resolve the workflow-specific policy, policy lookup is tenant-scoped, and the database permits only one active policy per tenant/workflow type. Migration fails clearly if existing duplicates need cleanup. | `src/R2WAI.Infrastructure/Services/ApprovalService.cs`, `src/R2WAI.Infrastructure/Persistence/Configurations/ApprovalPolicyConfiguration.cs`, `src/R2WAI.Infrastructure/Persistence/Migrations/20261024155658_EnsureUniqueActiveApprovalPolicies.cs` — Fixed in source; migration SQL generated, not applied to a production database. |
| 032 | P0 | Added database-backed webhook idempotency: stable provider delivery IDs are preferred, signature/timestamp is the fallback, and duplicates do not start a second run. | `src/R2WAI.Api/Controllers/WorkflowsController.cs`, `src/R2WAI.Domain/Entities/WebhookDeliveryRecord.cs`, `src/R2WAI.Infrastructure/Persistence/Migrations/20261024160454_AddWebhookDeliveryIdempotency.cs`, `docs/deployment/RUNBOOK.md` — Fixed in source; migration SQL generated, not applied to production. |

## B. Open gaps confirmed in the current source or manifests

| ID | Priority | Gap and required action | Evidence / status |
|---|---|---|---|
| 033 | P1 | Kubernetes does not set the trusted ingress/proxy network for forwarded client IPs. Configure the real cluster ingress CIDR or rate limits can collapse to a proxy address. | `k8s/configmap.yaml`, `src/R2WAI.Api/Program.cs` — Open; deployment-specific value required. |
| 034 | P1 | Kubernetes images still use `:latest`; pin immutable release tags or digests and record the deployed image. | `k8s/api-deployment.yaml` — Open; checked. |
| 035 | P0 | The Kubernetes HPA is explicitly capped at one API replica. It cannot scale out under load. | `k8s/api-deployment.yaml` — Open; checked. |
| 036 | P1 | SignalR has no configured multi-instance backplane. Notifications and streaming cannot be assumed to reach clients across API replicas. | `src/R2WAI.Api/Program.cs`, `src/R2WAI.Infrastructure/DependencyInjection.cs`, `k8s/api-deployment.yaml` — Open; checked. |
| 037 | P1 | Kubernetes storage is local/PVC-backed, not shared object storage. Move uploads and durable artifacts to an S3-compatible store before multi-replica deployment. | `k8s/configmap.yaml`, `k8s/api-deployment.yaml` — Open; checked. |
| 038 | P1 | Logs and document-processing temporary files remain pod-local. Centralize logs and make temporary processing recoverable/observable. | `k8s/api-deployment.yaml` comments and storage mounts — Open; checked. |
| 039 | P1 | The secure HTTP handler disables outbound proxy use. Deployments that require a corporate egress proxy need an explicit proxy-aware, destination-validating path. | `src/R2WAI.Infrastructure/Services/ToolFramework/HttpToolClient.cs` — Open; checked. |
| 040 | P2 | Disabling automatic redirects can break integrations that legitimately redirect. Add a manual redirect loop that re-runs the egress check for every hop. | `src/R2WAI.Infrastructure/Services/ToolFramework/HttpToolClient.cs` — Open compatibility work. |
| 041 | P1 | The included Compose Postgres service is a single database node. Production availability needs managed/replicated PostgreSQL and a tested failover plan. | `docker/docker-compose.production.yml` — Open; checked. |
| 042 | P1 | The included Redis deployment has no failover topology. Decide managed Redis or a supported HA setup for distributed rate limits and background coordination. | `docker/docker-compose.production.yml`, `k8s/redis.yaml` — Open; checked. |
| 043 | P0 | There is no measured load result for 1,000+ concurrent users, chat requests, or workflow starts. Run representative load profiles before setting a safe capacity. | No current load report found; deployment gate. |
| 044 | P1 | Capacity and performance budgets are not based on measurements. Tune API/DB/Redis/model/queue limits from load results, and set a web startup budget for the ~854 KB initial client bundle. Conversation, workflow-run, and audit-log history now use database-side ordering and pagination, preventing page requests from materializing entire histories. | `k8s/api-deployment.yaml`, `src/R2WAI.Application/Features/Chat/Queries/GetMessagesQuery.cs`, `src/R2WAI.Application/Features/Workflows/Queries/GetWorkflowInstancesQuery.cs`, `src/R2WAI.Application/Features/Admin/Queries/GetAuditLogsQuery.cs`, `src/R2WAI.Infrastructure/Persistence/Repositories/GenericRepository.cs`; latest client build output — capacity budget remains open; pagination fixes are in source. |
| 045 | P0 | Webhook slugs are part of a public URL, but endpoint lookup ignores tenant filters and the database did not enforce global uniqueness. Duplicate slugs could route requests to an arbitrary tenant; add a unique index and fail the migration on existing conflicts. | `src/R2WAI.Api/Controllers/WorkflowsController.cs`, `src/R2WAI.Api/Controllers/WebhooksController.cs`, `src/R2WAI.Infrastructure/Persistence/Migrations/20261024160623_EnsureGlobalWebhookSlugs.cs` — Open until the migration is applied. |

## C. Operational gates requiring a real deployment

| ID | Priority | Gate | Evidence / status |
|---|---|---|---|
| 046 | P0 | Load-test login, anonymous chat, assistant chat, webhook, document ingest, and workflows at expected peak concurrency. | Not run in this pass; no customer-like environment/load profile provided. |
| 047 | P0 | Measure p50/p95/p99 latency, error rates, queue lag, DB pool saturation, token throughput, and cost at load. | Not run; required to support capacity claims. |
| 048 | P0 | Execute a scheduled PostgreSQL backup and verify artifact integrity. | `docker/backup.sh` exists; drill not performed. |
| 049 | P0 | Restore into a clean environment and verify users, tenant data, workflows, documents, and vector search. | `docker/restore.sh` exists; restore drill not performed. |
| 050 | P0 | Publish recovery objectives (RPO/RTO) and confirm backup frequency/retention meets them. | No measured RPO/RTO record found. |
| 051 | P0 | Install production Compose using real TLS certificates, secrets, and domain DNS; verify redirect, renewal, websocket, upload, and health paths. | Config parsed; production stack not launched with real credentials/certificates. |
| 052 | P0 | Install the Kubernetes release with the target ingress and real certificate manager; verify client IP forwarding and TLS renewal. | `kubectl kustomize k8s` rendered; no live cluster validation. |
| 053 | P1 | Upgrade a copy of a previous production database and app image, then prove rollback behavior. | No end-to-end upgrade/rollback run this pass. |
| 054 | P1 | Simulate PostgreSQL, Redis, model-provider, and SMTP outages; confirm bounded retries, recovery, and user-visible errors. | Not exercised against a live stack. |
| 055 | P1 | Verify dashboards, alerts, paging, log retention, audit search, and SLO thresholds with real traffic. | Telemetry configuration exists; operational alert path not validated. |

## D. Carry-forward security, product, and UX findings to revalidate

These are concrete findings from the 2026-10-15 audit. They are listed separately because subsequent work in this tree may have closed some of them. Confirm each against the current UI/API before implementation.

| ID | Priority | Gap to recheck | Prior evidence / status |
|---|---|---|---|
| 056 | P2 | The placeholder `Action` step only logs; keep it clearly marked in the editor or replace it with executable steps. Publishing already blocks workflows containing it. | `src/R2WAI.Api/Workflows/NodeProviders/ActionNodeProvider.cs`, `src/R2WAI.Api/Controllers/WorkflowsController.cs` — Recheck. |
| 057 | P1 | Event triggers such as form submission, payment, and connected-record changes do not have event sources; only Manual, Webhook, and Schedule are currently accepted. | `src/R2WAI.Api/Controllers/WorkflowsController.cs` — Current code confirms the limited vocabulary; Recheck UI claims. |
| 058 | P1 | Content-moderation settings need enforcement in the chat/request path or removal from settings. | Prior audit: `src/R2WAI.Api/Controllers/AdminController.cs` — Recheck current consumers. |
| 059 | P2 | Teams channel integration is not implemented. | Prior audit: `src/R2WAI.Domain/Enums/ChatbotChannelType.cs` — Recheck adapter registration. |
| 060 | P2 | Slack channel integration is not implemented. | Prior audit: channel enum and adapter inventory — Recheck. |
| 061 | P2 | WhatsApp Cloud integration is not implemented end to end. | Prior audit: `src/R2WAI.Client/src/features/deploy/pages/WhatsAppSetupPage.tsx` — Recheck. |
| 062 | P2 | SMS channel integration is not implemented. | Prior audit: channel enum/adapter inventory — Recheck. |
| 063 | P0 | Published chatbot/widget sessions lack a signed end-user identity token for actions on behalf of the website user. | Prior audit: widget authentication flow — Recheck current code. |
| 064 | P2 | There is no live-human handoff queue/action for an agent that cannot resolve a conversation. | Prior audit: channel and agent capabilities — Recheck. |
| 065 | P3 | Navigation CRUD exists without a client caller while product navigation is static; remove the dead surface or wire it to tenant navigation. | Prior audit: `src/R2WAI.Api/Controllers/NavigationController.cs` and client `roleNav.ts` — Recheck. |
| 066 | P2 | Access-request endpoints have no clear requester entry point or admin review queue. | Prior audit: `src/R2WAI.Api/Controllers/AuthController.cs` — Recheck. |
| 067 | P2 | Automation list/detail UI needs a discoverable unpublish action. | Prior audit: workflow lifecycle API/UI map — Recheck. |
| 068 | P2 | Automation archive and restore actions need UI and a clear retention policy. | Prior audit: workflow lifecycle API/UI map — Recheck. |
| 069 | P2 | Automation version history and rollback need to be usable from the UI. | Prior audit: workflow version API/UI map — Recheck. |
| 070 | P2 | Automation bulk-delete needs safeguards, preview, and a useful result summary. | Prior audit: workflow bulk API/UI map — Recheck. |
| 071 | P1 | Knowledge-base owners need a “test search/ask” panel to validate retrieval before publishing. | Prior audit: `knowledgebases/{id}/search` API/UI map — Recheck. |
| 072 | P2 | Knowledge version history and rollback need a usable UI. | Prior audit: knowledge API/UI map — Recheck. |
| 073 | P2 | Knowledge-source inventory and per-source state need to be visible. | Prior audit: knowledge API/UI map — Recheck. |
| 074 | P2 | Tenant prompt templates need a managed UI with preview and impact warning. | Prior audit: assistant prompt-template endpoints — Recheck. |
| 075 | P2 | Assistant prompt history needs a review/restore UI. | Prior audit: assistant prompt-history endpoint — Recheck. |
| 076 | P0 | Complete route-by-route API authorization review. Fixed cross-user access gaps in pending approvals, automation runs, operations workflow history/analytics/reports, and chat conversations: authenticated users could read pending request data outside their role/policy, list other users' runs, inspect tenant-wide workflow execution records and business usage/cost reports, read another user's conversation/messages, append messages to another user's conversation, or reuse a conversation across assistants. Pending approvals now use DB role membership and active workflow policies; configured approvers can reach Confirmations; policy administration/history, workflow execution history, analytics/reports, and run details are AdminOnly; routed roles are enforced during decisions; chat conversations are caller-scoped, and chat idempotency keys are tenant/user-scoped. Finish the matrix across all other routes. | `src/R2WAI.Api/Controllers/ApprovalsController.cs`, `src/R2WAI.Api/Controllers/AssistantsController.cs`, `src/R2WAI.Api/Controllers/OperationsController.cs`, `src/R2WAI.Api/Controllers/RunsController.cs`, `src/R2WAI.Application/Features/Operations/Queries/GetMetricsQuery.cs`, `src/R2WAI.Application/Features/Workflows/Queries/GetWorkflowInstancesQuery.cs`, `src/R2WAI.Application/Features/Chat`, `src/R2WAI.Application/Features/Assistants/Commands/ChatWithAssistantCommand.cs`, `src/R2WAI.Application/Features/Assistants/Commands/ChatWithPublishedAssistantCommand.cs`, `src/R2WAI.Infrastructure/Services/ApprovalService.cs`, `src/R2WAI.Client/src/lib/nav/roleNav.ts`, `src/R2WAI.Client/src/features/approvals/pages/ApprovalsPage.tsx`, `src/R2WAI.Client/src/features/runs/api.ts`, `src/R2WAI.Client/src/features/runs/components/RunInspectorDrawer.tsx` — Targeted gaps fixed in source; full route matrix remains open. |
| 077 | P1 | Approval policy CRUD, roles, escalation, and due-time configuration need an admin UI. | Prior audit: `approvals/policies` API/UI map — Recheck current screens. |
| 078 | P2 | Document summarization needs a user-facing action and result state. | Prior audit: documents API/UI map — Recheck. |
| 079 | P2 | Document extraction and ask operations need a user-facing flow. | Prior audit: documents API/UI map — Recheck. |
| 080 | P2 | Document reprocessing and download need clear progress, errors, and authorization affordances. | Prior audit: documents API/UI map — Recheck. |
| 081 | P2 | Admin bulk operations, model-key rotation/removal, and usage analytics need complete UI workflows. | Prior audit: admin API/UI map — Recheck. |
| 082 | P2 | User invitation and initial role assignment should be one guided onboarding flow. | Prior audit: `admin/users/invite` API/UI map — Recheck. |
| 083 | P1 | API field-validation errors should appear on the relevant form controls instead of only a toast. | Prior audit: `apiErrorMessage` and dialog review — Recheck. |
| 084 | P2 | Query error, empty, and loading states need a page-by-page review so failed data loads do not look like empty data. | Prior audit: Users, Applications, Automations, Chatbots, Home, Tools and lookup dialogs — Recheck. |
| 085 | P2 | Notifications need broader event coverage and per-user preferences; failures should not be silent. | Prior audit: notification event map — Recheck current event handlers. |

## E. Larger product-capability gaps

These are roadmap choices for a mature enterprise product, not defects that can be safely closed by a small code patch.

| ID | Priority | Capability gap | Evidence / status |
|---|---|---|---|
| 086 | P2 | No topic/dialog designer for deterministic conversational flows. | Prior product comparison in [competitive analysis](COMPETITIVE-GAP-ANALYSIS-2026-10-15.md). |
| 087 | P1 | Agent tool/knowledge plan and reasoning trace are not presented as an auditable run plan. | Prior product comparison; verify current Runs UI. |
| 088 | P2 | Multi-agent delegation lacks parallel fan-out and aggregation. | Prior product comparison; verify current runtime behavior. |
| 089 | P2 | Workflow canvas lacks common loop/for-each, parallel branch, variable, and call-agent building blocks. | Prior workflow inventory; roadmap gap. |
| 090 | P1 | OAuth2 client-credentials flows and automatic token refresh are missing from integration credentials. | Prior audit: `DynamicToolExecutor.cs`; verify auth schemes before implementation. |
| 091 | P1 | Integration secrets cannot reference an external secret manager/vault with rotation policy. | Prior audit: credential configuration; roadmap/infrastructure gap. |
| 092 | P2 | Knowledge sources do not have scheduled recrawl and freshness controls. | Prior product comparison; verify source service. |
| 093 | P2 | Website knowledge ingestion lacks site crawling, sitemap scope, and crawl budget controls. | Prior product comparison; verify current URL ingestion. |
| 094 | P2 | Connector catalog depth is small for enterprise on-prem deployments; prioritize systems from actual customer demand. | Prior audit catalog count; confirm current catalog before prioritizing. |
| 095 | P1 | Connector credentials lack a complete expiry, rotation, and reauthorization workflow. | Prior audit: static-token model; roadmap gap. |
| 096 | P2 | Voice output/TTS is not available in the chatbot experience. | Prior product comparison; verify current client speech support. |
| 097 | P2 | Conversation analytics lack resolution/escalation funnels, CSAT, and unanswered-question clustering. | Prior product comparison; verify current analytics pages. |
| 098 | P1 | Test cases do not provide LLM-scored quality gates before publishing a new agent version. | Prior product comparison; roadmap gap. |
| 099 | P1 | Agent/workflow export-import and environment promotion (dev → test → production) are not available. | Prior product comparison; roadmap gap. |
| 100 | P2 | UI localization and multilingual admin workflows are not available. | Prior audit found English-only UI; verify current localization packages. |

## Verification performed for this pass

- `dotnet build R2WAI.slnx --no-restore -clp:ErrorsOnly` — succeeded; 0 errors, 241 warnings. This compiles test projects but does not execute tests.
- `dotnet list R2WAI.slnx package --vulnerable --include-transitive --no-restore` — no vulnerable packages reported after package upgrades.
- `npm run build` in `src/R2WAI.Client` — succeeded. Vite still reports a large initial bundle (about 854 KB minified) and a large monitor/data-table chunk; see gap 044/performance follow-up.
- Production Compose config validation and `kubectl kustomize k8s` rendering succeeded earlier in this pass. A production stack, real TLS certificates, customer secrets, and a Kubernetes cluster were not available for live deployment validation.
- EF generated the approval-policy, webhook-idempotency, and webhook-slug migration SQL. Duplicate policy/slug preflights stop rather than silently changing data. The migrations were not applied to a live database.
- EF reports no pending model changes after those migrations were generated.
- The current authorization follow-up adds caller ownership checks to conversation reads, sends, assistant continuation, and streaming; chat idempotency is tenant/user-scoped. Conversation history is paginated in SQL, and anonymous chatbot chat inputs are capped before model work.
- `git diff --check` completed without whitespace errors after the follow-up edits.
- No test suite or load test was run in this pass.

## Recommended order

1. Complete a route-by-route authorization matrix for every write/read endpoint. Apply G031, G032, and G045 migrations after reviewing active policy and webhook-slug conflicts.
2. Fix G033–G042 before increasing Kubernetes replicas or promising high availability.
3. Perform G046–G055 with a representative customer deployment, starting with backup/restore and a measured load profile.
4. Revalidate G056–G085 against the current UI before planning implementation; the Oct 15 audit is a baseline, not a claim that every item is still present.
5. Prioritize G086–G100 from customer interviews and contractual requirements rather than chasing feature-count parity.
