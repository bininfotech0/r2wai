using FluentValidation;
using R2WAI.Application.Common.AI;
using R2WAI.Application.Features.Applications.DTOs;

namespace R2WAI.Application.Features.Applications.Commands;

/// <summary>
/// Track B Phase 7 (Application Discovery Engine) — scoped starting point: manually-triggered,
/// single-application, OpenAPI-only discovery. Given a spec (URL or raw content), parses it (reusing
/// IOpenApiImportService, the same parser Integrations' standalone OpenAPI import already uses),
/// creates/enriches the application's ApplicationApi, and creates one governed capability
/// (ToolDefinition) per discovered operation — the admin reviews these before Publish, same as any
/// other capability. Deliberately out of scope for this pass: unattended/scheduled discovery,
/// multi-application scanning, and endpoint/navigation/tool-suggestion beyond raw OpenAPI parsing —
/// see docs/implementation/PLATFORM-IMPLEMENTATION-PLAN.md §8 for the fuller original vision.
/// </summary>
public record DiscoverApplicationCommand : IRequest<ApplicationDiscoveryResultDto>
{
    public Guid ApplicationId { get; init; }
    public string? OpenApiUrl { get; init; }
    public string? OpenApiFileContent { get; init; }
}

public class DiscoverApplicationCommandValidator : AbstractValidator<DiscoverApplicationCommand>
{
    // Same ceiling OpenApiImportService applies to a fetched spec.
    private const int MaxSpecChars = 5 * 1024 * 1024;

    public DiscoverApplicationCommandValidator()
    {
        RuleFor(v => v.ApplicationId).NotEmpty();
        RuleFor(v => v)
            .Must(v => !string.IsNullOrWhiteSpace(v.OpenApiUrl) || !string.IsNullOrWhiteSpace(v.OpenApiFileContent))
            .WithMessage("Either an OpenAPI URL or file content must be provided.");
        RuleFor(v => v.OpenApiFileContent).MaximumLength(MaxSpecChars)
            .WithMessage("Spec file is too large (max 5 MB).");
    }
}

