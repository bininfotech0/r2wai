using FluentValidation;

namespace R2WAI.Application.Features.Leads.Commands;

public record UpdateLeadCommand : IRequest<LeadDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? ClassOrGrade { get; init; }
    public string? Interest { get; init; }
    public string? Notes { get; init; }
}

public class UpdateLeadCommandValidator : AbstractValidator<UpdateLeadCommand>
{
    public UpdateLeadCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Phone).MaximumLength(30);
        RuleFor(v => v.Email).MaximumLength(320).EmailAddress().When(v => !string.IsNullOrWhiteSpace(v.Email));
    }
}

public class UpdateLeadCommandHandler(
    IRepository<Lead> leadRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateLeadCommand, LeadDto>
{
    public async Task<LeadDto> Handle(UpdateLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await leadRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Lead), command.Id);

        lead.UpdateDetails(command.Name, command.Phone, command.Email, command.ClassOrGrade, command.Interest);
        if (command.Notes is not null)
            lead.SetNotes(command.Notes);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<LeadDto>(lead);
    }
}
