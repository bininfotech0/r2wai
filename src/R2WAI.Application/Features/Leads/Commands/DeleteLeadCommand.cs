using FluentValidation;

namespace R2WAI.Application.Features.Leads.Commands;

public record DeleteLeadCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteLeadCommandValidator : AbstractValidator<DeleteLeadCommand>
{
    public DeleteLeadCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteLeadCommandHandler(
    IRepository<Lead> leadRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteLeadCommand, Unit>
{
    public async Task<Unit> Handle(DeleteLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await leadRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Lead), command.Id);

        lead.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
