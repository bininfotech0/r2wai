using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.TestCases.Commands;
using R2WAI.Application.Features.TestCases.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class TestCasesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTestCaseCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? assistantId = null,
        [FromQuery] Guid? applicationId = null,
        CancellationToken ct = default)
    {
        var query = new GetTestCasesQuery { Page = page, PageSize = pageSize, Search = search, AssistantId = assistantId, ApplicationId = applicationId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetTestCaseByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTestCaseCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetEnabled(Guid id, [FromBody] SetEnabledRequest request, CancellationToken ct = default)
    {
        var command = new SetTestCaseEnabledCommand { Id = id, IsEnabled = request.IsEnabled };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/run")]
    public async Task<IActionResult> Run(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new RunTestCaseCommand { TestCaseId = id }, ct);
        return Ok(result);
    }

    [HttpPost("run-all")]
    public async Task<IActionResult> RunAll([FromBody] RunAllRequest request, CancellationToken ct = default)
    {
        var command = new RunAllTestCasesCommand { AssistantId = request.AssistantId, ApplicationId = request.ApplicationId };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await mediator.Send(new DeleteTestCaseCommand { Id = id }, ct);
        return NoContent();
    }

    public record SetEnabledRequest(bool IsEnabled);
    public record RunAllRequest(Guid? AssistantId, Guid? ApplicationId);
}
