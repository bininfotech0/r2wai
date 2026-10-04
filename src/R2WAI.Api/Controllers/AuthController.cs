using System.Security.Claims;
using System.Security.Cryptography;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Security;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Authentication;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(
    JwtService jwtService,
    EntraIdAuthService entraIdAuthService,
    TotpService totpService,
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IStorageService storageService,
    IMediator mediator,
    ICacheService cacheService,
    IAuthPolicyService authPolicyService,
    ILogger<AuthController> logger) : ControllerBase
{
    private static readonly Dictionary<string, string> AvatarContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
    };
    private static readonly HashSet<string> AllowedAvatarContentTypes =
        new(AvatarContentTypes.Values, StringComparer.OrdinalIgnoreCase);
    private const long MaxAvatarSize = 2 * 1024 * 1024;

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    // Long relative to LockoutDuration: this is an outer bound on the cache entry itself (so it
    // doesn't live in Redis forever), not the lockout window — FailedAttempts is meant to persist
    // until a real successful login resets it, same as the tracker it replaces intended.
    private static readonly TimeSpan LoginAttemptStateTtl = TimeSpan.FromHours(24);

    // Was a static ConcurrentDictionary — per-process only, so a multi-instance deployment let an
    // attacker bypass the lockout by hitting a different instance, and any restart/redeploy
    // silently reset every account's failed-attempt count. Same distribution problem
    // RateLimitingMiddleware already solves via ICacheService; this now uses the same fix.
    // LoginAttemptState/LoginLockoutCache.Key live in Services/LoginLockoutCache.cs so
    // AdminController's unlock action can clear exactly this same cache entry.

    public record LoginRequest(string Email, string Password, string? MfaCode = null);

    public record LoginResponse(string Token, string RefreshToken, DateTime ExpiresAt, UserInfo User);

    public record UserInfo(
        Guid Id,
        string? Email,
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
        DateTime? UpdatedAt,
        string? MobileNumber,
        bool HasAadhaar);

    public record RefreshRequest(string AccessToken, string RefreshToken);

    // MfaCode is optional on the wire (an SSO client with no local MFA enrolled never sends it) —
    // ExchangeEntraIdToken enforces the same per-account MfaEnabled requirement the password Login
    // path already does, using this field, not a bypass of it.
    public record EntraIdRequest(string IdToken, string? MfaCode = null);

    public record UpdateProfileRequest(string FirstName, string LastName, string? MobileNumber = null, string? Email = null);

    public record ForgotPasswordRequest(string Email);

    public record ResetPasswordRequest(string Email, string Token, string NewPassword);

    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    public record RequestAccessRequest(
        string FullName,
        string Email,
        string Organization,
        string? Department = null,
        string? Reason = null);

    public record RegisterMemberRequest(string FullName, string AadhaarNumber, string MobileNumber, string Password, string? Email);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        logger.LogInformation("Login attempt for: {Email}", request.Email);

        var normalizedEmail = request.Email.ToLowerInvariant();
        var lockoutKey = Services.LoginLockoutCache.Key(normalizedEmail);
        var state = await cacheService.GetAsync<Services.LoginAttemptState>(lockoutKey, ct) ?? new Services.LoginAttemptState();

        if (state.LockedUntil.HasValue && state.LockedUntil.Value > DateTime.UtcNow)
        {
            var remaining = (int)(state.LockedUntil.Value - DateTime.UtcNow).TotalSeconds;
            logger.LogWarning("Login blocked for locked account: {Email}", request.Email);
            return StatusCode(429, new { error = $"Account temporarily locked. Try again in {remaining} seconds.", retryAfter = remaining });
        }

        // A 12-digit numeric input is never a valid email — treat it as an Aadhaar-based member
        // login instead. Hashed the same deterministic way RegisterMemberCommandHandler stores it.
        var isAadhaarLogin = System.Text.RegularExpressions.Regex.IsMatch(request.Email, @"^\d{12}$");

        var usersQuery = dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.Tenant)
            .Where(u => !u.IsDeleted);

        User? user;
        if (isAadhaarLogin)
        {
            var aadhaarHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Email)));
            user = await usersQuery.FirstOrDefaultAsync(u => u.AadhaarNumberHash == aadhaarHash, ct);
        }
        else
        {
            user = await usersQuery.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        }

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) ||
            !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            state.FailedAttempts++;
            if (state.FailedAttempts >= MaxFailedAttempts)
            {
                state.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                logger.LogWarning("Account locked after {Attempts} failed attempts: {Email}", state.FailedAttempts, request.Email);
            }
            await cacheService.SetAsync(lockoutKey, state, LoginAttemptStateTtl, ct);
            logger.LogWarning("Failed login attempt for: {Email} (attempt {Attempt})", request.Email, state.FailedAttempts);
            return Unauthorized(new { error = isAadhaarLogin ? "Invalid Aadhaar number or password" : "Invalid email or password" });
        }

        // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.4 #67 — TenantStatus existed on the entity but was
        // never checked anywhere in the codebase (confirmed by grep before adding this): a platform
        // admin "suspending" a tenant had zero actual effect on whether its users could still log in.
        // Checked here, before MFA/password-expiry, and deliberately does NOT touch the lockout
        // counter above — credentials were already verified correct, this isn't a guessing attempt.
        if (user.Tenant is not null && user.Tenant.Status != Domain.Enums.TenantStatus.Active)
        {
            logger.LogWarning("Login blocked for non-active tenant ({Status}): {Email}", user.Tenant.Status, request.Email);
            return StatusCode(403, new { error = "This organisation's account is not active. Contact your administrator or R2WAI support." });
        }

        if (user.MfaEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.MfaCode))
                return Unauthorized(new { error = "MFA code required", mfaRequired = true });

            if (!totpService.ValidateCode(user.MfaSecret!, request.MfaCode))
                return Unauthorized(new { error = "Invalid MFA code", mfaRequired = true });
        }
        else if (await authPolicyService.IsMfaRequiredAsync(user.TenantId, ct))
        {
            // "Auth" GlobalPolicy opt-in ({"requireMfa":true}) — additive tightening only, same
            // convention as the other GlobalPolicy types: a tenant with no policy set is unaffected,
            // credentials were already verified above, but the user can't finish signing in until
            // they enroll MFA. Distinct from mfaRequired (which asks for a code the user already has).
            // Issues a restricted token (claim-gated by MfaSetupScopeMiddleware to mfa/* + logout
            // only) rather than a normal session, so the user can call mfa/setup + mfa/enable
            // without getting a full session ahead of actually enrolling.
            logger.LogWarning("Login blocked pending MFA enrollment (tenant policy): {Email}", request.Email);
            var (setupToken, _) = await jwtService.GenerateTokenAsync(
                user.Id, user.TenantId, user.Email ?? string.Empty, [],
                new Dictionary<string, string> { ["mfa_setup_pending"] = "true" }, ct);
            return StatusCode(403, new { error = "Your organization requires multi-factor authentication. Set up MFA to continue.", mfaSetupRequired = true, setupToken });
        }

        // Same additive-tightening "Auth" GlobalPolicy, independent optional field
        // ({"maxPasswordAgeDays":N}) — checked only once MFA (if required) is already satisfied,
        // so a user needing both is prompted for one thing at a time rather than simultaneously.
        // PasswordChangedAt is null only for rows this migration's backfill couldn't reach (no
        // PasswordHash at all, e.g. a not-yet-activated account) — treated as "not overdue" rather
        // than blocking someone who was never given a password to begin with.
        var maxPasswordAgeDays = await authPolicyService.GetMaxPasswordAgeDaysAsync(user.TenantId, ct);
        if (maxPasswordAgeDays.HasValue && user.PasswordChangedAt.HasValue
            && (DateTime.UtcNow - user.PasswordChangedAt.Value).TotalDays > maxPasswordAgeDays.Value)
        {
            logger.LogWarning("Login blocked pending password change (tenant policy, age > {MaxDays}d): {Email}", maxPasswordAgeDays.Value, request.Email);
            var (setupToken, _) = await jwtService.GenerateTokenAsync(
                user.Id, user.TenantId, user.Email ?? string.Empty, [],
                new Dictionary<string, string> { ["password_change_pending"] = "true" }, ct);
            return StatusCode(403, new { error = "Your password has expired. Set a new password to continue.", passwordChangeRequired = true, setupToken });
        }

        await cacheService.RemoveAsync(lockoutKey, ct);

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
            user.Id, user.TenantId, user.Email ?? string.Empty, roles, nameClaims, ct);

        var userInfo = BuildUserInfo(user, roles);
        var response = new LoginResponse(token, refreshToken, expiresAt, userInfo);

        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var principal = jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            return Unauthorized(new { error = "Invalid access token" });

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid token claims" });

        // IgnoreQueryFilters: [AllowAnonymous] by design — the caller's access token is expired, so
        // there is no ambient authenticated tenant to filter by (P0-5's fail-closed tenant filter
        // would otherwise find nothing here regardless of who's asking). The real security boundary
        // is the fixed-time refresh-token-hash comparison below, not the ambient tenant filter.
        var user = await dbContext.Users.IgnoreQueryFilters()
            .Where(u => !u.IsDeleted)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Unauthorized(new { error = "User not found" });

        // Same tenant-status enforcement as Login — a tenant suspended after a user's last login
        // must not let them silently keep renewing a session via refresh.
        if (user.Tenant is not null && user.Tenant.Status != Domain.Enums.TenantStatus.Active)
        {
            user.RevokeRefreshToken();
            await dbContext.SaveChangesAsync(ct);
            logger.LogWarning("Refresh denied for non-active tenant ({Status}): user {UserId}", user.Tenant.Status, userId);
            return Unauthorized(new { error = "This organisation's account is not active." });
        }

        var incomingHash = jwtService.HashRefreshToken(request.RefreshToken);
        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(user.RefreshTokenHash ?? ""),
            System.Text.Encoding.UTF8.GetBytes(incomingHash)))
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

        if (request.MobileNumber is not null)
        {
            var mobile = request.MobileNumber.Trim();
            if (mobile.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(mobile, @"^(\+91)?[6-9]\d{9}$"))
                return BadRequest(new { error = "Please enter a valid Indian mobile number." });

            user.SetMobileNumber(mobile.Length == 0 ? null : mobile);
        }

        // Email can only be added when the account doesn't already have one (e.g. a member who
        // signed up with just Aadhaar) — changing an existing email is a bigger, riskier change
        // (it's also the login identifier for non-member accounts) and isn't handled here.
        if (!string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrEmpty(user.Email))
        {
            var email = request.Email.Trim();
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
                return BadRequest(new { error = "Please enter a valid email address." });

            var emailTaken = await dbContext.Users.IgnoreQueryFilters()
                .Where(u => !u.IsDeleted && u.Id != userId)
                .AnyAsync(u => u.Email == email, ct);
            if (emailTaken)
                return Conflict(new { error = "A user with this email already exists." });

            user.SetEmail(email);
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Profile updated for user {UserId}", userId);
        return Ok(new { message = "Profile updated successfully" });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
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
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await dbContext.Users.IgnoreQueryFilters().Where(u => !u.IsDeleted).FirstOrDefaultAsync(u => u.Email == request.Email, ct);
        if (user is null)
            return BadRequest(new { error = "Invalid reset request." });

        var incomingTokenHash = jwtService.HashRefreshToken(request.Token);
        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(user.PasswordResetToken ?? ""),
            System.Text.Encoding.UTF8.GetBytes(incomingTokenHash)) || user.PasswordResetExpiresAt < DateTime.UtcNow)
            return BadRequest(new { error = "Invalid or expired reset token." });

        if (!PasswordPolicy.IsValid(request.NewPassword, out var passwordError))
            return BadRequest(new { error = passwordError });

        user.SetPasswordHash(passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordResetToken();
        user.RevokeRefreshToken();
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Password reset completed for {Email}", request.Email);
        return Ok(new { message = "Password has been reset successfully." });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await dbContext.Users.FindAsync([userId], ct);
        if (user is null)
            return NotFound(new { error = "User not found" });

        if (string.IsNullOrEmpty(user.PasswordHash) || !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { error = "Current password is incorrect." });

        if (!PasswordPolicy.IsValid(request.NewPassword, out var passwordError))
            return BadRequest(new { error = passwordError });

        user.SetPasswordHash(passwordHasher.Hash(request.NewPassword));
        user.RevokeRefreshToken();
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Password changed for user {UserId}", userId);
        return Ok(new { message = "Password changed successfully." });
    }

    [HttpPost("profile/avatar")]
    [Authorize]
    [RequestSizeLimit(MaxAvatarSize)]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        if (file.Length > MaxAvatarSize)
            return BadRequest(new { error = $"File size exceeds the {MaxAvatarSize / (1024 * 1024)} MB limit." });

        if (!AllowedAvatarContentTypes.Contains(file.ContentType))
            return BadRequest(new { error = $"File type '{file.ContentType}' is not supported. Use PNG, JPEG, WebP, or GIF." });

        var user = await dbContext.Users.FindAsync([userId], ct);
        if (user is null)
            return NotFound(new { error = "User not found" });

        var extension = AvatarContentTypes.First(kv => kv.Value.Equals(file.ContentType, StringComparison.OrdinalIgnoreCase)).Key;
        await using var stream = file.OpenReadStream();
        var storagePath = await storageService.UploadFileAsync(
            stream, $"avatar{extension}", file.ContentType, $"avatars/{userId}", ct);

        var previousPath = user.AvatarStoragePath;
        user.SetAvatar($"/api/v1/auth/profile/avatar/{userId}", storagePath);
        await dbContext.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previousPath) && previousPath != storagePath)
        {
            try { await storageService.DeleteFileAsync(previousPath, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to delete previous avatar {Path}", previousPath); }
        }

        logger.LogInformation("Avatar updated for user {UserId}", userId);
        return Ok(new { avatarUrl = user.AvatarUrl });
    }

    // [Authorize]-only (not [AllowAnonymous]): avatars are only ever rendered inside the
    // authenticated app UI (profile page, member/user lists), so this stays behind auth like
    // every other tenant-scoped resource rather than becoming a public image host.
    [HttpGet("profile/avatar/{userId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetAvatar(Guid userId, CancellationToken ct)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || string.IsNullOrEmpty(user.AvatarStoragePath))
            return NotFound();

        var extension = Path.GetExtension(user.AvatarStoragePath);
        var contentType = AvatarContentTypes.GetValueOrDefault(extension, "application/octet-stream");

        try
        {
            var stream = await storageService.DownloadFileAsync(user.AvatarStoragePath, ct);
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("request-access")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestAccess([FromBody] RequestAccessRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Organization))
            return BadRequest(new { error = "Full name, email, and organization are required." });

        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email))
            return BadRequest(new { error = "Please provide a valid email address." });

        var accessRequest = new AccessRequest(
            Guid.NewGuid(), request.FullName.Trim(), request.Email.Trim(),
            request.Organization.Trim(), request.Department?.Trim(), request.Reason?.Trim());

        await dbContext.AccessRequests.AddAsync(accessRequest, ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Access request submitted for {Email}", request.Email);
        return StatusCode(201, new { message = "Your request has been submitted for review." });
    }

    // Was anonymous: anyone on the network could create a login-capable account in the default tenant
    // (collecting an Aadhaar number) — see docs/audit/R2WAI-IQ200-AUDIT-2026-09-20.md, finding 7.
    // Member creation is now an administrator action.
    [HttpPost("register-member")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> RegisterMember([FromBody] RegisterMemberRequest request, CancellationToken ct)
    {
        var member = await mediator.Send(new R2WAI.Application.Features.Auth.Commands.RegisterMemberCommand
        {
            FullName = request.FullName,
            AadhaarNumber = request.AadhaarNumber,
            MobileNumber = request.MobileNumber,
            Password = request.Password,
            Email = request.Email,
        }, ct);

        var refreshToken = jwtService.GenerateRefreshToken();
        var refreshTokenHash = jwtService.HashRefreshToken(refreshToken);
        var refreshExpiry = DateTime.UtcNow.AddDays(jwtService.GetRefreshTokenExpirationDays());

        var user = await dbContext.Users.FindAsync([member.Id], ct)
            ?? throw new InvalidOperationException("Member was created but could not be reloaded.");
        user.SetRefreshToken(refreshTokenHash, refreshExpiry);
        user.SetLastLogin();
        await dbContext.SaveChangesAsync(ct);

        var (token, expiresAt) = await jwtService.GenerateTokenAsync(
            member.Id, member.TenantId, member.Email ?? string.Empty, [], null, ct);

        var userInfo = BuildUserInfo(user, []);
        logger.LogInformation("Member registered and signed in: {UserId}", member.Id);
        return StatusCode(201, new LoginResponse(token, refreshToken, expiresAt, userInfo));
    }

    [HttpPost("mfa/setup")]
    [Authorize]
    public async Task<IActionResult> SetupMfa(CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await dbContext.Users.FindAsync([userId], ct);
        if (user is null) return NotFound();

        if (user.MfaEnabled)
            return BadRequest(new { error = "MFA is already enabled." });

        var secret = totpService.GenerateSecret();
        var setupUri = totpService.GenerateSetupUri(secret, user.Email);

        return Ok(new { secret, setupUri });
    }

    public record MfaVerifyRequest(string Secret, string Code);

    [HttpPost("mfa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableMfa([FromBody] MfaVerifyRequest request, CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await dbContext.Users.FindAsync([userId], ct);
        if (user is null) return NotFound();

        if (!totpService.ValidateCode(request.Secret, request.Code))
            return BadRequest(new { error = "Invalid verification code. Please try again." });

        user.EnableMfa(request.Secret);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("MFA enabled for user {UserId}", userId);
        return Ok(new { message = "MFA enabled successfully." });
    }

    public record MfaDisableRequest(string Code);

    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableMfa([FromBody] MfaDisableRequest request, CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await dbContext.Users.FindAsync([userId], ct);
        if (user is null) return NotFound();

        if (!user.MfaEnabled)
            return BadRequest(new { error = "MFA is not enabled." });

        if (!totpService.ValidateCode(user.MfaSecret!, request.Code))
            return BadRequest(new { error = "Invalid verification code." });

        user.DisableMfa();
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("MFA disabled for user {UserId}", userId);
        return Ok(new { message = "MFA disabled successfully." });
    }

    [HttpGet("mfa/status")]
    [Authorize]
    public async Task<IActionResult> GetMfaStatus(CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await dbContext.Users.FindAsync([userId], ct);
        if (user is null) return NotFound();

        return Ok(new { mfaEnabled = user.MfaEnabled });
    }

    [HttpPost("entra-id")]
    [AllowAnonymous]
    public async Task<IActionResult> ExchangeEntraIdToken([FromBody] EntraIdRequest request, CancellationToken ct)
    {
        logger.LogInformation("Entra ID token exchange requested");

        var isValid = await entraIdAuthService.ValidateEntraIdTokenAsync(request.IdToken, ct);
        if (!isValid)
            return Unauthorized(new { error = "Invalid Entra ID token" });

        var entraUser = await entraIdAuthService.GetUserFromEntraIdAsync(request.IdToken, ct);
        if (entraUser is null)
            return Unauthorized(new { error = "Could not extract user from Entra ID token" });

        // IgnoreQueryFilters: [AllowAnonymous] by design — the caller has just proven their identity to
        // Entra ID, not to R2WAI, so there is no ambient authenticated tenant to filter by yet (same
        // reasoning as Refresh above). The real security boundary is the validated Entra ID token
        // above, not the ambient tenant filter.
        var user = await dbContext.Users.IgnoreQueryFilters()
            .Where(u => !u.IsDeleted)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.Tenant)
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

            // Same tenant-status enforcement as the password Login path — SSO doesn't bypass tenant
            // suspension either, same reasoning as the MFA floor just below.
            if (user.Tenant is not null && user.Tenant.Status != Domain.Enums.TenantStatus.Active)
            {
                logger.LogWarning("Entra ID login blocked for non-active tenant ({Status}): {Email}", user.Tenant.Status, user.Email);
                return StatusCode(403, new { error = "This organisation's account is not active. Contact your administrator or R2WAI support." });
            }

            // Same per-account MFA enforcement as the password Login path — SSO used to skip this
            // branch entirely (only the tenant-wide enrollment floor below existed here), so a user
            // who had personally enabled TOTP MFA got that control silently bypassed the moment they
            // authenticated via Entra instead of a password. A valid Entra id_token is proof of
            // identity to Entra, not to R2WAI's own second factor — enforced here for the same
            // reason it's enforced on the password path.
            if (user.MfaEnabled)
            {
                if (string.IsNullOrWhiteSpace(request.MfaCode))
                    return Unauthorized(new { error = "MFA code required", mfaRequired = true });

                if (!totpService.ValidateCode(user.MfaSecret!, request.MfaCode))
                    return Unauthorized(new { error = "Invalid MFA code", mfaRequired = true });
            }
            else if (await authPolicyService.IsMfaRequiredAsync(tenantId, ct))
            {
                // Same tenant-wide MFA floor as the password login path — SSO doesn't bypass it,
                // since Entra ID's own conditional-access posture isn't visible to R2WAI.
                logger.LogWarning("Entra ID login blocked pending MFA enrollment (tenant policy): {Email}", user.Email);
                var (entraSetupToken, _) = await jwtService.GenerateTokenAsync(
                    user.Id, tenantId, user.Email ?? string.Empty, [],
                    new Dictionary<string, string> { ["mfa_setup_pending"] = "true" }, ct);
                return StatusCode(403, new { error = "Your organization requires multi-factor authentication. Set up MFA to continue.", mfaSetupRequired = true, setupToken = entraSetupToken });
            }

            var refreshToken = jwtService.GenerateRefreshToken();
            var refreshTokenHash = jwtService.HashRefreshToken(refreshToken);
            var refreshExpiry = DateTime.UtcNow.AddDays(jwtService.GetRefreshTokenExpirationDays());
            user.SetRefreshToken(refreshTokenHash, refreshExpiry);
            user.SetLastLogin();
            await dbContext.SaveChangesAsync(ct);

            userInfo = BuildUserInfo(user, roles);

            var (token, expiresAt) = await jwtService.GenerateTokenAsync(
                user.Id, tenantId, user.Email ?? string.Empty, roles, null, ct);

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
            user.ModifiedAt,
            user.MobileNumber,
            !string.IsNullOrEmpty(user.AadhaarNumberHash));
    }
}
