using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Capabilities.Commands;
using R2WAI.Application.Features.Capabilities.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class CapabilitiesController(IMediator mediator) : ControllerBase
{
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

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCapabilityCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveRequest request, CancellationToken ct = default)
    {
        var command = new SetCapabilityActiveCommand { Id = id, IsActive = request.IsActive };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await mediator.Send(new DeleteCapabilityCommand { Id = id }, ct);
        return NoContent();
    }

    public record SetActiveRequest(bool IsActive);
}
