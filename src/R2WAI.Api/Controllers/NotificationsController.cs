using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class NotificationsController(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUser) : ControllerBase
{
    public record NotificationDto(Guid Id, string Title, string Message, string Type, string? Link, bool IsRead, DateTime CreatedAt);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        var query = dbContext.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Type, n.Link, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        var unreadCount = await dbContext.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return Ok(new { items, totalCount, page, pageSize, unreadCount });
    }

    // The list endpoint above already computes this alongside its page of results, but a navbar
    // badge that polls frequently shouldn't have to load and paginate a full page of notifications
    // just to show one number.
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        var unreadCount = await dbContext.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return Ok(new { unreadCount });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);
        if (notification is null)
            return NotFound(new { error = "Notification not found" });

        notification.MarkRead();
        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        var unread = await dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        foreach (var notification in unread)
            notification.MarkRead();

        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }
}
