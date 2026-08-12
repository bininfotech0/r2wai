using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Applications.Commands;
using R2WAI.Application.Features.Applications.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class DepartmentsController(IMediator mediator) : ControllerBase
{
    // Reading departments is open to any authenticated user — the Add Application wizard and
    // Applications list need the department dropdown/filter regardless of role. Only creating,
    // renaming, or removing a department is Super-Admin/Governance territory.
    [HttpPost]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = new GetDepartmentsQuery { Page = page, PageSize = pageSize, Search = search };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetDepartmentByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteDepartmentCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }
}
