using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Governance.Commands;
using R2WAI.Application.Features.Governance.Queries;

namespace R2WAI.Api.Controllers;

// Security & Policies is a Super Admin-only surface per roleNav.ts (hidden entirely from the
// plain-Admin nav) — this was previously "Admin,SystemAdmin", which let any Admin reach every
// GlobalPolicy read/write by direct API call regardless of what the UI showed.
[ApiController]
[Authorize(Roles = "SystemAdmin")]
[Route("api/v1/governance")]
public class GovernanceController(IMediator mediator) : ControllerBase
{
    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicies(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetGlobalPoliciesQuery(), ct);
        return Ok(result);
    }

    [HttpGet("auth-status")]
    public async Task<IActionResult> GetAuthStatus(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetAuthSecurityStatusQuery(), ct);
        return Ok(result);
    }

    [HttpPut("policies/{type}")]
    public async Task<IActionResult> UpsertPolicy(string type, [FromBody] UpsertGlobalPolicyCommand command, CancellationToken ct = default)
    {
        command = command with { Type = type };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }
}
