using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/runs")]
public sealed class RunsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? applicationId = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? assistantId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var tenantId = dbContext.TenantId;
        if (tenantId is null) return Unauthorized();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var normalizedType = type?.Trim();
        var normalizedStatus = status?.Trim();
        var normalizedSearch = search?.Trim().ToLowerInvariant();

        var workflowRuns = dbContext.WorkflowInstances.AsNoTracking()
            .Where(instance => instance.TenantId == tenantId.Value);
        if (string.Equals(normalizedType, "Assistant", StringComparison.OrdinalIgnoreCase))
            workflowRuns = workflowRuns.Where(_ => false);
        if (applicationId.HasValue)
            workflowRuns = workflowRuns.Where(instance => instance.Workflow.ApplicationId == applicationId);
        if (assistantId.HasValue)
            workflowRuns = workflowRuns.Where(_ => false);
        if (userId.HasValue)
            workflowRuns = workflowRuns.Where(instance => instance.InitiatedBy == userId);
        if (from.HasValue)
            workflowRuns = workflowRuns.Where(instance => (instance.StartedAt ?? instance.CreatedAt) >= from.Value);
        if (to.HasValue)
            workflowRuns = workflowRuns.Where(instance => (instance.StartedAt ?? instance.CreatedAt) < to.Value);
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
            workflowRuns = workflowRuns.Where(instance =>
                instance.Workflow.Name.ToLower().Contains(normalizedSearch) ||
                instance.Initiator.FirstName.ToLower().Contains(normalizedSearch) ||
                instance.Initiator.LastName.ToLower().Contains(normalizedSearch) ||
                (instance.Workflow.Application != null && instance.Workflow.Application.Name.ToLower().Contains(normalizedSearch)) ||
                dbContext.AuditLogs.Any(log => log.EntityType == "WorkflowInstance" && log.EntityId == instance.Id.ToString() &&
                    log.CorrelationId != null && log.CorrelationId.ToLower().Contains(normalizedSearch)));
        if (!string.IsNullOrWhiteSpace(normalizedStatus) &&
            Enum.TryParse<WorkflowInstanceStatus>(normalizedStatus, true, out var workflowStatus))
            workflowRuns = workflowRuns.Where(instance => instance.Status == workflowStatus);
        else if (!string.IsNullOrWhiteSpace(normalizedStatus) &&
                 !string.Equals(normalizedStatus, "all", StringComparison.OrdinalIgnoreCase))
            workflowRuns = workflowRuns.Where(_ => false);

        var conversations = dbContext.Conversations.AsNoTracking()
            .Where(conversation => conversation.TenantId == tenantId.Value &&
                                   conversation.UserId == currentUserId &&
                                   !conversation.IsArchived && conversation.Module == "assistant");
        if (string.Equals(normalizedType, "Automation", StringComparison.OrdinalIgnoreCase))
            conversations = conversations.Where(_ => false);
        if (applicationId.HasValue)
            conversations = conversations.Where(conversation => conversation.ReferenceId.HasValue &&
                dbContext.AssistantDefinitions.Any(assistant => assistant.Id == conversation.ReferenceId && assistant.ApplicationId == applicationId));
        if (userId.HasValue && userId.Value != currentUserId)
            conversations = conversations.Where(_ => false);
        if (assistantId.HasValue)
            conversations = conversations.Where(conversation => conversation.ReferenceId == assistantId);
        if (from.HasValue)
            conversations = conversations.Where(conversation => conversation.CreatedAt >= from.Value);
        if (to.HasValue)
            conversations = conversations.Where(conversation => conversation.CreatedAt < to.Value);
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
            conversations = conversations.Where(conversation =>
                conversation.Title.ToLower().Contains(normalizedSearch) ||
                conversation.User.FirstName.ToLower().Contains(normalizedSearch) ||
                conversation.User.LastName.ToLower().Contains(normalizedSearch) ||
                dbContext.AssistantDefinitions.Any(assistant => assistant.Id == conversation.ReferenceId &&
                    (assistant.Name.ToLower().Contains(normalizedSearch) ||
                     (assistant.Application != null && assistant.Application.Name.ToLower().Contains(normalizedSearch)))) ||
                dbContext.AuditLogs.Any(log => log.EntityType == "Conversation" &&
                    log.EntityId == conversation.Id.ToString() && log.CorrelationId != null &&
                    log.CorrelationId.ToLower().Contains(normalizedSearch)));
        if (string.Equals(normalizedStatus, "Active", StringComparison.OrdinalIgnoreCase))
            conversations = conversations.Where(conversation => dbContext.Messages.Any(message => message.ConversationId == conversation.Id));
        else if (string.Equals(normalizedStatus, "Started", StringComparison.OrdinalIgnoreCase))
            conversations = conversations.Where(conversation => !dbContext.Messages.Any(message => message.ConversationId == conversation.Id));
        else if (!string.IsNullOrWhiteSpace(normalizedStatus) &&
                 !string.Equals(normalizedStatus, "all", StringComparison.OrdinalIgnoreCase))
            conversations = conversations.Where(_ => false);

        var workflowCount = await workflowRuns.CountAsync(ct);
        var conversationCount = await conversations.CountAsync(ct);
        var totalCount = workflowCount + conversationCount;
        page = Math.Min(page, Math.Max(1, (totalCount + pageSize - 1) / pageSize));
        var skip = (page - 1) * pageSize;
        var take = skip + pageSize;
        var workflowItems = await workflowRuns
            .OrderByDescending(instance => instance.StartedAt ?? instance.CreatedAt)
            .Take(take)
            .Select(instance => new RunProjection(
                instance.Id,
                "Automation",
                instance.Workflow.Name,
                instance.Status.ToString(),
                instance.StartedAt ?? instance.CreatedAt,
                instance.CompletedAt,
                instance.Workflow.ApplicationId,
                instance.Workflow.Application == null ? null : instance.Workflow.Application.Name,
                null,
                null,
                instance.InitiatedBy,
                instance.Initiator.FirstName + " " + instance.Initiator.LastName,
                dbContext.AuditLogs.Where(log => log.EntityType == "WorkflowInstance" && log.EntityId == instance.Id.ToString())
                    .OrderByDescending(log => log.Timestamp).Select(log => log.CorrelationId).FirstOrDefault(),
                instance.StartedAt ?? instance.CreatedAt))
            .ToListAsync(ct);
        var conversationItems = await conversations
            .OrderByDescending(conversation => conversation.CreatedAt)
            .Take(take)
            .Select(conversation => new RunProjection(
                conversation.Id,
                "Assistant",
                conversation.Title,
                dbContext.Messages.Any(message => message.ConversationId == conversation.Id) ? "Active" : "Started",
                conversation.CreatedAt,
                dbContext.Messages.Where(message => message.ConversationId == conversation.Id)
                    .Select(message => (DateTime?)message.CreatedAt).Max(),
                conversation.ReferenceId.HasValue
                    ? dbContext.AssistantDefinitions.Where(assistant => assistant.Id == conversation.ReferenceId).Select(assistant => assistant.ApplicationId).FirstOrDefault()
                    : null,
                conversation.ReferenceId.HasValue
                    ? dbContext.AssistantDefinitions.Where(assistant => assistant.Id == conversation.ReferenceId).Select(assistant => assistant.Application == null ? null : assistant.Application.Name).FirstOrDefault()
                    : null,
                conversation.ReferenceId,
                conversation.ReferenceId.HasValue
                    ? dbContext.AssistantDefinitions.Where(assistant => assistant.Id == conversation.ReferenceId).Select(assistant => assistant.Name).FirstOrDefault()
                    : null,
                conversation.UserId,
                conversation.User.FirstName + " " + conversation.User.LastName,
                dbContext.AuditLogs.Where(log => log.EntityType == "Conversation" && log.EntityId == conversation.Id.ToString())
                    .OrderByDescending(log => log.Timestamp).Select(log => log.CorrelationId).FirstOrDefault(),
                conversation.CreatedAt))
            .ToListAsync(ct);

        var items = workflowItems.Concat(conversationItems)
            .OrderByDescending(item => item.SortAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(item => new RunItemDto(item.Id, item.Type, item.Name, item.Status, item.StartedAt,
                item.CompletedAt, item.ApplicationId, item.ApplicationName, item.LinkedId, item.LinkedName,
                item.UserId, item.UserName, item.CorrelationId))
            .ToArray();

        return Ok(new
        {
            items,
            totalCount,
            page,
            pageSize
        });
    }

    [HttpGet("filters")]
    public async Task<IActionResult> GetFilters(CancellationToken ct = default)
    {
        if (dbContext.TenantId is null) return Unauthorized();

        var applications = await dbContext.Applications.AsNoTracking()
            .OrderBy(application => application.Name)
            .Select(application => new FilterOption(application.Id, application.Name))
            .ToListAsync(ct);
        var users = await dbContext.Users.AsNoTracking()
            .OrderBy(user => user.FirstName).ThenBy(user => user.LastName)
            .Select(user => new FilterOption(user.Id, user.FirstName + " " + user.LastName))
            .ToListAsync(ct);
        var assistants = await dbContext.AssistantDefinitions.AsNoTracking()
            .OrderBy(assistant => assistant.Name)
            .Select(assistant => new FilterOption(assistant.Id, assistant.Name))
            .ToListAsync(ct);

        return Ok(new { applications, users, assistants });
    }

    private sealed record RunProjection(
        Guid Id, string Type, string Name, string Status, DateTime StartedAt, DateTime? CompletedAt,
        Guid? ApplicationId, string? ApplicationName, Guid? LinkedId, string? LinkedName,
        Guid? UserId, string? UserName, string? CorrelationId, DateTime SortAt);

    private sealed record RunItemDto(
        Guid Id, string Type, string Name, string Status, DateTime StartedAt, DateTime? CompletedAt,
        Guid? ApplicationId, string? ApplicationName, Guid? LinkedId, string? LinkedName,
        Guid? UserId, string? UserName, string? CorrelationId);

    private sealed record FilterOption(Guid Id, string Label);
}
