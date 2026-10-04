using R2WAI.Api.Services;

namespace R2WAI.Api.Tests.Security;

/// <summary>
/// Audit finding P1-8: an Admin could create an API key with Roles = "SystemAdmin" and then authenticate
/// as a SystemAdmin, because roles were stored exactly as typed.
/// </summary>
public class ApiKeyRolePolicyTests
{
    private static Func<string, bool> Holds(params string[] roles) =>
        role => roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Admin_cannot_mint_a_SystemAdmin_key()
    {
        var result = ApiKeyRolePolicy.Check(["SystemAdmin"], Holds("Admin"));

        Assert.False(result.IsValid);
        Assert.True(result.IsForbidden);
    }

    [Fact]
    public void Role_names_are_matched_case_insensitively_when_checking_escalation()
    {
        var result = ApiKeyRolePolicy.Check(["systemadmin"], Holds("Admin"));

        Assert.False(result.IsValid);
        Assert.True(result.IsForbidden);
    }

    [Fact]
    public void Admin_can_grant_admin_and_user_and_the_result_is_canonical()
    {
        var result = ApiKeyRolePolicy.Check(["admin", "USER", "Admin"], Holds("Admin"));

        Assert.True(result.IsValid);
        Assert.Equal(["Admin", "User"], result.Roles);
    }

    [Fact]
    public void SystemAdmin_can_grant_any_assignable_role()
    {
        var result = ApiKeyRolePolicy.Check(["SystemAdmin", "Admin", "User"], Holds("SystemAdmin"));

        Assert.True(result.IsValid);
        Assert.Equal(["SystemAdmin", "Admin", "User"], result.Roles);
    }

    [Theory]
    [InlineData("Editor")]
    [InlineData("WorkflowManager")]
    [InlineData("root")]
    [InlineData("")]
    public void Unknown_or_retired_roles_are_rejected_as_a_bad_request_not_a_forbidden(string role)
    {
        var result = ApiKeyRolePolicy.Check([role], Holds("SystemAdmin"));

        Assert.False(result.IsValid);
        Assert.False(result.IsForbidden);
    }

    [Fact]
    public void No_roles_requested_is_valid_and_grants_nothing()
    {
        Assert.Empty(ApiKeyRolePolicy.Check(null, Holds("Admin")).Roles);
        Assert.True(ApiKeyRolePolicy.Check([], Holds("Admin")).IsValid);
    }
}
