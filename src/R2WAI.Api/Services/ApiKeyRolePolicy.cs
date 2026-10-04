namespace R2WAI.Api.Services;

/// <summary>
/// Which roles an API key may carry. Roles used to be stored exactly as typed, so an Admin could create a
/// key with Roles = "SystemAdmin" and then authenticate as a SystemAdmin (privilege escalation), and a
/// typo or a retired role name silently produced a key with a meaningless role.
/// A caller may only grant roles they hold themselves; Admin implies User and SystemAdmin implies both.
/// </summary>
public static class ApiKeyRolePolicy
{
    // Ascending privilege.
    private static readonly string[] Assignable = ["User", "Admin", "SystemAdmin"];

    public sealed record Result(bool IsValid, bool IsForbidden, string? Error, string[] Roles);

    public static Result Check(IEnumerable<string>? requestedRoles, Func<string, bool> callerHasRole)
    {
        var callerRank = -1;
        for (var rank = 0; rank < Assignable.Length; rank++)
        {
            if (callerHasRole(Assignable[rank]))
                callerRank = rank;
        }

        var granted = new List<string>();
        foreach (var raw in requestedRoles ?? [])
        {
            var role = Assignable.FirstOrDefault(r => r.Equals(raw?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (role is null)
                return new Result(false, false,
                    $"'{raw}' is not an assignable role. Use one of: {string.Join(", ", Assignable)}.", []);

            if (Array.IndexOf(Assignable, role) > callerRank)
                return new Result(false, true,
                    $"You cannot give an API key the '{role}' role because you do not hold it yourself.", []);

            if (!granted.Contains(role))
                granted.Add(role);
        }

        return new Result(true, false, null, [.. granted]);
    }
}
