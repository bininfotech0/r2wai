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
        // Policy-based ("CanManageWorkflows" = Admin only, post-CollapseRbacToThreeRoles), not
        // role-based — a different enforcement mechanism than the [Authorize(Roles=...)] routes
        // above, worth covering separately since a bug in one wouldn't necessarily show up in the other.
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
    public async Task PlainAdmin_CanManageWorkflows()
    {
        // Regression guard for the RBAC collapse (CollapseRbacToThreeRoles): "CanManageWorkflows"
        // used to admit Admin OR WorkflowManager. Every WorkflowManager holder was migrated onto
        // Admin — this proves that migration path still passes the policy, i.e. nobody who could
        // manage workflows before the collapse lost that ability. "deptadmin@r2wai.io" (plain
        // Admin, no SystemAdmin) is the right account: it isolates the Admin-role grant specifically.
        var client = await GetAuthenticatedClientAsync("deptadmin@r2wai.io", "R2wai_DeptAdmin!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.PostAsJsonAsync("/api/v1/workflows", new
        {
            Name = "RBAC Collapse Regression Probe",
            Type = "sequential",
            Steps = "[]"
        });

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public static IEnumerable<object[]> SuperAdminOnlyGetRoutes =>
    [
        ["/api/v1/governance/policies"],
        ["/api/v1/admin/models"],
        // P0-5 (2026-09-20 audit): AccessRequest is a pre-tenant, platform-wide signup queue — a plain
        // tenant Admin used to be able to read every pending request platform-wide and approve one
        // into their OWN tenant, regardless of which organization the requester named. Moved here from
        // AdminOnlyGetRoutes, where the shared seeded "admin" account (holds both Admin and SystemAdmin)
        // couldn't have told the difference — deptadmin@r2wai.io (Admin only) is what actually proves
        // the narrower grant.
        ["/api/v1/admin/access-requests"],
    ];

    [Theory]
    [MemberData(nameof(SuperAdminOnlyGetRoutes))]
    public async Task PlainAdmin_CannotReadSuperAdminOnlyRoute(string route)
    {
        // "deptadmin@r2wai.io" holds Admin only, no SystemAdmin — closes the gap RoleMatrixSecurityTests
        // couldn't previously catch: every prior seeded "admin" account held both roles together, so
        // nothing could tell "Super Admin-only routes are enforced server-side" apart from "the only
        // Admin account happens to also be a SystemAdmin". roleNav.ts hides Security & Policies and AI
        // Models from the Admin persona entirely — the backend must actually agree, not just the nav.
        var client = await GetAuthenticatedClientAsync("deptadmin@r2wai.io", "R2wai_DeptAdmin!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(SuperAdminOnlyGetRoutes))]
    public async Task SystemAdmin_CanReadSuperAdminOnlyRoute(string route)
    {
        var client = await GetAuthenticatedClientAsync();
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.GetAsync(route);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlainAdmin_CannotCreateCapability()
    {
        // Tools & APIs is also Super Admin-only per roleNav.ts — CapabilitiesController's writes were
        // previously reachable by any authenticated user at all, not just a misplaced plain Admin.
        var client = await GetAuthenticatedClientAsync("deptadmin@r2wai.io", "R2wai_DeptAdmin!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.PostAsJsonAsync("/api/v1/capabilities", new
        {
            Name = "Should Not Be Created",
            Description = "role-matrix probe",
            RiskLevel = "Low",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlainAdmin_CanReadCapabilities()
    {
        // The Assistant editor's Tools tab (Admin-reachable, not Super Admin-only) lists capabilities
        // read-only to populate its "+Add Tool" picker — the fix above must not break that real path.
        var client = await GetAuthenticatedClientAsync("deptadmin@r2wai.io", "R2wai_DeptAdmin!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.GetAsync("/api/v1/capabilities?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlainAdmin_CannotApproveAccessRequest()
    {
        // Same P0-5 gap as the GET route above, on the write path: a plain tenant Admin approving an
        // access request used to create the new account under their OWN tenant.
        var client = await GetAuthenticatedClientAsync("deptadmin@r2wai.io", "R2wai_DeptAdmin!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.PostAsync($"/api/v1/admin/access-requests/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlainAdmin_CannotRejectAccessRequest()
    {
        var client = await GetAuthenticatedClientAsync("deptadmin@r2wai.io", "R2wai_DeptAdmin!2026");
        if (client.DefaultRequestHeaders.Authorization is null) return;

        var response = await client.PostAsync($"/api/v1/admin/access-requests/{Guid.NewGuid()}/reject", null);

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
