using FluentValidation;

namespace R2WAI.Application.Features.Applications.Commands;

public record CreateApplicationVersionCommand : IRequest<ApplicationVersionDto>
{
    public Guid ApplicationId { get; init; }
    public string? Note { get; init; }
    public bool Publish { get; init; }
}

public class CreateApplicationVersionCommandValidator : AbstractValidator<CreateApplicationVersionCommand>
{
    public CreateApplicationVersionCommandValidator()
    {
        RuleFor(v => v.ApplicationId).NotEmpty();
        RuleFor(v => v.Note).MaximumLength(1000);
    }
}

public class CreateApplicationVersionCommandHandler(
    IRepository<ApplicationVersion> versionRepo,
    IRepository<ApplicationApi> apiRepo,
    IRepository<ApplicationConfiguration> configRepo,
    IRepository<ConnectedApplication> applicationRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateApplicationVersionCommand, ApplicationVersionDto>
{
    public async Task<ApplicationVersionDto> Handle(CreateApplicationVersionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var application = await applicationRepo.GetByIdAsync(command.ApplicationId, cancellationToken)
            ?? throw new NotFoundException(nameof(ConnectedApplication), command.ApplicationId);

        var snapshotJson = await ApplicationVersionSnapshotService.BuildSnapshotJsonAsync(
            application, apiRepo, configRepo, cancellationToken);

        var existingVersions = await versionRepo.FindAsync(v => v.ApplicationId == application.Id, cancellationToken);
        var nextVersionNumber = existingVersions.Count == 0 ? 1 : existingVersions.Max(v => v.VersionNumber) + 1;

        var version = ApplicationVersion.CreateSnapshot(
            Guid.NewGuid(), tenantId, application.Id, nextVersionNumber, snapshotJson, command.Note);

        if (command.Publish)
        {
            var userId = currentUser.UserId ?? throw new UnauthorizedException();

            foreach (var published in existingVersions.Where(v => v.IsPublished))
                published.Unpublish();

            version.Publish(userId);
        }

        await versionRepo.AddAsync(version, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationVersionDto>(version);
    }
}
