using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record CreateUserCommand : IRequest<UserDto>
{
    public string ExternalId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string? Password { get; init; }
}

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(v => v.ExternalId).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(v => v.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.LastName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Password).NotEmpty().MinimumLength(6).When(v => !string.IsNullOrEmpty(v.Password));
    }
}

public class CreateUserCommandHandler(
    IRepository<User> userRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IPasswordHasher passwordHasher,
    IMapper mapper) : IRequestHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var existingUsers = await userRepo.FindAsync(
            u => u.Email == command.Email && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);
        if (existingUsers.Count > 0)
            throw new ValidationException("Email", "A user with this email already exists in this tenant.");

        var user = new User(
            Guid.NewGuid(), tenantId, command.ExternalId,
            command.Email, command.FirstName, command.LastName,
            command.AvatarUrl);

        if (!string.IsNullOrEmpty(command.Password))
        {
            user.SetPasswordHash(passwordHasher.Hash(command.Password));
        }

        await userRepo.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<UserDto>(user);
    }
}
