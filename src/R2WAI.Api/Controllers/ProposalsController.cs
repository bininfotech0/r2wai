using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Proposals.Commands;
using R2WAI.Application.Features.Proposals.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ProposalsController(IMediator mediator, ILogger<ProposalsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProposalCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Generating proposal: {Title}", command.Title);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetList), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new GetProposalsQuery { Page = page, PageSize = pageSize };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }
}
