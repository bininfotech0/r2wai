using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Middleware;

public class ApiKeyAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthenticationMiddleware> logger)
{
    private const string ApiKeyHeaderName = "X-API-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            await next(context);
            return;
        }

        var incomingKey = extractedApiKey.ToString();

        // Static, env-configured keys (e.g. the Docker dev key) are checked first — unchanged
        // from before. This alone used to be the *only* path: the admin UI's "create API key"
        // feature persists real keys to the ApiKeys table, complete with hashing, expiry, and a
        // RecordUsage()/IsExpired domain model clearly built for real auth — but nothing ever
        // checked that table here, so any key created through the admin panel silently never
        // worked. The DB check below is what actually wires that feature up.
        var matched = MatchConfiguredKey(incomingKey);
        if (matched is not null)
        {
            Authenticate(context, matched);
            await next(context);
            return;
        }

        var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
        var incomingHashBase64 = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(incomingKey)));

        // Filter to active, unexpired, non-deleted keys before pulling into memory, but still
        // compare every candidate's hash in constant time rather than short-circuiting on the
        // first match, for the same timing-attack-avoidance reason as the config-key loop above.
        var candidates = await dbContext.ApiKeys
            .IgnoreQueryFilters()
            .Where(k => !k.IsDeleted && k.IsActive && (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow))
            .ToListAsync(context.RequestAborted);

        Domain.Entities.ApiKey? matchedDbKey = null;
        var incomingHashBytes = Encoding.UTF8.GetBytes(incomingHashBase64);
        foreach (var candidate in candidates)
        {
            var storedHashBytes = Encoding.UTF8.GetBytes(candidate.KeyHash);
            if (storedHashBytes.Length == incomingHashBytes.Length
                && CryptographicOperations.FixedTimeEquals(storedHashBytes, incomingHashBytes))
                matchedDbKey = candidate;
        }

        if (matchedDbKey is null)
        {
            logger.LogWarning("Invalid API key attempt from {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        AuthenticateFromDbKey(context, matchedDbKey);
        matchedDbKey.RecordUsage();
        await dbContext.SaveChangesAsync(context.RequestAborted);

        logger.LogInformation("API key authenticated: {Name} from {IP}", matchedDbKey.Name, context.Connection.RemoteIpAddress);
        await next(context);
    }

    private ApiKeyEntry? MatchConfiguredKey(string incomingKey)
    {
        var apiKeys = configuration.GetSection("Authentication:ApiKeys").Get<ApiKeyEntry[]>();
        if (apiKeys is null || apiKeys.Length == 0)
            return null;

        // Hash the incoming key once, then compare against all configured keys using
        // constant-time equality. Always iterate the full list to avoid timing leaks
        // based on which position the matching key is at.
        var incomingHash = SHA256.HashData(Encoding.UTF8.GetBytes(incomingKey));
        ApiKeyEntry? matched = null;
        foreach (var entry in apiKeys)
        {
            if (!entry.Enabled) continue;
            var entryHash = SHA256.HashData(Encoding.UTF8.GetBytes(entry.Key));
            if (CryptographicOperations.FixedTimeEquals(entryHash, incomingHash))
                matched = entry;
        }
        return matched;
    }

    private void Authenticate(HttpContext context, ApiKeyEntry matched)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, matched.UserId ?? Guid.Empty.ToString()),
            new(ClaimTypes.Name, matched.Name ?? "api-key-user"),
            new(ClaimTypes.Email, matched.Name ?? "api@r2wai.local"),
            new("auth_method", "api_key"),
        };

        if (!string.IsNullOrEmpty(matched.TenantId))
            claims.Add(new Claim("tenant_id", matched.TenantId));

        if (matched.Roles is { Length: > 0 })
        {
            foreach (var role in matched.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (matched.Scopes is { Length: > 0 })
        {
            foreach (var scope in matched.Scopes)
                claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, "ApiKey");
        context.User = new ClaimsPrincipal(identity);

        logger.LogInformation("API key authenticated: {Name} from {IP}", matched.Name, context.Connection.RemoteIpAddress);
    }

    private static void AuthenticateFromDbKey(HttpContext context, Domain.Entities.ApiKey matched)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, matched.CreatedByUserId.ToString()),
            new(ClaimTypes.Name, matched.Name),
            new(ClaimTypes.Email, $"{matched.KeyPrefix}@api-key.local"),
            new("auth_method", "api_key"),
            new("tenant_id", matched.TenantId.ToString()),
        };

        if (!string.IsNullOrEmpty(matched.Roles))
        {
            foreach (var role in matched.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (!string.IsNullOrEmpty(matched.Scopes))
        {
            foreach (var scope in matched.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, "ApiKey");
        context.User = new ClaimsPrincipal(identity);
    }

    public class ApiKeyEntry
    {
        public string Key { get; set; } = "";
        public string? Name { get; set; }
        public string? UserId { get; set; }
        public string? TenantId { get; set; }
        public string[]? Roles { get; set; }
        public string[]? Scopes { get; set; }
        public bool Enabled { get; set; } = true;
    }
}
