using R2WAI.Application.Features.Integrations.DTOs;

namespace R2WAI.Application.Features.Integrations.Queries;

public record GetIntegrationCatalogQuery : IRequest<List<IntegrationCatalogEntryDto>>;

// Static/hardcoded, same convention this codebase already accepts elsewhere for curated,
// rarely-changing reference data (docs/api/MISSING-BACKEND-ENDPOINTS.md §3.7 #82 notes the 5
// workflow templates are hardcoded in WorkflowsController the same way). A real admin-editable
// catalog (add/remove entries without a deploy) is the natural next step if this list needs to
// grow beyond a small curated set, not built here since nothing today asks for that.
public class GetIntegrationCatalogQueryHandler : IRequestHandler<GetIntegrationCatalogQuery, List<IntegrationCatalogEntryDto>>
{
    private static readonly List<IntegrationCatalogEntryDto> Catalog =
    [
        new() { Id = "slack", Name = "Slack", Category = "Messaging", SuggestedAuthType = "Bearer",
            SuggestedEndpointUrl = "https://slack.com/api",
            Description = "Post messages and read channel activity via Slack's Web API." },
        new() { Id = "teams", Name = "Microsoft Teams", Category = "Messaging", SuggestedAuthType = "Bearer",
            SuggestedEndpointUrl = "https://graph.microsoft.com/v1.0",
            Description = "Reach Teams channels and chats through Microsoft Graph." },
        new() { Id = "google-sheets", Name = "Google Sheets", Category = "Productivity", SuggestedAuthType = "Bearer",
            SuggestedEndpointUrl = "https://sheets.googleapis.com/v4",
            Description = "Read and write spreadsheet data an assistant can reference or update." },
        new() { Id = "notion", Name = "Notion", Category = "Productivity", SuggestedAuthType = "Bearer",
            SuggestedEndpointUrl = "https://api.notion.com/v1",
            Description = "Query and update Notion pages and databases." },
        new() { Id = "hubspot", Name = "HubSpot", Category = "CRM", SuggestedAuthType = "Bearer",
            SuggestedEndpointUrl = "https://api.hubapi.com",
            Description = "Look up and update contacts, deals and tickets." },
        new() { Id = "salesforce", Name = "Salesforce", Category = "CRM", SuggestedAuthType = "Bearer",
            SuggestedEndpointUrl = null,
            Description = "Connect a Salesforce org (enter your org's own instance URL)." },
        new() { Id = "github", Name = "GitHub", Category = "Developer Tools", SuggestedAuthType = "Bearer",
            SuggestedEndpointUrl = "https://api.github.com",
            Description = "Read issues, pull requests and repository data." },
        new() { Id = "generic-webhook", Name = "Generic Webhook", Category = "REST APIs", SuggestedAuthType = "None",
            SuggestedEndpointUrl = null,
            Description = "Call any outbound webhook URL — no specific provider required." },
        new() { Id = "generic-rest", Name = "Custom REST API", Category = "REST APIs", SuggestedAuthType = "ApiKey",
            SuggestedEndpointUrl = null,
            Description = "Connect any REST API this tenant already has credentials for." },
    ];

    public Task<List<IntegrationCatalogEntryDto>> Handle(GetIntegrationCatalogQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Catalog);
}
