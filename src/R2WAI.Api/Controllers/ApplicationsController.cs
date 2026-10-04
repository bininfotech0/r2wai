using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Applications.Commands;
using R2WAI.Application.Features.Applications.Queries;
using R2WAI.Domain.Enums;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ApplicationsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? departmentId = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new GetApplicationsQuery { Page = page, PageSize = pageSize, Search = search, DepartmentId = departmentId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetApplicationByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApplicationCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteApplicationCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeApplicationStatusRequest request, CancellationToken ct = default)
    {
        var command = new ChangeApplicationStatusCommand { Id = id, Action = request.Action };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    public record DiscoverApplicationRequest(string? OpenApiUrl, string? OpenApiFileContent);

    [HttpPost("{id:guid}/discover")]
    public async Task<IActionResult> Discover(Guid id, [FromBody] DiscoverApplicationRequest request, CancellationToken ct = default)
    {
        var command = new DiscoverApplicationCommand
        {
            ApplicationId = id,
            OpenApiUrl = request.OpenApiUrl,
            OpenApiFileContent = request.OpenApiFileContent
        };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/apis")]
    public async Task<IActionResult> GetApis(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetApplicationApisQuery { ApplicationId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/apis")]
    public async Task<IActionResult> CreateApi(Guid id, [FromBody] CreateApplicationApiCommand command, CancellationToken ct = default)
    {
        command = command with { ApplicationId = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/apis/{apiId:guid}")]
    public async Task<IActionResult> UpdateApi(Guid id, Guid apiId, [FromBody] UpdateApplicationApiCommand command, CancellationToken ct = default)
    {
        command = command with { Id = apiId };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/apis/{apiId:guid}")]
    public async Task<IActionResult> DeleteApi(Guid id, Guid apiId, CancellationToken ct = default)
    {
        await mediator.Send(new DeleteApplicationApiCommand { Id = apiId }, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/configuration")]
    public async Task<IActionResult> GetConfiguration(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetApplicationConfigurationQuery { ApplicationId = id }, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/configuration")]
    public async Task<IActionResult> UpdateConfiguration(Guid id, [FromBody] UpdateApplicationConfigurationCommand command, CancellationToken ct = default)
    {
        command = command with { ApplicationId = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetApplicationVersionsQuery { ApplicationId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> CreateVersion(Guid id, [FromBody] CreateApplicationVersionCommand command, CancellationToken ct = default)
    {
        command = command with { ApplicationId = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/rollback")]
    public async Task<IActionResult> RollbackVersion(Guid id, Guid versionId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new RollbackApplicationVersionCommand { VersionId = versionId }, ct);
        return Ok(result);
    }

    public record ChangeApplicationStatusRequest(ApplicationAction Action);
}
