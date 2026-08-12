using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Assistants.Commands;
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
    IConfiguration configuration,
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
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? assistantId = null, CancellationToken ct = default)
    {
        var query = new GetChatbotsQuery { Page = page, PageSize = pageSize, AssistantId = assistantId };
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

    [HttpGet("{id:guid}/channels")]
    public async Task<IActionResult> GetChannels(Guid id, CancellationToken ct = default)
    {
        var query = new GetChatbotChannelsQuery { ChatbotId = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/channels/{channel}")]
    public async Task<IActionResult> ConnectChannel(Guid id, string channel, [FromBody] System.Text.Json.JsonElement payload, CancellationToken ct = default)
    {
        var command = new ConnectChatbotChannelCommand
        {
            ChatbotId = id,
            Channel = channel,
            PayloadJson = payload.GetRawText(),
        };
        await mediator.Send(command, ct);
        return Ok();
    }

    [HttpDelete("{id:guid}/channels/{channel}")]
    public async Task<IActionResult> DisconnectChannel(Guid id, string channel, CancellationToken ct = default)
    {
        var command = new DisconnectChatbotChannelCommand { ChatbotId = id, Channel = channel };
        await mediator.Send(command, ct);
        return Ok();
    }

    [HttpGet("{id:guid}/webhook-key")]
    public async Task<IActionResult> GetWebhookKey(Guid id, CancellationToken ct = default)
    {
        var configuredHost = configuration["ApiPublicUrl"] ?? configuration["ApiBaseUrl"];
        var baseUrl = !string.IsNullOrWhiteSpace(configuredHost)
            ? configuredHost.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host}";
        var query = new GetWebhookKeyInfoQuery { ChatbotId = id, WebhookUrl = $"{baseUrl}/api/v1/chatbots/{id}/webhook" };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/webhook-key/regenerate")]
    public async Task<IActionResult> RegenerateWebhookKey(Guid id, CancellationToken ct = default)
    {
        var command = new RegenerateWebhookKeyCommand { ChatbotId = id };
        var result = await mediator.Send(command, ct);
        logger.LogInformation("Webhook API key regenerated for chatbot {ChatbotId}", id);
        return Ok(new
        {
            Key = result.RawKey,
            result.KeyPrefix,
            Message = "Store this key securely — it cannot be retrieved again."
        });
    }

    public record ChatbotPublicInfo(string Name, string? WelcomeMessage, bool VoiceEnabled);

    // Anonymous: the embeddable widget (ChatbotWidget.razor) runs on external, unauthenticated
    // websites and only needs enough to render its header/greeting — never the prompt template
    // or internal config IDs, which GetById exposes to authenticated tenant users.
    [HttpGet("{id:guid}/public-info")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicInfo(Guid id, CancellationToken ct = default)
    {
        var chatbot = await dbContext.Chatbots
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (chatbot is null)
            return NotFound(new { error = "Chatbot not found" });

        return Ok(new ChatbotPublicInfo(chatbot.Name, chatbot.WelcomeMessage, chatbot.VoiceEnabled));
    }

    public record ChatbotChatRequest(string Message);

    // Anonymous for the same reason as GetPublicInfo above — this is the endpoint the public
    // embed widget calls to actually send a message.
    [HttpPost("{id:guid}/chat")]
    [AllowAnonymous]
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
        // enableTools stays false: this endpoint is [AllowAnonymous] for the public embed
        // widget, and must never expose mutating tools (start_workflow, submit_approval_request,
        // notify_approver) to unauthenticated website visitors.
        var reply = await aiService.ChatAsync(request.Message, context, systemPrompt, enableTools: false, ct: ct);

        return Ok(new { reply });
    }

    // Streaming counterpart to Chat above — same [AllowAnonymous] rationale and the same
    // enableTools: false restriction. Mirrors AssistantsController's {id}/chat/stream so the
    // embed widget gets the same token-by-token UX as the internal Playground instead of a
    // blocking spinner.
    [HttpPost("{id:guid}/chat/stream")]
    [AllowAnonymous]
    public async Task StreamChat(Guid id, [FromBody] ChatbotChatRequest request, CancellationToken ct = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var streamCt = linkedCts.Token;

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var chatbot = await dbContext.Chatbots
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, streamCt);

        if (chatbot is null)
        {
            await WriteSseEventAsync("error", new { message = "Chatbot not found" }, streamCt);
            return;
        }

        string? context = null;
        List<CitationDto>? citations = null;

        if (chatbot.KnowledgeBaseId.HasValue)
        {
            try
            {
                var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                    chatbot.KnowledgeBaseId.Value, request.Message, 1, 5, streamCt);

                if (searchResult.Items.Count > 0)
                {
                    context = string.Join("\n\n", searchResult.Items.Select(i => i.Content));
                    citations = searchResult.Items
                        .Select((item, index) => new CitationDto(
                            item.SourceName ?? "Unknown",
                            item.Content,
                            (float)item.Score,
                            index + 1))
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to search knowledge base for chatbot {ChatbotId}", id);
            }
        }

        var systemPrompt = chatbot.PromptTemplate ?? "You are a helpful AI assistant.";

        await foreach (var chunk in aiService.StreamChatAsync(request.Message, context, systemPrompt, enableTools: false, ct: streamCt))
        {
            await WriteSseEventAsync("chunk", new { content = chunk }, streamCt);
        }

        if (citations is { Count: > 0 })
        {
            await WriteSseEventAsync("citations", new { citations }, streamCt);
        }

        await WriteSseEventAsync("done", new { message = "Stream complete" }, streamCt);
    }

    private async Task WriteSseEventAsync(string eventType, object data, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var sseMessage = $"event: {eventType}\ndata: {json}\n\n";
        await Response.WriteAsync(sseMessage, ct);
        await Response.Body.FlushAsync(ct);
    }
}
