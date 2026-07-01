using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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

        var apiKeys = configuration.GetSection("Authentication:ApiKeys").Get<ApiKeyEntry[]>();
        if (apiKeys is null || apiKeys.Length == 0)
        {
            await next(context);
            return;
        }

        // Hash the incoming key once, then compare against all configured keys using
        // constant-time equality. Always iterate the full list to avoid timing leaks
        // based on which position the matching key is at.
        var incomingHash = SHA256.HashData(Encoding.UTF8.GetBytes(extractedApiKey.ToString()));
        ApiKeyEntry? matched = null;
        foreach (var entry in apiKeys)
        {
            if (!entry.Enabled) continue;
            var entryHash = SHA256.HashData(Encoding.UTF8.GetBytes(entry.Key));
            if (CryptographicOperations.FixedTimeEquals(entryHash, incomingHash))
                matched = entry;
        }

        if (matched is null)
        {
            logger.LogWarning("Invalid API key attempt from {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

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

        await next(context);
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
