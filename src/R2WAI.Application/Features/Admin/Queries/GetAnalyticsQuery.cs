namespace R2WAI.Application.Features.Admin.Queries;

public record GetAnalyticsQuery : IRequest<AnalyticsDto> { }

public class GetAnalyticsQueryHandler(
    IRepository<User> userRepo,
    IRepository<Conversation> conversationRepo,
    IRepository<Document> documentRepo,
    IRepository<Chatbot> chatbotRepo,
    IRepository<KnowledgeBase> kbRepo,
    IRepository<Workflow> workflowRepo,
    ICurrentUserService currentUser) : IRequestHandler<GetAnalyticsQuery, AnalyticsDto>
{
    public async Task<AnalyticsDto> Handle(GetAnalyticsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var users = await userRepo.FindAsync(
            u => u.TenantId == tenantId && !u.IsDeleted, cancellationToken);
        var conversations = await conversationRepo.FindAsync(
            c => c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        var documents = await documentRepo.FindAsync(
            d => d.TenantId == tenantId && !d.IsDeleted, cancellationToken);
        var chatbots = await chatbotRepo.FindAsync(
            c => c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        var knowledgeBases = await kbRepo.FindAsync(
            k => k.TenantId == tenantId && !k.IsDeleted, cancellationToken);
        var workflows = await workflowRepo.FindAsync(
            w => w.TenantId == tenantId && !w.IsDeleted, cancellationToken);

        // Messages nav property not eagerly loaded; count in-memory from what EF returns
        var today = DateTime.UtcNow.Date;
        var activeConversations = conversations.Where(c => c.Messages != null && c.Messages.Any(m => m.CreatedAt >= today)).ToList();

        return new AnalyticsDto
        {
            TotalUsers = users.Count,
            ActiveConversations = activeConversations.Count,
            TotalDocuments = documents.Count,
            TotalChatbots = chatbots.Count,
            TotalKnowledgeBases = knowledgeBases.Count,
            TotalWorkflows = workflows.Count,
            AiRequestsToday = activeConversations.Sum(c => c.Messages?.Count ?? 0),
            RequestsByModule = new Dictionary<string, int>
            {
                ["chat"] = conversations.Count(c => c.Module == "chat" || c.Module == null),
                ["document"] = documents.Count,
            },
        };
    }
}
