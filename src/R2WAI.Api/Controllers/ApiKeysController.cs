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
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = dbContext.ApiKeys
            .Where(k => !k.IsDeleted)
            .OrderByDescending(k => k.CreatedAt);

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
                k.CreatedAt
            }).ToListAsync(ct);

        return Ok(new { items, total, page, pageSize });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request, CancellationToken ct = default)
    {
        var tenantClaim = User.FindFirst("tenant_id");
        var tenantId = tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tid) ? tid : Guid.Empty;

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        var userId = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uid) ? uid : Guid.Empty;

        var rawKey = $"r2w_{GenerateRandomKey(32)}";
        var keyHash = HashKey(rawKey);
        var keyPrefix = rawKey[..8];

        var scopes = request.Scopes != null ? string.Join(",", request.Scopes) : null;
        var roles = request.Roles != null ? string.Join(",", request.Roles) : null;

        var apiKey = new ApiKey(
            Guid.NewGuid(), tenantId, request.Name, keyHash, keyPrefix,
            userId, scopes, roles, request.ExpiresAt);

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("API key created: {Name} by user {UserId}", request.Name, userId);
        return Ok(new { apiKey.Id, apiKey.Name, key = rawKey, apiKey.KeyPrefix, message = "Store this key securely — it cannot be retrieved again." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct = default)
    {
        var apiKey = await dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted, ct);
        if (apiKey is null) return NotFound();

        apiKey.Revoke();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("API key revoked: {Id}", id);
        return NoContent();
    }

    [HttpPost("{id:guid}/regenerate")]
    public async Task<IActionResult> Regenerate(Guid id, CancellationToken ct = default)
    {
        var apiKey = await dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted, ct);
        if (apiKey is null) return NotFound();

        var rawKey = $"r2w_{GenerateRandomKey(32)}";
        var keyHash = HashKey(rawKey);
        var keyPrefix = rawKey[..8];

        apiKey.Regenerate(keyHash, keyPrefix);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("API key regenerated: {Id}", id);
        return Ok(new { apiKey.Id, key = rawKey, apiKey.KeyPrefix, message = "Store this key securely — it cannot be retrieved again." });
    }

    private static string GenerateRandomKey(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..length];
    }

    private static string HashKey(string key)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hash);
    }

    public record CreateApiKeyRequest(string Name, string[]? Scopes, string[]? Roles, DateTime? ExpiresAt);
}
