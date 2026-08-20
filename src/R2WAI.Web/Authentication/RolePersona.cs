using System.Security.Claims;

namespace R2WAI.Web.Authentication;

/// <summary>
/// UI-only navigation persona. Does not affect authorization — the API enforces access via its
/// own [Authorize(Roles/Policy)] attributes regardless of what this resolves to. This exists purely
/// to pick which simplified nav menu a signed-in user sees.
/// </summary>
public enum RolePersona
{
    Public,
    Citizen,
    Officer,
    DepartmentAdmin,
    SuperAdmin
}

public static class RolePersonaExtensions
{
    public static RolePersona GetPersona(this ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return RolePersona.Public;

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roles.Contains("SystemAdmin"))
            return RolePersona.SuperAdmin;

        if (roles.Contains("Admin"))
            return RolePersona.DepartmentAdmin;

        if (roles.Contains("WorkflowManager") || roles.Contains("Editor") || roles.Contains("Contributor") || roles.Contains("UserManager"))
            return RolePersona.Officer;

        return RolePersona.Citizen;
    }
}
