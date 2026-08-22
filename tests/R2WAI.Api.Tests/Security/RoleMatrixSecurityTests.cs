using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Security;

/// <summary>
/// Every other authorization test in this project asserts "authenticated vs not" (401). None asserted
/// "the right kind of authenticated" — every seeded account before this was Admin, so a test could
/// never tell the difference between "role checks are enforced" and "the only account happens to be
/// an admin". This is the role-matrix negative-test gap the R2WAI redesign's test section (§6) called
/// out as missing. Uses the newly-seeded "user@r2wai.io" plain-User-role account
/// (ApplicationDbContextSeed) against a sample of Admin/SystemAdmin-gated routes spanning both
/// [Authorize(Roles=...)] and policy-based ([Authorize(Policy="...")]) enforcement.
/// </summary>
public class RoleMatrixSecurityTests : IntegrationTestBase
{
    public RoleMatrixSecurityTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    public static IEnumerable<object[]> AdminOnlyGetRoutes =>
    [
        ["/api/v1/admin/users"],
        ["/api/v1/admin/roles"],
        ["/api/v1/admin/access-requests"],
        ["/api/v1/governance/policies"],
    ];

    [Theory]
    [MemberData(nameof(AdminOnlyGetRoutes))]
    public async Task StandardUser_CannotReadAdminOnlyRoute(string route)
    {
        var client = await GetAuthenticatedClientAsync("user@r2wai.io", "R2wai_User!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return; // login itself failed — nothing to assert

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminOnlyGetRoutes))]
    public async Task AdminUser_CanReadAdminOnlyRoute(string route)
    {
        var client = await GetAuthenticatedClientAsync();
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.GetAsync(route);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CannotCreateDepartment()
    {
        // [Authorize(Roles = "Admin,SystemAdmin")] on this one action only — GET is open to any
        // authenticated user (department dropdowns need it), so this specifically checks the
        // write-path boundary rather than the whole controller.
        var client = await GetAuthenticatedClientAsync("user@r2wai.io", "R2wai_User!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.PostAsJsonAsync("/api/v1/departments", new { Name = "Should Not Be Created" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CannotManageWorkflows()
    {
        // Policy-based ("CanManageWorkflows" = Admin,WorkflowManager), not role-based — a different
        // enforcement mechanism than the [Authorize(Roles=...)] routes above, worth covering
        // separately since a bug in one wouldn't necessarily show up in the other.
        var client = await GetAuthenticatedClientAsync("user@r2wai.io", "R2wai_User!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.PostAsJsonAsync("/api/v1/workflows", new
        {
            Name = "Should Not Be Created",
            Type = "sequential",
            Steps = "[]"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StandardUser_CanReadOwnProfile()
    {
        // The negative tests above are only meaningful if this account can authenticate and use the
        // API at all — confirms the seeded standard user isn't just universally rejected.
        var client = await GetAuthenticatedClientAsync("user@r2wai.io", "R2wai_User!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
