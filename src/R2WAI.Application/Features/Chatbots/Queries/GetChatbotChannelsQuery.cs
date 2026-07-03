namespace R2WAI.Application.Features.Chatbots.Queries;

public record GetChatbotChannelsQuery : IRequest<List<ChatbotChannelDto>>
{
    public Guid ChatbotId { get; init; }
}

public class GetChatbotChannelsQueryHandler(
    IRepository<ChatbotChannel> channelRepo) : IRequestHandler<GetChatbotChannelsQuery, List<ChatbotChannelDto>>
{
    public async Task<List<ChatbotChannelDto>> Handle(GetChatbotChannelsQuery query, CancellationToken cancellationToken)
    {
        var channels = await channelRepo.FindAsync(c => c.ChatbotId == query.ChatbotId, cancellationToken);

        return channels.Select(c => new ChatbotChannelDto
        {
            ChannelType = c.ChannelType,
            IsConnected = c.IsConnected,
            ConnectedAt = c.ConnectedAt,
        }).ToList();
    }
}
