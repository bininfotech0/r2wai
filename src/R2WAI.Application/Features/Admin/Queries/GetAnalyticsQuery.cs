namespace R2WAI.Application.Features.Admin.Queries;

public record GetAnalyticsQuery : IRequest<AnalyticsDto>, IAuthorizedRequest
{
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class GetAnalyticsQueryHandler(
    IRepository<User> userRepo,
    IRepository<Conversation> conversationRepo,
    IRepository<Document> documentRepo,
    IRepository<Chatbot> chatbotRepo,
    IRepository<KnowledgeBase> kbRepo,
    IRepository<Workflow> workflowRepo,
    IRepository<Message> messageRepo,
    ICurrentUserService currentUser) : IRequestHandler<GetAnalyticsQuery, AnalyticsDto>
{
    public async Task<AnalyticsDto> Handle(GetAnalyticsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var today = DateTime.UtcNow.Date;

        var totalUsers = await userRepo.CountAsync(
            u => u.TenantId == tenantId && !u.IsDeleted, cancellationToken);
        var totalConversations = await conversationRepo.CountAsync(
            c => c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        var totalDocuments = await documentRepo.CountAsync(
            d => d.TenantId == tenantId && !d.IsDeleted, cancellationToken);
        var totalChatbots = await chatbotRepo.CountAsync(
            c => c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        var totalKnowledgeBases = await kbRepo.CountAsync(
            k => k.TenantId == tenantId && !k.IsDeleted, cancellationToken);
        var totalWorkflows = await workflowRepo.CountAsync(
            w => w.TenantId == tenantId && !w.IsDeleted, cancellationToken);

        var todayMessages = await messageRepo.FindAsync(
            m => m.TenantId == tenantId && m.CreatedAt >= today && !m.IsDeleted, cancellationToken);
        var activeConversationIds = todayMessages.Select(m => m.ConversationId).Distinct().Count();

        var chatConversations = await conversationRepo.CountAsync(
            c => c.TenantId == tenantId && !c.IsDeleted && (c.Module == "chat" || c.Module == null), cancellationToken);

        return new AnalyticsDto
        {
            TotalUsers = totalUsers,
            ActiveConversations = activeConversationIds,
            TotalDocuments = totalDocuments,
            TotalChatbots = totalChatbots,
            TotalKnowledgeBases = totalKnowledgeBases,
            TotalWorkflows = totalWorkflows,
            AiRequestsToday = todayMessages.Count,
            RequestsByModule = new Dictionary<string, int>
            {
                ["chat"] = chatConversations,
                ["document"] = totalDocuments,
            },
        };
    }
}
