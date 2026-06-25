using System.Net;
using System.Net.Http.Json;

namespace R2WAI.Api.Tests.Controllers;

public class ApprovalFlowTests : IntegrationTestBase
{
    public ApprovalFlowTests(R2WAIWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetApprovals_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/approvals");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingApprovals_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/approvals/pending");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetApprovalPolicies_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/approvals/policies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApproveRequest_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/approvals/{Guid.NewGuid()}/approve",
            new { Comments = "Approved" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RejectRequest_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/v1/approvals/{Guid.NewGuid()}/reject",
            new { Comments = "Rejected" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePolicy_WithoutAuth_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/approvals/policies", new
        {
            Name = "Test Policy",
            ApproverRoles = "Admin"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
