using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record AwardEventPointsCommand : IRequest<PointsTransactionDto>, IAuthorizedRequest
{
    public Guid EventId { get; init; }
    public Guid UserId { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class AwardEventPointsCommandValidator : AbstractValidator<AwardEventPointsCommand>
{
    public AwardEventPointsCommandValidator()
    {
        RuleFor(v => v.EventId).NotEmpty();
        RuleFor(v => v.UserId).NotEmpty();
    }
}

public class AwardEventPointsCommandHandler(
    IRepository<MemberEvent> eventRepo,
    IRepository<EventAttendance> attendanceRepo,
    IRepository<MemberWallet> walletRepo,
    IRepository<PointsTransaction> transactionRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<AwardEventPointsCommand, PointsTransactionDto>
{
    public async Task<PointsTransactionDto> Handle(AwardEventPointsCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var awardedByUserId = currentUser.UserId ?? throw new UnauthorizedException();

        var memberEvent = await eventRepo.GetByIdAsync(command.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberEvent), command.EventId);

        if (!memberEvent.IsActive)
            throw new ValidationException(nameof(command.EventId), "This event is not active.");

        var alreadyAwarded = await attendanceRepo.FirstOrDefaultAsync(
            a => a.EventId == command.EventId && a.UserId == command.UserId, cancellationToken);
        if (alreadyAwarded is not null)
            throw new ValidationException(nameof(command.UserId), "This member has already been awarded points for this event.");

        var wallet = await walletRepo.FirstOrDefaultAsync(w => w.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberWallet), command.UserId);

        // PointsAwarded snapshots the event's current value so a later edit to MemberEvent.PointsValue
        // doesn't rewrite history for members already credited.
        var attendance = new EventAttendance(Guid.NewGuid(), tenantId, command.EventId, command.UserId,
            memberEvent.PointsValue, awardedByUserId);
        await attendanceRepo.AddAsync(attendance, cancellationToken);

        var transaction = new PointsTransaction(Guid.NewGuid(), tenantId, command.UserId, memberEvent.PointsValue,
            PointsTransactionReason.EventAttendance, $"Attended: {memberEvent.Name}", command.EventId);
        await transactionRepo.AddAsync(transaction, cancellationToken);

        wallet.AddPoints(memberEvent.PointsValue);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<PointsTransactionDto>(transaction);
    }
}
