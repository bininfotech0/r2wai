using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Workspaces.Commands;
using R2WAI.Application.Features.Workspaces.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class WorkspacesController(IMediator mediator) : ControllerBase
{
    // Reading is open to any authenticated user of the tenant (pickers and the hub need it);
    // creating, renaming and deleting workspaces is admin work, as for every other tenant structure.
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetWorkspacesQuery(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetWorkspaceByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkspaceCommand command, CancellationToken ct = default) =>
        Ok(await mediator.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await mediator.Send(new DeleteWorkspaceCommand(id), ct);
        return NoContent();
    }
}
