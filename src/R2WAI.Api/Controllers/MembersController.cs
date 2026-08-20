using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Members.Commands;
using R2WAI.Application.Features.Members.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/members")]
public class MembersController(IMediator mediator) : ControllerBase
{
    [HttpGet("wallet")]
    public async Task<IActionResult> GetMyWallet(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMyWalletQuery(), ct);
        return Ok(result);
    }

    [HttpGet("points-history")]
    public async Task<IActionResult> GetMyPointsHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMyPointsHistoryQuery { Page = page, PageSize = pageSize }, ct);
        return Ok(result);
    }

    [HttpPost("points/convert")]
    public async Task<IActionResult> ConvertPoints([FromBody] ConvertPointsCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("withdrawals")]
    public async Task<IActionResult> RequestWithdrawal([FromBody] RequestWithdrawalCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return StatusCode(201, result);
    }

    [HttpPost("plan-upgrades")]
    public async Task<IActionResult> RequestPlanUpgrade([FromBody] RequestPlanUpgradeCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return StatusCode(201, result);
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetEventsQuery { IncludeInactive = false }, ct);
        return Ok(result);
    }
}

[ApiController]
[Authorize(Roles = "Admin,SystemAdmin")]
[Route("api/v1/admin/members")]
public class AdminMembersController(IMediator mediator) : ControllerBase
{
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] bool includeInactive = true, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetEventsQuery { IncludeInactive = includeInactive }, ct);
        return Ok(result);
    }

    [HttpPost("events")]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return StatusCode(201, result);
    }

    [HttpPut("events/{id:guid}")]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("events/award-points")]
    public async Task<IActionResult> AwardEventPoints([FromBody] AwardEventPointsCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("withdrawals")]
    public async Task<IActionResult> GetWithdrawalRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetWithdrawalRequestsQuery { Page = page, PageSize = pageSize, Status = status }, ct);
        return Ok(result);
    }

    [HttpPost("withdrawals/{id:guid}/approve")]
    public async Task<IActionResult> ApproveWithdrawal(Guid id, [FromBody] ApproveWithdrawalCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("withdrawals/{id:guid}/reject")]
    public async Task<IActionResult> RejectWithdrawal(Guid id, [FromBody] RejectWithdrawalCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("withdrawals/{id:guid}/complete")]
    public async Task<IActionResult> CompleteWithdrawal(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new CompleteWithdrawalCommand { Id = id }, ct);
        return Ok(result);
    }

    [HttpGet("plan-upgrades")]
    public async Task<IActionResult> GetPlanUpgradeRequests(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetPlanUpgradeRequestsQuery { Page = page, PageSize = pageSize, Status = status }, ct);
        return Ok(result);
    }

    [HttpPost("plan-upgrades/{id:guid}/approve")]
    public async Task<IActionResult> ApprovePlanUpgrade(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ApprovePlanUpgradeCommand { Id = id }, ct);
        return Ok(result);
    }

    [HttpPost("plan-upgrades/{id:guid}/reject")]
    public async Task<IActionResult> RejectPlanUpgrade(Guid id, CancellationToken ct = default)
    {
        var result = await mediator.Send(new RejectPlanUpgradeCommand { Id = id }, ct);
        return Ok(result);
    }
}
