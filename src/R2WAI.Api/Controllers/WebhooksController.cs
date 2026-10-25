using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;
using System.Text.RegularExpressions;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SystemAdmin")]
[Route("api/v1/admin/webhooks")]
public class WebhooksController(
    ApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    ILogger<WebhooksController> logger) : ControllerBase
{
    private static readonly Regex ValidSlug = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
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
        return Ok(new
        {
            webhook.Id,
            webhook.Name,
            webhook.Slug,
            webhook.TriggerType,
            webhook.WorkflowId,
            webhook.IsActive,
            webhook.LastCalledAt,
            webhook.TotalCalls,
            webhook.CreatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWebhookRequest request, CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant context not found");

        var slug = (request.Slug ?? request.Name.ToLowerInvariant().Replace(' ', '-')).Trim();
        if (!ValidSlug.IsMatch(slug) || slug.Length > 100)
            return BadRequest(new { error = "Slug must use lowercase letters, numbers, and single hyphens (maximum 100 characters)." });
        if (string.IsNullOrWhiteSpace(request.Secret) || request.Secret.Length < 32)
            return BadRequest(new { error = "Webhook secret must be at least 32 characters." });
        if (request.WorkflowId is not Guid workflowId)
            return BadRequest(new { error = "Select an automation for this webhook." });
        if (!await dbContext.Workflows.AnyAsync(w => w.Id == workflowId && w.TenantId == tenantId && !w.IsDeleted, ct))
            return BadRequest(new { error = "The selected automation does not exist in this tenant." });

        var exists = await dbContext.WebhookEndpoints.IgnoreQueryFilters()
            .AnyAsync(w => w.Slug == slug && !w.IsDeleted, ct);
        if (exists) return BadRequest(new { error = "Webhook slug is already in use." });

        var webhook = new WebhookEndpoint(
            Guid.NewGuid(), tenantId, request.Name, slug,
            request.TriggerType, request.WorkflowId, EncryptSecret(request.Secret));

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

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant context not found");
        if (request.WorkflowId is not Guid workflowId)
            return BadRequest(new { error = "Select an automation for this webhook." });
        if (!await dbContext.Workflows.AnyAsync(w => w.Id == workflowId && w.TenantId == tenantId && !w.IsDeleted, ct))
            return BadRequest(new { error = "The selected automation does not exist in this tenant." });
        if (!string.IsNullOrWhiteSpace(request.Secret) && request.Secret.Length < 32)
            return BadRequest(new { error = "Webhook secret must be at least 32 characters." });

        webhook.Update(request.Name, request.TriggerType, request.WorkflowId,
            string.IsNullOrWhiteSpace(request.Secret) ? null : EncryptSecret(request.Secret));
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

    private string EncryptSecret(string secret) => $"enc:v1:{encryptionService.Encrypt(secret)}";
}
