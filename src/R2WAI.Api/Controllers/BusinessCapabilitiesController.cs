using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.BusinessCapabilities.Commands;
using R2WAI.Application.Features.BusinessCapabilities.Queries;

namespace R2WAI.Api.Controllers;

// Authorization mirrors AssistantsController's own CRUD (class-level [Authorize] only, no
// SystemAdmin restriction) — a Capability is edited from inside the Assistant Studio page, which
// is already Admin/SuperAdmin-only in roleNav.ts; there is no separate, wider-reachable surface
// for it the way CapabilitiesController's "Tools & APIs" management page has.
[ApiController]
[Authorize]
[Route("api/v1/business-capabilities")]
public class BusinessCapabilitiesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBusinessCapabilityCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] Guid assistantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await mediator.Send(new GetBusinessCapabilitiesQuery { AssistantId = assistantId, Page = page, PageSize = pageSize }, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetBusinessCapabilityByIdQuery { Id = id }, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBusinessCapabilityCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetStatusRequest request, CancellationToken ct = default)
    {
        var result = await mediator.Send(new SetBusinessCapabilityStatusCommand { Id = id, Status = request.Status }, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await mediator.Send(new DeleteBusinessCapabilityCommand { Id = id }, ct);
        return NoContent();
    }

    public record SetStatusRequest(string Status);
}
