namespace R2WAI.Application.Features.Integrations.DTOs;

// docs/api/MISSING-BACKEND-ENDPOINTS.md §3.5 #72. Curated *suggestions*, not live connections —
// "Connect" on the client pre-fills the same real CreateEditIntegrationDialog every hand-built
// integration already uses (name/endpoint/auth type), nothing here claims a working connection
// exists until the admin actually enters credentials and the existing Test action succeeds.
// SuggestedType is always Http: DynamicToolFunctionFactory only ever makes ToolType.Http rows
// callable (Email/Database/Script/Custom exist as enum values but have no execution path —
// IntegrationConnectorTests proves they 422 "not executable yet"), so cataloguing a non-Http type
// would suggest a dead end.
public class IntegrationCatalogEntryDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SuggestedType { get; init; } = "Http";
    public string SuggestedAuthType { get; init; } = "None";
    public string? SuggestedEndpointUrl { get; init; }
}
