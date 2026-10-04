using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Assistants.Commands;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

/// <summary>
/// The standalone "publish an assistant as its own REST endpoint" surface (implementation plan
/// Phase 1) — distinct from AssistantsController (internal JWT-authenticated management API) and
/// from ChatbotsController's anonymous widget (no auth at all, no tools, tied to a Chatbot's own
/// independently-edited fields). Reached via an API key scoped to this specific assistant
/// ("assistant:{id}" in ApiKey.Scopes, reusing the existing generic scopes field per
/// ApiKeysController — no new auth scheme), or by an Admin/SystemAdmin JWT for direct testing.
/// Every call is pinned to the assistant's published AssistantVersion snapshot — see
/// ChatWithPublishedAssistantCommand.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/published-assistants")]
public class PublishedAssistantsController(
    IMediator mediator,
    ApplicationDbContext dbContext,
    ICurrentUserService currentUser) : ControllerBase
{
    private bool CanAccessAssistant(Guid assistantId)
    {
        if (currentUser.Roles.Contains("Admin") || currentUser.Roles.Contains("SystemAdmin"))
            return true;

        var scopes = User.FindAll("scope").Select(c => c.Value);
        return scopes.Contains($"assistant:{assistantId}", StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet("{assistantId:guid}/info")]
    public async Task<IActionResult> GetInfo(Guid assistantId, CancellationToken ct = default)
    {
        if (!CanAccessAssistant(assistantId))
            return Forbid();

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        var info = await dbContext.AssistantDefinitions
            .Where(a => a.Id == assistantId && a.TenantId == tenantId)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Description,
                PublishedVersionNumber = dbContext.Set<AssistantVersion>()
                    .Where(v => v.AssistantDefinitionId == a.Id && v.IsPublished)
                    .Select(v => (int?)v.VersionNumber)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (info is null) return NotFound(new { error = "Assistant not found." });
        if (info.PublishedVersionNumber is null)
            return UnprocessableEntity(new { error = $"Assistant '{info.Name}' has no published version." });

        return Ok(info);
    }

    public record PublishedAssistantChatRequest(string Message, Guid? ConversationId = null);

    [HttpPost("{assistantId:guid}/chat")]
    public async Task<IActionResult> Chat(Guid assistantId, [FromBody] PublishedAssistantChatRequest request, CancellationToken ct = default)
    {
        if (!CanAccessAssistant(assistantId))
            return Forbid();

        var result = await mediator.Send(new ChatWithPublishedAssistantCommand
        {
            AssistantId = assistantId,
            Message = request.Message,
            ConversationId = request.ConversationId,
        }, ct);

        return Ok(new
        {
            conversationId = result.ConversationId,
            reply = result.Reply,
            citations = result.Citations,
            durationMs = result.DurationMs,
        });
    }
}
