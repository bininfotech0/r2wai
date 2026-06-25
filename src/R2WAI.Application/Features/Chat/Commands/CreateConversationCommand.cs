using FluentValidation;

namespace R2WAI.Application.Features.Chat.Commands;

public record CreateConversationCommand : IRequest<ConversationDto>
{
    public string Title { get; init; } = string.Empty;
    public string? Module { get; init; }
    public Guid? ReferenceId { get; init; }
}

public class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(v => v.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");
    }
}

public class CreateConversationCommandHandler(
    IRepository<Conversation> conversationRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateConversationCommand, ConversationDto>
{
    public async Task<ConversationDto> Handle(CreateConversationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var conversation = new Conversation(
            Guid.NewGuid(), tenantId, userId, command.Title,
            command.Module, command.ReferenceId);

        await conversationRepo.AddAsync(conversation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ConversationDto>(conversation);
    }
}
