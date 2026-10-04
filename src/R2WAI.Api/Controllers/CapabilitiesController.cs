using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Capabilities.Commands;
using R2WAI.Application.Features.Capabilities.Queries;

namespace R2WAI.Api.Controllers;

// Reads stay open to any authenticated user — the Assistant editor's Tools tab (Admin-reachable,
// not just Super Admin) lists capabilities read-only via GET to populate its "+Add Tool" picker.
// Writes are Super Admin-only per roleNav.ts (the "Tools & APIs" management page is hidden
// entirely from the plain-Admin nav) — every mutating action below was previously reachable by any
// authenticated user, including a plain User, via direct API call.
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class CapabilitiesController(IMediator mediator) : ControllerBase
{
    [Authorize(Roles = "SystemAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCapabilityCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? applicationId = null,
        CancellationToken ct = default)
    {
        var query = new GetCapabilitiesQuery { Page = page, PageSize = pageSize, Search = search, ApplicationId = applicationId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetCapabilityByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [Authorize(Roles = "SystemAdmin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCapabilityCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [Authorize(Roles = "SystemAdmin")]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveRequest request, CancellationToken ct = default)
    {
        var command = new SetCapabilityActiveCommand { Id = id, IsActive = request.IsActive };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [Authorize(Roles = "SystemAdmin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await mediator.Send(new DeleteCapabilityCommand { Id = id }, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetToolDefinitionVersionsQuery { ToolDefinitionId = id }, ct);
        return Ok(result);
    }

    [Authorize(Roles = "SystemAdmin")]
    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> CreateVersion(Guid id, [FromBody] CreateToolDefinitionVersionCommand command, CancellationToken ct = default)
    {
        command = command with { ToolDefinitionId = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [Authorize(Roles = "SystemAdmin")]
    [HttpPost("{id:guid}/versions/{versionId:guid}/rollback")]
    public async Task<IActionResult> RollbackVersion(Guid id, Guid versionId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new RollbackToolDefinitionVersionCommand { VersionId = versionId }, ct);
        return Ok(result);
    }

    public record SetActiveRequest(bool IsActive);
}
