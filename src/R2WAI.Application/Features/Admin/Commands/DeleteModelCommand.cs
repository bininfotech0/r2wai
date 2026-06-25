using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record DeleteModelCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteModelCommandValidator : AbstractValidator<DeleteModelCommand>
{
    public DeleteModelCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteModelCommandHandler(
    IRepository<ModelConfiguration> modelRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteModelCommand, Unit>
{
    public async Task<Unit> Handle(DeleteModelCommand command, CancellationToken cancellationToken)
    {
        var model = await modelRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ModelConfiguration), command.Id);

        model.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
