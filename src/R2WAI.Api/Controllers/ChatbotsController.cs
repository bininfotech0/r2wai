using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Chatbots.Commands;
using R2WAI.Application.Features.Chatbots.Queries;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ChatbotsController(
    IMediator mediator,
    ApplicationDbContext dbContext,
    IAIService aiService,
    IKnowledgeBaseService knowledgeBaseService,
    ILogger<ChatbotsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChatbotCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating chatbot: {Name}", command.Name);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new GetChatbotsQuery { Page = page, PageSize = pageSize };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetChatbotByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChatbotCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteChatbotCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    public record ChatbotChatRequest(string Message);

    [AllowAnonymous]
    [HttpPost("{id:guid}/chat")]
    public async Task<IActionResult> Chat(Guid id, [FromBody] ChatbotChatRequest request, CancellationToken ct = default)
    {
        var chatbot = await dbContext.Chatbots
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chatbot is null)
            return NotFound(new { error = "Chatbot not found" });

        string? context = null;
        if (chatbot.KnowledgeBaseId.HasValue)
        {
            try
            {
                var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                    chatbot.KnowledgeBaseId.Value, request.Message, 1, 5, ct);

                if (searchResult.Items.Count > 0)
                    context = string.Join("\n\n", searchResult.Items.Select(i => i.Content));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to search knowledge base for chatbot {ChatbotId}", id);
            }
        }

        var systemPrompt = chatbot.PromptTemplate ?? "You are a helpful AI assistant.";
        var reply = await aiService.ChatAsync(request.Message, context, systemPrompt, ct);

        return Ok(new { reply });
    }
}