/// <summary>
/// Re-runnable: each run reconciles the spec against what earlier runs created instead of only
/// appending. Operations are matched to existing capabilities by route (method + path) first, then
/// by name, so a re-sync is idempotent even for capabilities that were renamed for uniqueness.
/// Nothing is ever deleted — operations that vanished from the spec are reported as stale for the
/// admin to retire, since an active capability may be in use by a live assistant.
/// </summary>
public class DiscoverApplicationCommandHandler(
    IRepository<ConnectedApplication> applicationRepo,
    IRepository<ApplicationApi> applicationApiRepo,
    IRepository<ToolDefinition> capabilityRepo,
    IOpenApiImportService openApiImportService,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    ILogger<DiscoverApplicationCommandHandler> logger) : IRequestHandler<DiscoverApplicationCommand, ApplicationDiscoveryResultDto>
{
    private const int MaxStaleNamesReported = 50;

    public async Task<ApplicationDiscoveryResultDto> Handle(DiscoverApplicationCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var application = await applicationRepo.GetByIdAsync(command.ApplicationId, cancellationToken)
            ?? throw new NotFoundException(nameof(ConnectedApplication), command.ApplicationId);

        // Re-syncing a live system must not take it back through the onboarding lifecycle: a
        // Published (or Disabled) system keeps its status, and new capabilities still land inactive.
        var isLive = application.Status is ApplicationStatus.Published or ApplicationStatus.Disabled;
        if (!isLive)
            application.StartDiscovery();

        var analysis = await openApiImportService.AnalyzeAsync(command.OpenApiUrl, command.OpenApiFileContent, cancellationToken);

        var existingApis = await applicationApiRepo.FindAsync(a => a.ApplicationId == application.Id, cancellationToken);
        var applicationApi = existingApis.FirstOrDefault();

        // A spec without a "servers" entry yields no base URL — keep what is already configured
        // rather than blanking it, which would make every linked capability undispatchable.
        var baseUrl = !string.IsNullOrWhiteSpace(analysis.BaseUrl)
            ? analysis.BaseUrl
            : applicationApi?.BaseUrl ?? application.BaseUrl ?? string.Empty;

        if (applicationApi is null)
        {
            applicationApi = new ApplicationApi(
                Guid.NewGuid(), tenantId, application.Id, $"{application.Name} API", baseUrl,
                openApiSource: command.OpenApiUrl);
            await applicationApiRepo.AddAsync(applicationApi, cancellationToken);
        }
        else
        {
            applicationApi.UpdateDetails(applicationApi.Name, baseUrl, applicationApi.AuthScheme,
                applicationApi.CredentialRef, command.OpenApiUrl ?? applicationApi.OpenApiSource);
        }

        // Every capability in the tenant, not just this API's: the runtime exposes them all under
        // sanitized function names and drops the second of any two that collide, so a discovered
        // "getUser" next to another system's "getUser" would otherwise be silently uncallable.
        var tenantCapabilities = await capabilityRepo.FindAsync(t => t.TenantId == tenantId, cancellationToken);
        var takenFunctionNames = tenantCapabilities
            .Select(c => ToolFunctionName.Sanitize(c.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var own = tenantCapabilities.Where(c => c.ApplicationApiId == applicationApi.Id).ToList();
        var ownByRoute = own
            .Where(c => !string.IsNullOrWhiteSpace(c.HttpMethod) && !string.IsNullOrWhiteSpace(c.EndpointPath))
            .GroupBy(c => RouteKey(c.HttpMethod!, c.EndpointPath!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var ownByName = own
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var specRoutes = analysis.Operations
            .Select(op => RouteKey(op.Method, op.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A capability still matched by its own route is spoken for before the loop starts, so a
        // same-named operation elsewhere in the spec (duplicate operationIds are common) can never
        // "move" it — whatever order the operations arrive in.
        var claimed = ownByRoute
            .Where(kv => specRoutes.Contains(kv.Key))
            .Select(kv => kv.Value.Id)
            .ToHashSet();
        int created = 0, unchanged = 0, relinked = 0, needsReview = 0, renamed = 0;

        foreach (var op in analysis.Operations)
        {
            var route = RouteKey(op.Method, op.Path);

            if (ownByRoute.ContainsKey(route))
            {
                unchanged++;
                continue;
            }

            // Same operation name, different route: the endpoint moved. An inactive draft is safe to
            // re-point; an active capability may be serving a live assistant, so it is left alone
            // and flagged for the admin instead of being silently redirected.
            if (ownByName.TryGetValue(op.SuggestedName, out var moved) && claimed.Add(moved.Id))
            {
                if (moved.IsActive)
                {
                    needsReview++;
                }
                else
                {
                    if (moved.HttpMethod is not null && moved.EndpointPath is not null)
                        ownByRoute.Remove(RouteKey(moved.HttpMethod, moved.EndpointPath));
                    moved.LinkApi(applicationApi.Id, op.Method, op.Path);
                    ownByRoute[route] = moved;
                    relinked++;
                }
                continue;
            }

            var name = CapabilityNames.MakeUnique(op.SuggestedName, application.Code, takenFunctionNames);
            if (!string.Equals(name, op.SuggestedName, StringComparison.Ordinal))
                renamed++;

            var capability = new ToolDefinition(Guid.NewGuid(), tenantId, name, ToolType.Http, op.Summary);
            capability.AssignApplication(application.Id);
            capability.LinkApi(applicationApi.Id, op.Method, op.Path);

            // A discovered operation is a draft: risk is derived from the method rather than left at the
            // entity default ("Low", no approval), and it stays INACTIVE — not callable by any assistant —
            // until an admin has reviewed and activated it.
            var governance = ToolDefinition.DefaultGovernanceForHttpMethod(op.Method);
            capability.ConfigureGovernance(governance.RiskLevel, requiredRole: null,
                governance.ConfirmationRequired, governance.ApprovalRequired, auditRequired: true);
            capability.Deactivate();

            await capabilityRepo.AddAsync(capability, cancellationToken);

            ownByRoute[route] = capability;
            created++;
        }

        var stale = own
            .Where(c => c.HttpMethod is not null && c.EndpointPath is not null
                        && !specRoutes.Contains(RouteKey(c.HttpMethod, c.EndpointPath)))
            .Select(c => c.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!isLive)
            application.MarkConfiguring();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Discovered application {ApplicationId}: {OperationCount} operations, {Created} created ({Renamed} renamed for uniqueness), " +
            "{Unchanged} unchanged, {Relinked} re-linked, {NeedsReview} need review, {Stale} stale",
            application.Id, analysis.Operations.Count, created, renamed, unchanged, relinked, needsReview, stale.Count);

        return new ApplicationDiscoveryResultDto
        {
            ApplicationApiId = applicationApi.Id,
            BaseUrl = baseUrl,
            OperationsDiscovered = analysis.Operations.Count,
            CapabilitiesCreated = created,
            CapabilitiesSkippedAsExisting = unchanged,
            CapabilitiesRelinked = relinked,
            CapabilitiesNeedingReview = needsReview,
            CapabilitiesRenamedForUniqueness = renamed,
            StaleCapabilityCount = stale.Count,
            StaleCapabilityNames = stale.Take(MaxStaleNamesReported).ToList()
        };
    }

    private static string RouteKey(string method, string path) => $"{method.ToUpperInvariant()} {path}";
}
