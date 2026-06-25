using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SystemAdmin")]
[Route("api/v1/admin/webhooks")]
public class WebhooksController(ApplicationDbContext dbContext, ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = dbContext.WebhookEndpoints
            .Where(w => !w.IsDeleted)
            .OrderByDescending(w => w.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(w => new
            {
                w.Id,
                w.Name,
                EndpointUrl = $"/api/v1/workflows/webhook/{w.Slug}",
                w.TriggerType,
                w.WorkflowId,
                w.IsActive,
                w.LastCalledAt,
                w.TotalCalls,
                w.CreatedAt
            }).ToListAsync(ct);

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var webhook = await dbContext.WebhookEndpoints.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, ct);
        if (webhook is null) return NotFound();
        return Ok(webhook);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWebhookRequest request, CancellationToken ct = default)
    {
        var tenantClaim = User.FindFirst("tenant_id");
        var tenantId = tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tid) ? tid : Guid.Empty;

        var slug = request.Slug ?? request.Name.ToLowerInvariant().Replace(' ', '-');

        var exists = await dbContext.WebhookEndpoints.AnyAsync(w => w.Slug == slug && !w.IsDeleted, ct);
        if (exists) return BadRequest(new { error = "Webhook slug already exists" });

        var webhook = new WebhookEndpoint(
            Guid.NewGuid(), tenantId, request.Name, slug,
            request.TriggerType, request.WorkflowId, request.Secret);

        dbContext.WebhookEndpoints.Add(webhook);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Webhook created: {Name} (/{Slug})", request.Name, slug);
        return CreatedAtAction(nameof(GetById), new { id = webhook.Id },
            new { webhook.Id, webhook.Name, EndpointUrl = $"/api/v1/workflows/webhook/{slug}" });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWebhookRequest request, CancellationToken ct = default)
    {
        var webhook = await dbContext.WebhookEndpoints.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, ct);
        if (webhook is null) return NotFound();

        webhook.Update(request.Name, request.TriggerType, request.WorkflowId, request.Secret);
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { webhook.Id, webhook.Name });
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct = default)
    {
        var webhook = await dbContext.WebhookEndpoints.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, ct);
        if (webhook is null) return NotFound();

        webhook.SetActive(!webhook.IsActive);
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { webhook.Id, webhook.IsActive });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var webhook = await dbContext.WebhookEndpoints.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, ct);
        if (webhook is null) return NotFound();

        webhook.SoftDelete();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Webhook deleted: {Id}", id);
        return NoContent();
    }

    public record CreateWebhookRequest(string Name, string? Slug, string TriggerType, Guid? WorkflowId, string? Secret);
    public record UpdateWebhookRequest(string Name, string TriggerType, Guid? WorkflowId, string? Secret);
}
