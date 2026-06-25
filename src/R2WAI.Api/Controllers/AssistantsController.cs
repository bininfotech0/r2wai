using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Assistants.Commands;
using R2WAI.Application.Features.Assistants.Queries;
using R2WAI.Infrastructure.AI.Prompts;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class AssistantsController(
    IMediator mediator,
    ApplicationDbContext dbContext,
    IAIService aiService,
    IKnowledgeBaseService knowledgeBaseService,
    ILogger<AssistantsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssistantCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating assistant: {Name}", command.Name);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var query = new GetAssistantsQuery { Page = page, PageSize = pageSize, Search = search };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetAssistantByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssistantCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteAssistantCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpGet("prompt-templates")]
    public IActionResult GetPromptTemplates()
    {
        var templates = R2WAI.Infrastructure.AI.Prompts.SystemPromptTemplates.GetAll();
        return Ok(new { items = templates.Select(t => new { type = t.Key, prompt = t.Value }) });
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct = default)
    {
        var assistant = await dbContext.AssistantDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assistant is null) return NotFound();
        assistant.Publish();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Assistant {Id} published", id);
        return Ok(new { id, isActive = true, status = "Published", publishedVersion = assistant.PublishedVersion });
    }

    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct = default)
    {
        var assistant = await dbContext.AssistantDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assistant is null) return NotFound();
        assistant.Unpublish();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Assistant {Id} unpublished", id);
        return Ok(new { id, isActive = false, status = "Draft" });
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct = default)
    {
        var assistant = await dbContext.AssistantDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (assistant is null) return NotFound();
        assistant.Archive();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Assistant {Id} archived", id);
        return Ok(new { id, isActive = false, status = "Archived" });
    }

    [HttpPost("{id:guid}/chat")]
    public async Task<IActionResult> Chat(Guid id, [FromBody] ChatWithAssistantCommand command, CancellationToken ct = default)
    {
        command = command with { AssistantId = id };
        var result = await mediator.Send(command, ct);
        return Ok(new
        {
            reply = result.Reply,
            conversationId = result.ConversationId,
            tokensUsed = result.TokensUsed,
            citations = result.Citations
        });
    }

    [HttpPost("{id:guid}/chat/stream")]
    public async Task StreamChat(Guid id, [FromBody] ChatWithAssistantCommand command, CancellationToken ct = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var assistant = await dbContext.AssistantDefinitions
            .Include(a => a.KnowledgeBase)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assistant is null)
        {
            await WriteSseEventAsync("error", new { message = "Assistant not found" }, ct);
            return;
        }

        string? context = null;
        List<CitationDto>? citations = null;

        if (assistant.KnowledgeBaseId.HasValue)
        {
            try
            {
                var searchResult = await knowledgeBaseService.SearchKnowledgeBaseAsync(
                    assistant.KnowledgeBaseId.Value, command.Message, 1, 5, ct);

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
                logger.LogWarning(ex, "Failed to search knowledge base for streaming chat");
            }
        }

        var systemPrompt = assistant.SystemPrompt ?? "You are a helpful AI assistant.";

        await foreach (var chunk in aiService.StreamChatAsync(command.Message, context, systemPrompt, ct))
        {
            await WriteSseEventAsync("chunk", new { content = chunk }, ct);
        }

        if (citations is { Count: > 0 })
        {
            await WriteSseEventAsync("citations", new { citations }, ct);
        }

        await WriteSseEventAsync("done", new { message = "Stream complete" }, ct);
    }

    private async Task WriteSseEventAsync(string eventType, object data, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var sseMessage = $"event: {eventType}\ndata: {json}\n\n";
        await Response.WriteAsync(sseMessage, ct);
        await Response.Body.FlushAsync(ct);
    }

    [HttpPost("generate-config")]
    public async Task<IActionResult> GenerateConfig([FromBody] GenerateConfigRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "Description is required" });

        try
        {
            var prompt =
                "You are an enterprise AI assistant configuration generator. " +
                "Based on the description below, return ONLY a valid JSON object — no markdown, no explanation.\n\n" +
                $"Description: {request.Description}\n\n" +
                "JSON format:\n" +
                "{\n" +
                "  \"name\": \"<Short professional name, 2-5 words>\",\n" +
                "  \"type\": \"<Exactly one of: General, HR, IT, Finance, Procurement, Legal>\",\n" +
                "  \"description\": \"<One sentence, max 150 characters>\",\n" +
                "  \"systemPrompt\": \"<Detailed behavioral system prompt, 150-350 words, professional tone>\"\n" +
                "}";

            var raw = await aiService.GenerateResponseAsync(prompt, ct: ct);

            var json = raw.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) json = json[7..];
            else if (json.StartsWith("```")) json = json[3..];
            if (json.EndsWith("```")) json = json[..^3];
            json = json.Trim();

            var cfg = System.Text.Json.JsonSerializer.Deserialize<AiConfigDto>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (cfg is not null && !string.IsNullOrWhiteSpace(cfg.Name))
            {
                logger.LogInformation("AI generated assistant config: {Name} ({Type})", cfg.Name, cfg.Type);
                return Ok(new
                {
                    name = cfg.Name,
                    type = cfg.Type ?? "General",
                    description = cfg.Description ?? string.Empty,
                    systemPrompt = cfg.SystemPrompt ?? string.Empty,
                    isAiGenerated = true
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI config generation failed, falling back to template");
        }

        // Template fallback
        var desc = request.Description.ToLowerInvariant();
        var type = desc.Contains("hr") || desc.Contains("onboard") || desc.Contains("employee") ? "HR"
            : desc.Contains(" it ") || desc.Contains("helpdesk") || desc.Contains("support") || desc.Contains("technical") ? "IT"
            : desc.Contains("finance") || desc.Contains("expense") || desc.Contains("invoice") || desc.Contains("budget") ? "Finance"
            : desc.Contains("legal") || desc.Contains("contract") || desc.Contains("compliance") ? "Legal"
            : desc.Contains("procure") || desc.Contains("vendor") || desc.Contains("purchase") ? "Procurement"
            : "General";

        var templates = SystemPromptTemplates.GetAll();
        var systemPrompt = templates.TryGetValue(type, out var tmpl) ? tmpl : templates["General"];
        var name = type == "General" ? "General Assistant" : $"{type} Assistant";

        return Ok(new
        {
            name,
            type,
            description = request.Description[..Math.Min(150, request.Description.Length)],
            systemPrompt,
            isAiGenerated = false
        });
    }

    public record GenerateConfigRequest(string Description);

    private record AiConfigDto(string Name, string? Type, string? Description, string? SystemPrompt);
}
