using Microsoft.Identity.Web;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace R2WAI.Infrastructure.Authentication;

public class EntraIdAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EntraIdAuthService> _logger;

    public EntraIdAuthService(IConfiguration configuration, ILogger<EntraIdAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> ValidateEntraIdTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var tenantId = _configuration["Authentication:EntraId:TenantId"];
            var clientId = _configuration["Authentication:EntraId:ClientId"];

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
            {
                _logger.LogWarning("Entra ID not configured");
                return false;
            }

            var handler = new JwtSecurityTokenHandler();
            var result = await handler.ValidateTokenAsync(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidIssuer = $"https://login.microsoftonline.com/{tenantId}/v2.0",
                ValidAudience = clientId,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                IssuerSigningKeyResolver = (tokenStr, securityToken, kid, validationParameters) =>
                {
                    var config = Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever
                        .GetAsync(
                            $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration",
                            CancellationToken.None)
                        .GetAwaiter().GetResult();
                    return config.SigningKeys;
                }
            });

            return result.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Entra ID token validation failed");
            return false;
        }
    }

    public Task<User?> GetUserFromEntraIdAsync(string token, CancellationToken ct = default)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

            if (jwtToken is null)
                return Task.FromResult<User?>(null);

            var externalId = jwtToken.Subject ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "oid")?.Value;
            var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "preferred_username" || c.Type == "email")?.Value;
            var firstName = jwtToken.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value ?? "User";
            var lastName = jwtToken.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value ?? "User";

            if (string.IsNullOrEmpty(externalId) || string.IsNullOrEmpty(email))
                return Task.FromResult<User?>(null);

            return Task.FromResult<User?>(new User(
                Guid.NewGuid(), Guid.Empty, externalId, email, firstName, lastName));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract user from Entra ID token");
            return Task.FromResult<User?>(null);
        }
    }

    public Task<User> MapEntraIdUserToLocalUser(User entraIdUser, Guid tenantId)
    {
        return Task.FromResult(new User(
            Guid.NewGuid(), tenantId, entraIdUser.ExternalId,
            entraIdUser.Email, entraIdUser.FirstName, entraIdUser.LastName));
    }
}
