using FluentValidation;

namespace R2WAI.Application.Features.Governance.Commands;

public record UpsertGlobalPolicyCommand : IRequest<GlobalPolicyDto>
{
    public string Type { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Content { get; init; }
    public bool IsActive { get; init; }
}

public class UpsertGlobalPolicyCommandValidator : AbstractValidator<UpsertGlobalPolicyCommand>
{
    public UpsertGlobalPolicyCommandValidator()
    {
        RuleFor(v => v.Type).NotEmpty().MaximumLength(50);
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpsertGlobalPolicyCommandHandler(
    IRepository<GlobalPolicy> policyRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<UpsertGlobalPolicyCommand, GlobalPolicyDto>
{
    public async Task<GlobalPolicyDto> Handle(UpsertGlobalPolicyCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var existing = await policyRepo.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Type == command.Type, cancellationToken);

        if (existing is not null)
        {
            existing.Update(command.Name, command.Content, command.IsActive);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return mapper.Map<GlobalPolicyDto>(existing);
        }

        var policy = new GlobalPolicy(Guid.NewGuid(), tenantId, command.Type, command.Name, command.Content, command.IsActive);
        await policyRepo.AddAsync(policy, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<GlobalPolicyDto>(policy);
    }
}
