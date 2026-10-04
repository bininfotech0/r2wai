using FluentValidation;
using R2WAI.Application.Features.BusinessCapabilities.DTOs;

namespace R2WAI.Application.Features.BusinessCapabilities.Commands;

public record SetBusinessCapabilityStatusCommand : IRequest<BusinessCapabilityDto>
{
    public Guid Id { get; init; }
    public string Status { get; init; } = "Draft";
}

public class SetBusinessCapabilityStatusCommandValidator : AbstractValidator<SetBusinessCapabilityStatusCommand>
{
    public SetBusinessCapabilityStatusCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Status).Must(s => Enum.TryParse<BusinessCapabilityStatus>(s, true, out _))
            .WithMessage("Status must be one of: Draft, Active, Disabled.");
    }
}

public class SetBusinessCapabilityStatusCommandHandler(
    IRepository<BusinessCapability> capabilityRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<SetBusinessCapabilityStatusCommand, BusinessCapabilityDto>
{
    public async Task<BusinessCapabilityDto> Handle(SetBusinessCapabilityStatusCommand command, CancellationToken cancellationToken)
    {
        var capability = await capabilityRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessCapability), command.Id);

        capability.SetStatus(Enum.Parse<BusinessCapabilityStatus>(command.Status, true));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<BusinessCapabilityDto>(capability);
    }
}
