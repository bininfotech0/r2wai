using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Authentication;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(
    JwtService jwtService,
    EntraIdAuthService entraIdAuthService,
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    ILogger<AuthController> logger) : ControllerBase
{
    public record LoginRequest(string Email, string Password);

    public record LoginResponse(string Token, string RefreshToken, DateTime ExpiresAt, UserInfo User);

    public record UserInfo(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        string DisplayName,
        string? AvatarUrl,
        string Role,
        string[] Roles,
        Guid TenantId,
        bool IsActive,
        DateTime? LastLoginAt,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    public record RefreshRequest(string AccessToken, string RefreshToken);

    public record EntraIdRequest(string IdToken);

    public record UpdateProfileRequest(string FirstName, string LastName);

    public record ForgotPasswordRequest(string Email);

    public record ResetPasswordRequest(string Email, string Token, string NewPassword);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        logger.LogInformation("Login attempt for: {Email}", request.Email);

        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => !u.IsDeleted)
            .FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) ||
            !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for: {Email}", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        var roles = user.UserRoles?
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Name)
            .ToArray() ?? [];

        user.SetLastLogin();

        var refreshToken = jwtService.GenerateRefreshToken();
        var refreshTokenHash = jwtService.HashRefreshToken(refreshToken);
        var refreshExpiry = DateTime.UtcNow.AddDays(jwtService.GetRefreshTokenExpirationDays());
        user.SetRefreshToken(refreshTokenHash, refreshExpiry);

        await dbContext.SaveChangesAsync(ct);

        var nameClaims = new Dictionary<string, string>
        {
            [ClaimTypes.GivenName] = user.FirstName,
            [ClaimTypes.Surname] = user.LastName
        };

        var (token, expiresAt) = await jwtService.GenerateTokenAsync(
            user.Id, user.TenantId, user.Email, roles, nameClaims, ct);

        var userInfo = BuildUserInfo(user, roles);
        var response = new LoginResponse(token, refreshToken, expiresAt, userInfo);

        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var principal = jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            return Unauthorized(new { error = "Invalid access token" });

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid token claims" });

        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Unauthorized(new { error = "User not found" });

        var incomingHash = jwtService.HashRefreshToken(request.RefreshToken);
        if (user.RefreshTokenHash != incomingHash)
        {
            logger.LogWarning("Refresh token mismatch for user {UserId} — possible token reuse", userId);
            user.RevokeRefreshToken();
            await dbContext.SaveChangesAsync(ct);
            return Unauthorized(new { error = "Invalid refresh token" });
        }

        if (user.RefreshTokenExpiresAt < DateTime.UtcNow)
        {
            user.RevokeRefreshToken();
            await dbContext.SaveChangesAsync(ct);
            return Unauthorized(new { error = "Refresh token expired" });
        }

        var roles = user.UserRoles?
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Name)
            .ToArray() ?? [];

        var newRefreshToken = jwtService.GenerateRefreshToken();
        var newRefreshHash = jwtService.HashRefreshToken(newRefreshToken);
        var refreshExpiry = DateTime.UtcNow.AddDays(jwtService.GetRefreshTokenExpirationDays());
        user.SetRefreshToken(newRefreshHash, refreshExpiry);

        await dbContext.SaveChangesAsync(ct);

        var refreshNameClaims = new Dictionary<string, string>
        {
            [ClaimTypes.GivenName] = user.FirstName,
            [ClaimTypes.Surname] = user.LastName
        };

        var (newAccessToken, expiresAt) = await jwtService.GenerateTokenAsync(
            user.Id, user.TenantId, user.Email, roles, refreshNameClaims, ct);

        return Ok(new { Token = newAccessToken, RefreshToken = newRefreshToken, ExpiresAt = expiresAt });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId))
        {
            var user = await dbContext.Users.FindAsync([userId], ct);
            if (user is not null)
            {
                user.RevokeRefreshToken();
                await dbContext.SaveChangesAsync(ct);
            }
        }

        logger.LogInformation("User logged out");
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Unauthorized();

        var roles = user.UserRoles?
            .Where(ur => ur.Role is not null)
            .Select(ur => ur.Role!.Name)
            .ToArray() ?? [];

        return Ok(BuildUserInfo(user, roles));
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await dbContext.Users.FindAsync([userId], ct);
        if (user is null)
            return NotFound(new { error = "User not found" });

        user.UpdateProfile(request.FirstName, request.LastName, user.AvatarUrl);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Profile updated for user {UserId}", userId);
        return Ok(new { message = "Profile updated successfully" });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await dbContext.Users.IgnoreQueryFilters().Where(u => !u.IsDeleted).FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null)
            return Ok(new { message = "If the email exists, a reset link has been sent." });

        if (user.PasswordResetExpiresAt.HasValue
            && user.PasswordResetExpiresAt.Value > DateTime.UtcNow.AddMinutes(58))
        {
            return Ok(new { message = "If the email exists, a reset link has been sent." });
        }

        var tokenBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var tokenHash = jwtService.HashRefreshToken(token);
        user.SetPasswordResetToken(tokenHash, DateTime.UtcNow.AddHours(1));
        await dbContext.SaveChangesAsync(ct);

        var emailService = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.IEmailService>();
        await emailService.SendPasswordResetAsync(user.Email, user.FirstName, token, ct);

        logger.LogInformation("Password reset requested for {Email}", request.Email);
        return Ok(new { message = "If the email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await dbContext.Users.IgnoreQueryFilters().Where(u => !u.IsDeleted).FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null)
            return BadRequest(new { error = "Invalid reset request." });

        var incomingTokenHash = jwtService.HashRefreshToken(request.Token);
        if (user.PasswordResetToken != incomingTokenHash || user.PasswordResetExpiresAt < DateTime.UtcNow)
            return BadRequest(new { error = "Invalid or expired reset token." });

        if (request.NewPassword.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        user.SetPasswordHash(passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordResetToken();
        user.RevokeRefreshToken();
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Password reset completed for {Email}", request.Email);
        return Ok(new { message = "Password has been reset successfully." });
    }

    [HttpPost("entra-id")]
    public async Task<IActionResult> ExchangeEntraIdToken([FromBody] EntraIdRequest request, CancellationToken ct)
    {
        logger.LogInformation("Entra ID token exchange requested");

        var isValid = await entraIdAuthService.ValidateEntraIdTokenAsync(request.IdToken, ct);
        if (!isValid)
            return Unauthorized(new { error = "Invalid Entra ID token" });

        var entraUser = await entraIdAuthService.GetUserFromEntraIdAsync(request.IdToken, ct);
        if (entraUser is null)
            return Unauthorized(new { error = "Could not extract user from Entra ID token" });

        var user = await dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == entraUser.Email, ct);

        Guid tenantId;
        string[] roles;
        UserInfo userInfo;

        if (user is not null)
        {
            tenantId = user.TenantId;
            roles = user.UserRoles?
                .Where(ur => ur.Role is not null)
                .Select(ur => ur.Role!.Name)
                .ToArray() ?? [];

            var refreshToken = jwtService.GenerateRefreshToken();
            var refreshTokenHash = jwtService.HashRefreshToken(refreshToken);
            var refreshExpiry = DateTime.UtcNow.AddDays(jwtService.GetRefreshTokenExpirationDays());
            user.SetRefreshToken(refreshTokenHash, refreshExpiry);
            user.SetLastLogin();
            await dbContext.SaveChangesAsync(ct);

            userInfo = BuildUserInfo(user, roles);

            var (token, expiresAt) = await jwtService.GenerateTokenAsync(
                user.Id, tenantId, user.Email, roles, null, ct);

            return Ok(new LoginResponse(token, refreshToken, expiresAt, userInfo));
        }

        logger.LogWarning("Entra ID login rejected: no pre-provisioned user for {Email}", entraUser.Email);
        return Unauthorized(new { error = "User account not provisioned. Contact your administrator." });
    }

    private static UserInfo BuildUserInfo(User user, string[] roles)
    {
        return new UserInfo(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.AvatarUrl,
            roles.FirstOrDefault() ?? "User",
            roles,
            user.TenantId,
            user.Status != "Inactive",
            user.LastLoginAt,
            user.CreatedAt,
            user.ModifiedAt);
    }
}
