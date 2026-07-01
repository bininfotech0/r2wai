using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Chat.Commands;
using R2WAI.Application.Features.Chat.DTOs;
using R2WAI.Application.Features.Chat.Queries;
using R2WAI.Application.Common.Models;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ChatController(IMediator mediator, IAIService aiService, ILogger<ChatController> logger) : ControllerBase
{
    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating conversation: {Title}", command.Title);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetConversation), new { id = result.Id }, result);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? module = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new GetConversationsQuery { Page = page, PageSize = pageSize, Module = module };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> GetConversation(Guid id, CancellationToken ct = default)
    {
        var query = new GetConversationByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteConversationCommand { ConversationId = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("conversations/{id:guid}/messages")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> SendMessage(Guid id, [FromForm] string content, [FromForm] List<IFormFile>? attachments, [FromForm] string? idempotencyKey, CancellationToken ct = default)
    {
        var attachmentDtos = new List<MessageAttachmentDto>();

        if (attachments?.Count > 0)
        {
            foreach (var file in attachments)
            {
                if (file.Length > 50 * 1024 * 1024)
                    return BadRequest(new { error = $"File '{file.FileName}' exceeds the 50MB size limit." });

                var safeFileName = Path.GetFileName(file.FileName);
                var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{safeFileName}");
                await using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, ct);
                }

                attachmentDtos.Add(new MessageAttachmentDto
                {
                    Id = Guid.NewGuid(),
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    TempFilePath = tempPath
                });
                
                // Note: We need a way to pass the actual temp file path to the handler 
                // since the handler will upload it to storage. 
                // I'll update the DTO or Command to include this.
            }
        }

        var command = new SendMessageCommand
        {
            ConversationId = id,
            Content = content,
            Attachments = attachmentDtos,
            IdempotencyKey = idempotencyKey
        };

        try
        {
            var result = await mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetMessages), new { id }, result);
        }
        finally
        {
            foreach (var att in attachmentDtos.Where(a => !string.IsNullOrEmpty(a.TempFilePath)))
                try { System.IO.File.Delete(att.TempFilePath!); } catch { }
        }
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new GetMessagesQuery { ConversationId = id, Page = page, PageSize = pageSize };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    public record StreamRequest(string Message, string? SystemPrompt = null, string? ConversationHistory = null);

    [HttpPost("stream")]
    public async Task StreamMessage([FromBody] StreamRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("Streaming chat response");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var streamCt = linkedCts.Token;

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        var writer = Response.Body;
        var encoding = Encoding.UTF8;

        await foreach (var chunk in aiService.StreamChatAsync(
            request.Message, request.ConversationHistory, request.SystemPrompt, streamCt))
        {
            var sseData = $"data: {System.Text.Json.JsonSerializer.Serialize(new { content = chunk })}\n\n";
            await writer.WriteAsync(encoding.GetBytes(sseData), streamCt);
            await writer.FlushAsync(streamCt);
        }

        await writer.WriteAsync(encoding.GetBytes("data: [DONE]\n\n"), streamCt);
        await writer.FlushAsync(streamCt);
    }

    [HttpGet("suggested-actions")]
    public async Task<IActionResult> GetSuggestedActions(
        [FromQuery] Guid? conversationId = null,
        CancellationToken ct = default)
    {
        var query = new GetSuggestedActionsQuery { ConversationId = conversationId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }
}
