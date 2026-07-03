using FluentValidation;

namespace R2WAI.Application.Features.Leads.Commands;

public record UpdateLeadStatusCommand : IRequest<LeadDto>
{
    public Guid Id { get; init; }
    public LeadStatus Status { get; init; }
    public string? Notes { get; init; }
}

public class UpdateLeadStatusCommandValidator : AbstractValidator<UpdateLeadStatusCommand>
{
    public UpdateLeadStatusCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class UpdateLeadStatusCommandHandler(
    IRepository<Lead> leadRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateLeadStatusCommand, LeadDto>
{
    public async Task<LeadDto> Handle(UpdateLeadStatusCommand command, CancellationToken cancellationToken)
    {
        var lead = await leadRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Lead), command.Id);

        lead.UpdateStatus(command.Status);
        if (command.Notes is not null)
            lead.SetNotes(command.Notes);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<LeadDto>(lead);
    }
}
