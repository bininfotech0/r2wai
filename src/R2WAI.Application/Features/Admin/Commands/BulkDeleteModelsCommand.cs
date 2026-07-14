using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record BulkDeleteModelsCommand : IRequest<Unit>, IAuthorizedRequest
{
    public Guid[] Ids { get; init; } = [];
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class BulkDeleteModelsCommandValidator : AbstractValidator<BulkDeleteModelsCommand>
{
    public BulkDeleteModelsCommandValidator()
    {
        RuleFor(v => v.Ids).NotEmpty();
    }
}

public class BulkDeleteModelsCommandHandler(
    IRepository<ModelConfiguration> modelRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<BulkDeleteModelsCommand, Unit>
{
    public async Task<Unit> Handle(BulkDeleteModelsCommand command, CancellationToken cancellationToken)
    {
        foreach (var id in command.Ids)
        {
            var model = await modelRepo.GetByIdAsync(id, cancellationToken);
            model?.Deactivate();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
