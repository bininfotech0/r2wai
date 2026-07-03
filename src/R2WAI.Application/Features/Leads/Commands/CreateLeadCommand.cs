using FluentValidation;

namespace R2WAI.Application.Features.Leads.Commands;

public record CreateLeadCommand : IRequest<LeadDto>
{
    public Guid? ChatbotId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? ClassOrGrade { get; init; }
    public string? Interest { get; init; }
    public string? Source { get; init; }
}

public class CreateLeadCommandValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Phone).MaximumLength(30);
        RuleFor(v => v.Email).MaximumLength(320).EmailAddress().When(v => !string.IsNullOrWhiteSpace(v.Email));
    }
}

public class CreateLeadCommandHandler(
    IRepository<Lead> leadRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateLeadCommand, LeadDto>
{
    public async Task<LeadDto> Handle(CreateLeadCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var lead = new Lead(
            Guid.NewGuid(), tenantId, command.Name, command.ChatbotId,
            command.Phone, command.Email, command.ClassOrGrade,
            command.Interest, command.Source);

        await leadRepo.AddAsync(lead, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<LeadDto>(lead);
    }
}
