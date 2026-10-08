# R2WAI documentation

The current product roadmap and target architecture is [`/ROADMAP.md`](../ROADMAP.md).
AI coding-agent rules and standards live in [`/AI`](../AI/README.md).

| Folder | Contents |
|---|---|
| [product/](product/) | [Vision](product/PRODUCT-VISION.md), [user journeys](product/USER-JOURNEYS.md), [navigation & UX](product/NAVIGATION-AND-UX.md), [feature matrix](product/FEATURE-MATRIX.md) |
| [architecture/](architecture/) | [Overview](architecture/ARCHITECTURE.md), [data model](architecture/DATA-MODEL.md), [AI runtime](architecture/AI-RUNTIME.md), [tool gateway](architecture/TOOL-GATEWAY.md), [execution & workflows](architecture/EXECUTION-AND-WORKFLOWS.md), [integrations](architecture/INTEGRATIONS.md), [workspace migration plan](architecture/WORKSPACE-MIGRATION-PLAN.md) |
| [adr/](adr/) | Architecture Decision Records (`NNNN-title.md`) |
| [api/](api/) | [API reference](api/API.md), [missing backend endpoints](api/MISSING-BACKEND-ENDPOINTS.md) |
| [development/](development/) | [Local development guide](development/DEVELOPMENT.md) |
| [deployment/](deployment/) | [Deployment guide](deployment/DEPLOYMENT.md), [production runbook](deployment/RUNBOOK.md) |
| [security/](security/) | [Threat model](security/THREAT-MODEL.md), dated security reviews |
| [implementation/](implementation/) | Implementation plans ([platform](implementation/PLATFORM-IMPLEMENTATION-PLAN.md), legacy [MVP](implementation/MVP-IMPLEMENTATION-PLAN.md)) |
| [audit/](audit/) | Dated audits (`*-YYYY-MM-DD.md`), [technical feature matrix](audit/FEATURE-MATRIX.md), [repository inventory](audit/REPOSITORY-INVENTORY.md) |
| [release/](release/) | [Release readiness report](release/RELEASE-READINESS-REPORT.md) |
| [samples/](samples/) | [widget-embed.html](samples/widget-embed.html): host page that embeds the chat widget (local dev values) |
| [archive/](archive/) | Superseded docs kept for history; don't treat them as current |

## Conventions

- Dated snapshots (audits, reviews) carry the date in the file name and are not edited after the fact, apart from link fixes.
- New architectural decisions go in `adr/` as the next number.
- When a doc is superseded, move it to `archive/` and update links to it.
