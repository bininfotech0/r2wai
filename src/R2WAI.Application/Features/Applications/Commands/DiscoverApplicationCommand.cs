using FluentValidation;
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
    public DiscoverApplicationCommandValidator()
    {
        RuleFor(v => v.ApplicationId).NotEmpty();
        RuleFor(v => v)
            .Must(v => !string.IsNullOrWhiteSpace(v.OpenApiUrl) || !string.IsNullOrWhiteSpace(v.OpenApiFileContent))
            .WithMessage("Either an OpenAPI URL or file content must be provided.");
    }
}

public class DiscoverApplicationCommandHandler(
    IRepository<ConnectedApplication> applicationRepo,
    IRepository<ApplicationApi> applicationApiRepo,
    IRepository<ToolDefinition> capabilityRepo,
    IOpenApiImportService openApiImportService,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    ILogger<DiscoverApplicationCommandHandler> logger) : IRequestHandler<DiscoverApplicationCommand, ApplicationDiscoveryResultDto>
{
    public async Task<ApplicationDiscoveryResultDto> Handle(DiscoverApplicationCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var application = await applicationRepo.GetByIdAsync(command.ApplicationId, cancellationToken)
            ?? throw new NotFoundException(nameof(ConnectedApplication), command.ApplicationId);

        application.StartDiscovery();

        var analysis = await openApiImportService.AnalyzeAsync(command.OpenApiUrl, command.OpenApiFileContent, cancellationToken);

        var existingApis = await applicationApiRepo.FindAsync(a => a.ApplicationId == application.Id, cancellationToken);
        var applicationApi = existingApis.FirstOrDefault();
        if (applicationApi is null)
        {
            applicationApi = new ApplicationApi(
                Guid.NewGuid(), tenantId, application.Id, $"{application.Name} API", analysis.BaseUrl,
                openApiSource: command.OpenApiUrl);
            await applicationApiRepo.AddAsync(applicationApi, cancellationToken);
        }
        else
        {
            applicationApi.UpdateDetails(applicationApi.Name, analysis.BaseUrl, applicationApi.AuthScheme,
                applicationApi.CredentialRef, command.OpenApiUrl ?? applicationApi.OpenApiSource);
        }

        var existingCapabilities = await capabilityRepo.FindAsync(
            t => t.ApplicationApiId == applicationApi.Id, cancellationToken);
        var existingNames = existingCapabilities.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var skipped = 0;
        foreach (var op in analysis.Operations)
        {
            if (existingNames.Contains(op.SuggestedName))
            {
                skipped++;
                continue;
            }

            var capability = new ToolDefinition(Guid.NewGuid(), tenantId, op.SuggestedName, ToolType.Http, op.Summary);
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
            created++;
        }

        application.MarkConfiguring();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Discovered application {ApplicationId}: {OperationCount} operations found, {Created} capabilities created, {Skipped} already existed",
            application.Id, analysis.Operations.Count, created, skipped);

        return new ApplicationDiscoveryResultDto
        {
            ApplicationApiId = applicationApi.Id,
            BaseUrl = analysis.BaseUrl,
            OperationsDiscovered = analysis.Operations.Count,
            CapabilitiesCreated = created,
            CapabilitiesSkippedAsExisting = skipped
        };
    }
}
