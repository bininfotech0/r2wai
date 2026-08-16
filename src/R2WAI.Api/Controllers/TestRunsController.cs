using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.TestCases.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class TestRunsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? assistantId = null,
        [FromQuery] Guid? applicationId = null,
        CancellationToken ct = default)
    {
        var query = new GetTestRunsQuery { Page = page, PageSize = pageSize, AssistantId = assistantId, ApplicationId = applicationId };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetTestRunByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }
}
