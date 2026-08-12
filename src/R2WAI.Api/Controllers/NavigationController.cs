using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Navigation.Commands;
using R2WAI.Application.Features.Navigation.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class NavigationController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNavigationItemCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetByApplication), new { applicationId = result.ApplicationId }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetByApplication([FromQuery] Guid applicationId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetNavigationItemsQuery { ApplicationId = applicationId }, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNavigationItemCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetEnabled(Guid id, [FromBody] SetEnabledRequest request, CancellationToken ct = default)
    {
        var result = await mediator.Send(new SetNavigationItemEnabledCommand { Id = id, IsEnabled = request.IsEnabled }, ct);
        return Ok(result);
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderNavigationItemsCommand command, CancellationToken ct = default)
    {
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await mediator.Send(new DeleteNavigationItemCommand { Id = id }, ct);
        return NoContent();
    }

    public record SetEnabledRequest(bool IsEnabled);
}
