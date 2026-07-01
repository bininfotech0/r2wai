using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SystemAdmin")]
[Route("api/v1/admin/api-keys")]
public class ApiKeysController(ApplicationDbContext dbContext, ILogger<ApiKeysController> logger) : ControllerBase
{
    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id");
        return claim != null && Guid.TryParse(claim.Value, out var tid) ? tid : Guid.Empty;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var uid) ? uid : Guid.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var tenantId = GetTenantId();

        var query = dbContext.ApiKeys
            .Where(k => !k.IsDeleted && k.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(k => k.Name.Contains(search));

        if (status == "active")
            query = query.Where(k => k.IsActive);
        else if (status == "revoked")
            query = query.Where(k => !k.IsActive);

        query = query.OrderByDescending(k => k.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(k => new
            {
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.Scopes,
                k.Roles,
                k.IsActive,
                k.ExpiresAt,
                k.LastUsedAt,
                k.CreatedAt,
                k.ModifiedAt,
                k.CreatedByUserId
            }).ToListAsync(ct);

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var apiKey = await dbContext.ApiKeys
            .Where(k => k.Id == id && !k.IsDeleted && k.TenantId == tenantId)
            .Select(k => new
            {
                k.Id,
                k.Name,
                k.KeyPrefix,
                k.Scopes,
                k.Roles,
                k.IsActive,
                k.ExpiresAt,
                k.LastUsedAt,
                k.CreatedAt,
                k.ModifiedAt,
                k.CreatedByUserId
            })
            .FirstOrDefaultAsync(ct);

        if (apiKey is null) return NotFound();
        return Ok(apiKey);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        var tenantId = GetTenantId();
        var userId = GetUserId();

        var rawKey = $"r2w_{GenerateRandomKey(32)}";
        var keyHash = HashKey(rawKey);
        var keyPrefix = rawKey[..8];

        var scopes = request.Scopes is { Length: > 0 } ? string.Join(",", request.Scopes) : null;
        var roles = request.Roles is { Length: > 0 } ? string.Join(",", request.Roles) : null;

        var apiKey = new ApiKey(
            Guid.NewGuid(), tenantId, request.Name, keyHash, keyPrefix,
            userId, scopes, roles, request.ExpiresAt);

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("API key created: {Name} by user {UserId}", request.Name, userId);
        return Ok(new
        {
            apiKey.Id,
            apiKey.Name,
            Key = rawKey,
            apiKey.KeyPrefix,
            apiKey.Scopes,
            apiKey.Roles,
            apiKey.ExpiresAt,
            Message = "Store this key securely — it cannot be retrieved again."
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApiKeyRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        var tenantId = GetTenantId();
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted && k.TenantId == tenantId, ct);
        if (apiKey is null) return NotFound();

        var scopes = request.Scopes is { Length: > 0 } ? string.Join(",", request.Scopes) : null;
        var roles = request.Roles is { Length: > 0 } ? string.Join(",", request.Roles) : null;

        apiKey.Update(request.Name, scopes, roles, request.ExpiresAt);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("API key updated: {Id} by user {UserId}", id, GetUserId());
        return Ok(new
        {
            apiKey.Id,
            apiKey.Name,
            apiKey.KeyPrefix,
            apiKey.Scopes,
            apiKey.Roles,
            apiKey.IsActive,
            apiKey.ExpiresAt
        });
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted && k.TenantId == tenantId, ct);
        if (apiKey is null) return NotFound();

        if (apiKey.IsActive)
            apiKey.Revoke();
        else
            apiKey.Activate();

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("API key {Action}: {Id}", apiKey.IsActive ? "activated" : "deactivated", id);
        return Ok(new { apiKey.Id, apiKey.IsActive });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted && k.TenantId == tenantId, ct);
        if (apiKey is null) return NotFound();

        apiKey.SoftDelete();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("API key deleted: {Id}", id);
        return NoContent();
    }

    [HttpPost("{id:guid}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted && k.TenantId == tenantId, ct);
        if (apiKey is null) return NotFound();

        var rawKey = $"r2w_{GenerateRandomKey(32)}";
        var keyHash = HashKey(rawKey);
        var keyPrefix = rawKey[..8];

        apiKey.Regenerate(keyHash, keyPrefix);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("API key regenerated: {Id}", id);
        return Ok(new
        {
            apiKey.Id,
            Key = rawKey,
            apiKey.KeyPrefix,
            Message = "Store this key securely — it cannot be retrieved again."
        });
    }

    private static string GenerateRandomKey(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length + 8);
        var encoded = Convert.ToBase64String(bytes)
            .Replace("+", "").Replace("/", "").Replace("=", "");
        return encoded[..Math.Min(length, encoded.Length)];
    }

    private static string HashKey(string key)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hash);
    }

    public record CreateApiKeyRequest(string Name, string[]? Scopes, string[]? Roles, DateTime? ExpiresAt);
    public record UpdateApiKeyRequest(string Name, string[]? Scopes, string[]? Roles, DateTime? ExpiresAt);
}
