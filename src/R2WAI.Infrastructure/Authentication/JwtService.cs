using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace R2WAI.Infrastructure.Authentication;

public class JwtService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtService> _logger;

    public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<(string Token, DateTime ExpiresAt)> GenerateTokenAsync(
        Guid userId, Guid tenantId, string email, string[] roles, Dictionary<string, string>? extraClaims = null, CancellationToken ct = default)
    {
        var secretKey = GetSecretKey();
        var issuer = _configuration["Authentication:Jwt:Issuer"] ?? "R2WAI";
        var audience = _configuration["Authentication:Jwt:Audience"] ?? "R2WAI-API";
        var expirationMinutes = int.Parse(_configuration["Authentication:Jwt:ExpirationMinutes"] ?? "15");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new("tenant_id", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        if (extraClaims is not null)
        {
            foreach (var claim in extraClaims)
                claims.Add(new Claim(claim.Key, claim.Value));
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Task.FromResult((tokenString, expiresAt));
    }

    public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var secretKey = GetSecretKey();
            var issuer = _configuration["Authentication:Jwt:Issuer"] ?? "R2WAI";
            var audience = _configuration["Authentication:Jwt:Audience"] ?? "R2WAI-API";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var handler = new JwtSecurityTokenHandler();

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return Task.FromResult<ClaimsPrincipal?>(principal);
        }
        catch (SecurityTokenExpiredException)
        {
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var secretKey = GetSecretKey();
        var issuer = _configuration["Authentication:Jwt:Issuer"] ?? "R2WAI";
        var audience = _configuration["Authentication:Jwt:Audience"] ?? "R2WAI-API";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = false,
                ClockSkew = TimeSpan.Zero
            }, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract principal from expired token");
            return null;
        }
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }

    public int GetRefreshTokenExpirationDays()
    {
        return int.Parse(_configuration["Authentication:Jwt:RefreshTokenExpirationDays"] ?? "7");
    }

    private string GetSecretKey()
    {
        return Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? _configuration["Authentication:Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured. Set the JWT_SECRET environment variable.");
    }
}
