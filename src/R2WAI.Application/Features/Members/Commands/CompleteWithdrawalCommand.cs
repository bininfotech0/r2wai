using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record CompleteWithdrawalCommand : IRequest<WithdrawalRequestDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class CompleteWithdrawalCommandValidator : AbstractValidator<CompleteWithdrawalCommand>
{
    public CompleteWithdrawalCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class CompleteWithdrawalCommandHandler(
    IRepository<WithdrawalRequest> requestRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<CompleteWithdrawalCommand, WithdrawalRequestDto>
{
    public async Task<WithdrawalRequestDto> Handle(CompleteWithdrawalCommand command, CancellationToken cancellationToken)
    {
        var request = await requestRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WithdrawalRequest), command.Id);

        // Marks that the admin has confirmed the off-system transfer (bank/UPI) actually happened.
        request.Complete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<WithdrawalRequestDto>(request);
    }
}
