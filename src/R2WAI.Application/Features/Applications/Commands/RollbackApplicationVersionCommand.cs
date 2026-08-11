using FluentValidation;

namespace R2WAI.Application.Features.Applications.Commands;

public record RollbackApplicationVersionCommand : IRequest<ApplicationVersionDto>
{
    public Guid VersionId { get; init; }
}

public class RollbackApplicationVersionCommandValidator : AbstractValidator<RollbackApplicationVersionCommand>
{
    public RollbackApplicationVersionCommandValidator()
    {
        RuleFor(v => v.VersionId).NotEmpty();
    }
}

public class RollbackApplicationVersionCommandHandler(
    IRepository<ApplicationVersion> versionRepo,
    IRepository<ApplicationApi> apiRepo,
    IRepository<ApplicationConfiguration> configRepo,
    IRepository<ConnectedApplication> applicationRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RollbackApplicationVersionCommand, ApplicationVersionDto>
{
    public async Task<ApplicationVersionDto> Handle(RollbackApplicationVersionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var targetVersion = await versionRepo.GetByIdAsync(command.VersionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationVersion), command.VersionId);

        var application = await applicationRepo.GetByIdAsync(targetVersion.ApplicationId, cancellationToken)
            ?? throw new NotFoundException(nameof(ConnectedApplication), targetVersion.ApplicationId);

        var snapshot = ApplicationVersionSnapshotService.Deserialize(targetVersion.ConfigSnapshot);

        var environment = Enum.TryParse<ApplicationEnvironment>(snapshot.Environment, out var parsedEnvironment)
            ? parsedEnvironment
            : ApplicationEnvironment.Development;

        application.UpdateDetails(snapshot.Name, snapshot.Description, snapshot.BaseUrl, environment);

        var existingApis = await apiRepo.FindAsync(a => a.ApplicationId == application.Id, cancellationToken);
        foreach (var existingApi in existingApis)
            apiRepo.Delete(existingApi);

        foreach (var apiSnapshot in snapshot.Apis)
        {
            var authScheme = Enum.TryParse<ApiAuthScheme>(apiSnapshot.AuthScheme, out var parsedScheme)
                ? parsedScheme
                : ApiAuthScheme.None;

            var restoredApi = new ApplicationApi(
                Guid.NewGuid(), tenantId, application.Id, apiSnapshot.Name, apiSnapshot.BaseUrl,
                authScheme, apiSnapshot.CredentialRef, apiSnapshot.OpenApiSource);

            await apiRepo.AddAsync(restoredApi, cancellationToken);
        }

        if (snapshot.Configuration is not null)
        {
            var configuration = await configRepo.FirstOrDefaultAsync(c => c.ApplicationId == application.Id, cancellationToken);
            if (configuration is null)
            {
                configuration = new ApplicationConfiguration(Guid.NewGuid(), tenantId, application.Id);
                await configRepo.AddAsync(configuration, cancellationToken);
            }

            configuration.UpdateSettings(
                snapshot.Configuration.TimeoutSeconds, snapshot.Configuration.MaxRetries,
                snapshot.Configuration.RagThreshold, snapshot.Configuration.ModelId,
                snapshot.Configuration.SystemPromptTemplate);
        }

        var allVersions = await versionRepo.FindAsync(v => v.ApplicationId == application.Id, cancellationToken);
        var nextVersionNumber = allVersions.Max(v => v.VersionNumber) + 1;

        foreach (var published in allVersions.Where(v => v.IsPublished))
            published.Unpublish();

        // The restored version's snapshot is exactly the target's snapshot (that's what "restore" means) —
        // rebuilding it here would re-query the DB before SaveChanges and miss the just-added/removed entities.
        var newVersion = ApplicationVersion.CreateSnapshot(
            Guid.NewGuid(), tenantId, application.Id, nextVersionNumber, targetVersion.ConfigSnapshot,
            $"Rolled back to v{targetVersion.VersionNumber}");
        newVersion.Publish(userId);

        await versionRepo.AddAsync(newVersion, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationVersionDto>(newVersion);
    }
}
