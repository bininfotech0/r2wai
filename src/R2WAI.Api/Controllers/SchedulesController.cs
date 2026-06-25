using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Domain.Entities;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize(Policy = "CanManageWorkflows")]
[Route("api/v1/workflows/schedules")]
public class SchedulesController(ApplicationDbContext dbContext, ILogger<SchedulesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = dbContext.WorkflowSchedules
            .Include(s => s.Workflow)
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Name,
                WorkflowName = s.Workflow != null ? s.Workflow.Name : null,
                s.WorkflowId,
                s.CronExpression,
                s.CronDescription,
                s.IsActive,
                s.NextRunAt,
                s.LastRunAt,
                s.LastRunStatus,
                s.CreatedAt
            }).ToListAsync(ct);

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var schedule = await dbContext.WorkflowSchedules
            .Include(s => s.Workflow)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

        if (schedule is null) return NotFound();
        return Ok(schedule);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateScheduleRequest request, CancellationToken ct = default)
    {
        var workflow = await dbContext.Workflows.FirstOrDefaultAsync(w => w.Id == request.WorkflowId, ct);
        if (workflow is null) return BadRequest(new { error = "Workflow not found" });

        var tenantClaim = User.FindFirst("tenant_id");
        var tenantId = tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tid) ? tid : Guid.Empty;

        var schedule = new WorkflowSchedule(
            Guid.NewGuid(), tenantId, request.WorkflowId, request.Name,
            request.CronExpression, request.CronDescription);

        dbContext.WorkflowSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Schedule created: {Name} for workflow {WorkflowId}", request.Name, request.WorkflowId);
        return CreatedAtAction(nameof(GetById), new { id = schedule.Id }, new { schedule.Id, schedule.Name });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScheduleRequest request, CancellationToken ct = default)
    {
        var schedule = await dbContext.WorkflowSchedules.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (schedule is null) return NotFound();

        schedule.Update(request.Name, request.CronExpression, request.CronDescription);
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { schedule.Id, schedule.Name });
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct = default)
    {
        var schedule = await dbContext.WorkflowSchedules.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (schedule is null) return NotFound();

        schedule.SetActive(!schedule.IsActive);
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { schedule.Id, schedule.IsActive });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var schedule = await dbContext.WorkflowSchedules.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (schedule is null) return NotFound();

        schedule.SoftDelete();
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Schedule deleted: {Id}", id);
        return NoContent();
    }

    public record CreateScheduleRequest(Guid WorkflowId, string Name, string CronExpression, string? CronDescription);
    public record UpdateScheduleRequest(string Name, string CronExpression, string? CronDescription);
}
