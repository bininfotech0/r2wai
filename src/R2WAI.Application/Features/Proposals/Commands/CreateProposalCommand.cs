using FluentValidation;

namespace R2WAI.Application.Features.Proposals.Commands;

public record CreateProposalCommand : IRequest<ProposalDto>
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ClientName { get; init; }
    public DateTime? DueDate { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
    public Guid? AssistantId { get; init; }
    public string? RfpContent { get; init; }
}

public class CreateProposalCommandValidator : AbstractValidator<CreateProposalCommand>
{
    public CreateProposalCommandValidator()
    {
        RuleFor(v => v.Title).NotEmpty().MaximumLength(200);
        RuleFor(v => v.RfpContent).MaximumLength(100_000);
    }
}

public class CreateProposalCommandHandler(
    ICurrentUserService currentUser,
    IAIService aiService,
    IKnowledgeBaseService knowledgeBaseService,
    ICacheService cache,
    ILogger<CreateProposalCommandHandler> logger) : IRequestHandler<CreateProposalCommand, ProposalDto>
{
    public async Task<ProposalDto> Handle(CreateProposalCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        string? context = null;
        if (command.KnowledgeBaseId.HasValue)
        {
            try
            {
                var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                    command.KnowledgeBaseId.Value,
                    command.RfpContent ?? command.Title,
                    1, 10, cancellationToken);

                if (searchResult.Items.Count > 0)
                    context = string.Join("\n\n", searchResult.Items.Select(i => i.Content));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to search knowledge base for proposal generation");
            }
        }

        var prompt = BuildProposalPrompt(command, context);
        var generatedContent = await aiService.GenerateResponseAsync(prompt, ct: cancellationToken);

        var proposal = new ProposalDto
        {
            Id = Guid.NewGuid(),
            Title = command.Title,
            Description = command.Description,
            Status = "Draft",
            ClientName = command.ClientName,
            DueDate = command.DueDate,
            GeneratedContent = generatedContent,
            WordCount = generatedContent?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            KnowledgeBaseId = command.KnowledgeBaseId,
            AssistantId = command.AssistantId,
            CreatedAt = DateTime.UtcNow,
        };

        logger.LogInformation("Proposal generated: {Title} ({WordCount} words)", command.Title, proposal.WordCount);
        return proposal;
    }

    private static string BuildProposalPrompt(CreateProposalCommand command, string? context)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("You are an expert enterprise proposal writer. Generate a professional RFP response.");
        sb.AppendLine();
        sb.AppendLine($"Proposal Title: {command.Title}");

        if (!string.IsNullOrEmpty(command.ClientName))
            sb.AppendLine($"Client: {command.ClientName}");

        if (!string.IsNullOrEmpty(command.Description))
            sb.AppendLine($"Description: {command.Description}");

        if (!string.IsNullOrEmpty(command.RfpContent))
        {
            sb.AppendLine();
            sb.AppendLine("RFP Requirements:");
            sb.AppendLine(command.RfpContent);
        }

        if (!string.IsNullOrEmpty(context))
        {
            sb.AppendLine();
            sb.AppendLine("Relevant Company Knowledge:");
            sb.AppendLine(context);
        }

        sb.AppendLine();
        sb.AppendLine("Generate a comprehensive, professional proposal response with:");
        sb.AppendLine("1. Executive Summary");
        sb.AppendLine("2. Understanding of Requirements");
        sb.AppendLine("3. Proposed Solution");
        sb.AppendLine("4. Implementation Approach");
        sb.AppendLine("5. Timeline");
        sb.AppendLine("6. Team & Qualifications");
        sb.AppendLine("7. Pricing Overview");

        return sb.ToString();
    }
}
