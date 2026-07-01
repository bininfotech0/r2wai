using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using R2WAI.Web.Services;

namespace R2WAI.Web.Authentication;

public class JwtAuthenticationStateProvider(TokenStorageService tokenStorage, CircuitTokenProvider tokenProvider)
    : AuthenticationStateProvider
{
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await tokenStorage.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                tokenProvider.Token = token;
                tokenProvider.RefreshToken = await tokenStorage.GetRefreshTokenAsync();
                var user = CreateClaimsPrincipalFromToken(token);
                _currentUser = user;
                return new AuthenticationState(user);
            }
        }
        catch (InvalidOperationException)
        {
            if (!string.IsNullOrEmpty(tokenProvider.Token))
            {
                var user = CreateClaimsPrincipalFromToken(tokenProvider.Token);
                _currentUser = user;
                return new AuthenticationState(user);
            }
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public async Task LoginAsync(string token, string refreshToken, string userInfoJson)
    {
        tokenProvider.Token = token;
        tokenProvider.RefreshToken = refreshToken;
        await tokenStorage.SetTokenAsync(token);
        await tokenStorage.SetRefreshTokenAsync(refreshToken);
        await tokenStorage.SetUserAsync(userInfoJson);

        var user = CreateClaimsPrincipalFromToken(token);
        _currentUser = user;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public void UpdateTokens(string token, string refreshToken)
    {
        tokenProvider.Token = token;
        tokenProvider.RefreshToken = refreshToken;

        var user = CreateClaimsPrincipalFromToken(token);
        _currentUser = user;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public async Task LogoutAsync()
    {
        tokenProvider.Token = null;
        tokenProvider.RefreshToken = null;
        await tokenStorage.ClearAllAsync();
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public Task<string?> GetTokenAsync()
    {
        return Task.FromResult(tokenProvider.Token);
    }

    private static ClaimsPrincipal CreateClaimsPrincipalFromToken(string token)
    {
        try
        {
            var claims = ParseClaimsFromJwt(token);
            if (claims == null)
                return new ClaimsPrincipal(new ClaimsIdentity());

            var identity = new ClaimsIdentity(claims, "jwt");
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    private static IEnumerable<Claim>? ParseClaimsFromJwt(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        try
        {
            var payload = parts[1];
            var jsonBytes = Convert.FromBase64String(payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '='));
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            if (keyValuePairs == null)
                return null;

            if (keyValuePairs.TryGetValue("exp", out var expVal))
            {
                if (expVal is JsonElement expEl && expEl.TryGetInt64(out var expSeconds))
                {
                    var expiry = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                    if (expiry < DateTimeOffset.UtcNow)
                        return null;
                }
            }

            var claims = new List<Claim>();

            if (keyValuePairs.TryGetValue("nameid", out var nameId))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, nameId.ToString()!));

            if (keyValuePairs.TryGetValue("email", out var email))
                claims.Add(new Claim(ClaimTypes.Email, email.ToString()!));

            if (keyValuePairs.TryGetValue("tenant_id", out var tenantId))
                claims.Add(new Claim("tenant_id", tenantId.ToString()!));

            if (keyValuePairs.TryGetValue("unique_name", out var uniqueName))
                claims.Add(new Claim(ClaimTypes.Name, uniqueName.ToString()!));

            if (keyValuePairs.TryGetValue("role", out var role))
            {
                if (role is JsonElement { ValueKind: JsonValueKind.Array } arr)
                {
                    foreach (var item in arr.EnumerateArray())
                        claims.Add(new Claim(ClaimTypes.Role, item.GetString()!));
                }
                else
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.ToString()!));
                }
            }

            return claims;
        }
        catch
        {
            return null;
        }
    }
}
