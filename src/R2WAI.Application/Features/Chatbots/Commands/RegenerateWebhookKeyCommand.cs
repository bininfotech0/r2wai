using System.Security.Cryptography;
using System.Text;
using FluentValidation;

namespace R2WAI.Application.Features.Chatbots.Commands;

public record RegenerateWebhookKeyCommand : IRequest<RegenerateWebhookKeyResultDto>
{
    public Guid ChatbotId { get; init; }
}

public class RegenerateWebhookKeyCommandValidator : AbstractValidator<RegenerateWebhookKeyCommand>
{
    public RegenerateWebhookKeyCommandValidator()
    {
        RuleFor(v => v.ChatbotId).NotEmpty();
    }
}

public class RegenerateWebhookKeyCommandHandler(
    IRepository<Chatbot> chatbotRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<RegenerateWebhookKeyCommand, RegenerateWebhookKeyResultDto>
{
    public async Task<RegenerateWebhookKeyResultDto> Handle(RegenerateWebhookKeyCommand command, CancellationToken cancellationToken)
    {
        var chatbot = await chatbotRepo.GetByIdAsync(command.ChatbotId, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), command.ChatbotId);

        var rawKey = $"r2w_{GenerateRandomKey(32)}";
        var keyHash = HashKey(rawKey);
        var keyPrefix = rawKey[..8];

        chatbot.SetWebhookApiKey(keyHash, keyPrefix);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegenerateWebhookKeyResultDto { RawKey = rawKey, KeyPrefix = keyPrefix };
    }

    private static string GenerateRandomKey(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length + 8);
        var encoded = Convert.ToBase64String(bytes)
            .Replace("+", "").Replace("/", "").Replace("=", "");
        return encoded[..Math.Min(length, encoded.Length)];
    }

    private static string HashKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hash);
    }
}
