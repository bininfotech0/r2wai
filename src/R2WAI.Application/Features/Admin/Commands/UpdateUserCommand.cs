using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record UpdateUserCommand : IRequest<UserDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string? MobileNumber { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.LastName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.MobileNumber)
            .Matches(@"^(\+91)?[6-9]\d{9}$")
            .When(v => !string.IsNullOrWhiteSpace(v.MobileNumber))
            .WithMessage("Please enter a valid Indian mobile number.");
    }
}

public class UpdateUserCommandHandler(
    IRepository<User> userRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), command.Id);

        user.UpdateProfile(command.FirstName, command.LastName, command.AvatarUrl);
        user.SetMobileNumber(string.IsNullOrWhiteSpace(command.MobileNumber) ? null : command.MobileNumber.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<UserDto>(user);
    }
}
