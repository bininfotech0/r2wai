using AutoMapper;
using R2WAI.Application.Features.Chat.DTOs;

namespace R2WAI.Application.Features.Chat.Mappings;

public class ChatProfile : Profile
{
    public ChatProfile()
    {
        CreateMap<Conversation, ConversationDto>()
            .ForMember(d => d.MessageCount, o => o.MapFrom(s => s.Messages.Count))
            .ForMember(d => d.LastMessageAt, o => o.MapFrom(s => s.Messages.Any() ? s.Messages.Max(m => m.CreatedAt) : (DateTime?)null));

        CreateMap<Message, MessageDto>();

        CreateMap<MessageAttachment, MessageAttachmentDto>();
    }
}
