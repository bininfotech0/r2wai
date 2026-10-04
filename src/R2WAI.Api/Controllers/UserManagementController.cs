using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Admin.Commands;
using R2WAI.Application.Features.Admin.Queries;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

/// <summary>
/// User CRUD, split out of AdminController so it can carry its own, narrower authorization: the
/// "CanManageUsers" policy rather than AdminController's blanket Admin-only gate. That policy was
/// defined in Program.cs but never actually applied anywhere — AdminController's class-level
/// [Authorize(Roles="Admin,SystemAdmin")] governed these same routes instead, so a user holding
/// only the (since-retired, see CollapseRbacToThreeRoles) UserManager role — which existed
/// specifically for this — got 403 on every one of them — confirmed live. ASP.NET Core combines
/// class- and method-level [Authorize] attributes with AND, so a method-level policy override on
/// the old controller couldn't have fixed this; moving the actions to their own controller was the
/// actual fix. The policy itself has since collapsed to just Admin/SystemAdmin (UserManager retired).
/// </summary>
[ApiController]
[Authorize(Policy = "CanManageUsers")]
[Route("api/v1/admin")]
public class UserManagementController(
    IMediator mediator,
    ApplicationDbContext dbContext,
    ICacheService cacheService,
    IRepository<AuditLog> auditLogRepo,
    ICurrentUserService currentUser,
    ILogger<UserManagementController> logger) : ControllerBase
{
    private static (int page, int pageSize) ClampPagination(int page, int pageSize, int maxPageSize = 100)
    {
        return (Math.Max(1, page), Math.Clamp(pageSize, 1, maxPageSize));
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        (page, pageSize) = ClampPagination(page, pageSize);
        var query = new GetUsersQuery { Page = page, PageSize = pageSize, Search = search };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetUserByIdQuery { Id = id }, ct);
        return Ok(result);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating user: {Email}", command.Email);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetUsers), new { id = result.Id }, result);
    }

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    public record AssignUserRolesRequest(List<Guid> RoleIds);

    [HttpPut("users/{id:guid}/roles")]
    public async Task<IActionResult> AssignUserRoles(Guid id, [FromBody] AssignUserRolesRequest request, CancellationToken ct = default)
    {
        var command = new AssignUserRolesCommand { UserId = id, RoleIds = request.RoleIds };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.4 #70 — admin-assisted account recovery, real gaps
    // confirmed absent before this: no route anywhere disabled a user's MFA or cleared a lockout.
    [HttpPost("users/{id:guid}/mfa-reset")]
    public async Task<IActionResult> ResetMfa(Guid id, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        if (!user.MfaEnabled)
            return Ok(new { id, mfaEnabled = false, message = "MFA was not enabled for this user." });

        user.DisableMfa();
        await auditLogRepo.AddAsync(new Domain.Entities.AuditLog(
            Guid.NewGuid(), user.TenantId, Domain.Enums.AuditAction.Update, nameof(Domain.Entities.User), user.Id.ToString(),
            userId: currentUser.UserId,
            metadata: System.Text.Json.JsonSerializer.Serialize(new { action = "mfa-reset", targetUserId = user.Id })), ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogWarning("MFA reset for user {UserId} by admin {AdminId}", user.Id, currentUser.UserId);
        return Ok(new
        {
            id,
            mfaEnabled = false,
            message = "MFA has been disabled for this user. If a \"require MFA\" policy is active for this organisation, they will be asked to re-enroll on next login.",
        });
    }

    // Clears exactly the cache entry AuthController.Login writes on a failed attempt — same key
    // format (Services.LoginLockoutCache.Key), so this genuinely undoes the lockout rather than
    // adding a second, disconnected notion of "unlocked".
    [HttpPost("users/{id:guid}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid id, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        if (string.IsNullOrWhiteSpace(user.Email))
            return Ok(new { id, message = "This account has no login email (Aadhaar-based member login is not lockout-gated the same way)." });

        await cacheService.RemoveAsync(Services.LoginLockoutCache.Key(user.Email.ToLowerInvariant()), ct);

        await auditLogRepo.AddAsync(new Domain.Entities.AuditLog(
            Guid.NewGuid(), user.TenantId, Domain.Enums.AuditAction.Update, nameof(Domain.Entities.User), user.Id.ToString(),
            userId: currentUser.UserId,
            metadata: System.Text.Json.JsonSerializer.Serialize(new { action = "unlock", targetUserId = user.Id })), ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogWarning("Login lockout cleared for user {UserId} by admin {AdminId}", user.Id, currentUser.UserId);
        return Ok(new { id, message = "Account unlocked. The failed-login counter has been reset." });
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteUserCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    public record BulkDeleteRequest(Guid[] Ids);

    [HttpPost("users/bulk-delete")]
    public async Task<IActionResult> BulkDeleteUsers([FromBody] BulkDeleteRequest request, CancellationToken ct = default)
    {
        await mediator.Send(new BulkDeleteUsersCommand { Ids = request.Ids }, ct);
        return NoContent();
    }

    public record InviteUserRequest(string Email, string? Role = null);

    [HttpPost("users/invite")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request, CancellationToken ct = default)
    {
        var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
        var currentUser = HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
        var jwtService = HttpContext.RequestServices.GetRequiredService<R2WAI.Infrastructure.Authentication.JwtService>();

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        // The invite email tells the recipient to "use this code to create your account", so the
        // code has to actually be redeemable — it used to be generated, emailed, and discarded with
        // nothing in the DB to check it against, and no endpoint that could check it even if there
        // were. Reuses the same create-user-then-set-a-reset-token pattern already proven for access
        // request approval: create the account now, then let the invitee set their own password via
        // the existing Reset Password page using this same token as the "reset code".
        var existingUser = await dbContext.Users.IgnoreQueryFilters()
            .Where(u => !u.IsDeleted).FirstOrDefaultAsync(u => u.Email == request.Email && u.TenantId == tenantId, ct);
        if (existingUser is not null)
            return Conflict(new { error = "A user with this email already exists." });

        var inviter = await dbContext.Users.FindAsync([currentUser.UserId], ct);
        var tenant = await dbContext.Tenants.FindAsync([tenantId], ct);

        var localPart = request.Email.Split('@')[0];
        var newUser = new Domain.Entities.User(Guid.NewGuid(), tenantId, request.Email, request.Email, localPart, string.Empty);

        var tokenBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var inviteToken = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        newUser.SetPasswordResetToken(jwtService.HashRefreshToken(inviteToken), DateTime.UtcNow.AddDays(7));

        // Role assignment isn't wired here — the same gap existed in the original code (the Role
        // field was accepted and silently ignored). Left as-is rather than bolted on as a side
        // effect of this fix; role assignment is a separate, already-existing admin action
        // (see AssignUserRoles above) that can be applied to the new user after creation.
        await dbContext.Users.AddAsync(newUser, ct);
        await dbContext.SaveChangesAsync(ct);

        await emailService.SendUserInviteAsync(
            request.Email,
            inviter is not null ? $"{inviter.FirstName} {inviter.LastName}" : "Admin",
            tenant?.Name ?? "R2WAI",
            inviteToken, ct);

        logger.LogInformation("User invitation sent to {Email}, account {UserId} created", request.Email, newUser.Id);
        return Ok(new { message = $"Invitation sent to {request.Email}", userId = newUser.Id });
    }
}
