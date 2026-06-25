using AutoMapper;
using R2WAI.Application.Features.Chatbots.DTOs;

namespace R2WAI.Application.Features.Chatbots.Mappings;

public class ChatbotProfile : Profile
{
    public ChatbotProfile()
    {
        CreateMap<Chatbot, ChatbotDto>();
    }
}
