using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Features.Admin.Queries;
using R2WAI.Application.Features.Operations.Queries;
using R2WAI.Application.Features.Workflows.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class OperationsController(IMediator mediator, R2WAI.Infrastructure.Persistence.ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        R2WAI.Domain.Enums.AuditAction? parsedAction = null;
        if (!string.IsNullOrEmpty(action) && Enum.TryParse<R2WAI.Domain.Enums.AuditAction>(action, true, out var act))
            parsedAction = act;

        var query = new GetAuditLogsQuery
        {
            Page = page,
            PageSize = pageSize,
            UserId = userId,
            EntityType = entityType,
            Action = parsedAction,
            From = from,
            To = to,
        };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("workflow-instances")]
    public async Task<IActionResult> GetWorkflowInstances(
        [FromQuery] Guid? workflowId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetWorkflowInstancesQuery
        {
            WorkflowId = workflowId,
            Page = page,
            PageSize = pageSize,
        };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken ct = default)
    {
        var dbStatus = "healthy";
        try
        {
            await dbContext.Database.CanConnectAsync(ct);
        }
        catch
        {
            dbStatus = "unhealthy";
        }

        var overallStatus = dbStatus == "healthy" ? "healthy" : "degraded";

        return Ok(new
        {
            status = overallStatus,
            timestamp = DateTime.UtcNow,
            services = new
            {
                api = "healthy",
                database = dbStatus,
            },
        });
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMetricsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("ai-stats")]
    public async Task<IActionResult> GetAiStats(CancellationToken ct = default)
    {
        var currentUser = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.ICurrentUserService>();
        if (currentUser.TenantId is null) return Unauthorized();

        var tenantId = currentUser.TenantId.Value;
        var since = DateTime.UtcNow.AddDays(-30);

        var totalTokens = await dbContext.Messages
            .Where(m => m.TenantId == tenantId && m.TokensUsed != null && m.CreatedAt >= since)
            .SumAsync(m => (long?)m.TokensUsed, ct) ?? 0L;

        var totalConversations = await dbContext.Conversations
            .Where(c => c.TenantId == tenantId && c.CreatedAt >= since)
            .CountAsync(ct);

        var recentMessages = await dbContext.Messages
            .Where(m => m.TenantId == tenantId && m.CreatedAt >= since)
            .OrderBy(m => m.ConversationId).ThenBy(m => m.CreatedAt)
            .Select(m => new { m.ConversationId, m.Role, m.CreatedAt })
            .ToListAsync(ct);

        double avgResponseSec = 0;
        var responseSpans = new List<double>();
        DateTime? lastUserAt = null;
        Guid? lastConvId = null;
        foreach (var m in recentMessages)
        {
            if (m.ConversationId != lastConvId) { lastUserAt = null; lastConvId = m.ConversationId; }
            if (m.Role == R2WAI.Domain.Enums.MessageRole.User) lastUserAt = m.CreatedAt;
            else if (m.Role == R2WAI.Domain.Enums.MessageRole.Assistant && lastUserAt is not null)
            {
                var span = (m.CreatedAt - lastUserAt.Value).TotalSeconds;
                if (span >= 0 && span < 120) responseSpans.Add(span);
                lastUserAt = null;
            }
        }
        if (responseSpans.Count > 0) avgResponseSec = responseSpans.Average();

        return Ok(new
        {
            totalTokens,
            totalConversations,
            avgResponseTimeSec = Math.Round(avgResponseSec, 1),
            samplesUsed = responseSpans.Count,
            windowDays = 30
        });
    }

    [HttpGet("errors")]
    public IActionResult GetErrorLogs(
        [FromQuery] int pageSize = 50,
        [FromQuery] string? level = null,
        [FromQuery] string? correlationId = null)
    {
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        var errors = new List<object>();

        if (!Directory.Exists(logPath))
            return Ok(new { items = errors });

        var logFiles = Directory.GetFiles(logPath, "*.log")
            .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
            .Take(3);

        foreach (var file in logFiles)
        {
            try
            {
                var lines = System.IO.File.ReadAllLines(file);
                foreach (var line in lines.Reverse())
                {
                    if (errors.Count >= pageSize) break;
                    if (!line.Contains("[ERR]") && !line.Contains("[WRN]") && !line.Contains("[FTL]"))
                        continue;

                    var entryLevel = line.Contains("[FTL]") ? "Critical"
                        : line.Contains("[ERR]") ? "Error"
                        : "Warning";

                    if (!string.IsNullOrEmpty(level) && !entryLevel.Equals(level, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrEmpty(correlationId) && !line.Contains(correlationId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var timestamp = DateTime.UtcNow;
                    if (line.Length >= 23 && DateTime.TryParse(line[..23], out var parsed))
                        timestamp = parsed;

                    var msgStart = line.IndexOf(']', line.IndexOf('[') + 1);
                    var message = msgStart > 0 ? line[(msgStart + 1)..].Trim() : line;

                    errors.Add(new
                    {
                        Level = entryLevel,
                        Message = message.Length > 500 ? message[..500] : message,
                        Timestamp = timestamp,
                        Source = Path.GetFileName(file),
                        CorrelationId = (string?)null,
                        StackTrace = (string?)null,
                    });
                }
            }
            catch { }
        }

        return Ok(new { items = errors });
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var currentUser = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.ICurrentUserService>();
        if (currentUser.TenantId is null) return Unauthorized();
        var tenantId = currentUser.TenantId.Value;

        var auditCount = await dbContext.AuditLogs.Where(a => a.TenantId == tenantId).CountAsync(ct);
        var workflowCount = await dbContext.WorkflowInstances.CountAsync(ct);
        var conversationCount = await dbContext.Conversations.Where(c => c.TenantId == tenantId).CountAsync(ct);

        var items = new List<object>();

        if (auditCount > 0)
        {
            items.Add(new
            {
                Id = Guid.NewGuid(),
                Name = $"Compliance Report - {DateTime.UtcNow:MMM yyyy}",
                Type = "Compliance",
                Period = "Last 30 days",
                GeneratedAt = DateTime.UtcNow,
                Status = "Completed",
                Stats = new { auditEntries = auditCount }
            });
        }

        if (conversationCount > 0)
        {
            items.Add(new
            {
                Id = Guid.NewGuid(),
                Name = $"Usage Report - {DateTime.UtcNow:MMM yyyy}",
                Type = "Usage",
                Period = "Last 30 days",
                GeneratedAt = DateTime.UtcNow,
                Status = "Completed",
                Stats = new { conversations = conversationCount, workflows = workflowCount }
            });
        }

        var since = DateTime.UtcNow.AddDays(-30);

        var totalTokens = await dbContext.Messages
            .Where(m => m.TenantId == tenantId && m.TokensUsed != null && m.CreatedAt >= since)
            .SumAsync(m => (long?)m.TokensUsed, ct) ?? 0L;

        if (totalTokens > 0 || conversationCount > 0)
        {
            items.Add(new
            {
                Id = Guid.NewGuid(),
                Name = $"Cost Report - {DateTime.UtcNow:MMM yyyy}",
                Type = "Cost",
                Period = "Last 30 days",
                GeneratedAt = DateTime.UtcNow,
                Status = "Completed",
                Stats = new { totalTokens, estimatedCost = Math.Round(totalTokens * 0.000002m, 4) }
            });
        }

        var assistantCount = await dbContext.AssistantDefinitions
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .CountAsync(ct);

        if (assistantCount > 0)
        {
            var assistantStats = await dbContext.AssistantDefinitions
                .Where(a => a.TenantId == tenantId && !a.IsDeleted)
                .Select(a => new { a.Name, a.UsageCount })
                .OrderByDescending(a => a.UsageCount)
                .Take(5)
                .ToListAsync(ct);

            items.Add(new
            {
                Id = Guid.NewGuid(),
                Name = $"Assistant Report - {DateTime.UtcNow:MMM yyyy}",
                Type = "Assistant",
                Period = "Last 30 days",
                GeneratedAt = DateTime.UtcNow,
                Status = "Completed",
                Stats = new { totalAssistants = assistantCount, topAssistants = assistantStats }
            });
        }

        return Ok(new { items, total = items.Count, page, pageSize });
    }

    [HttpPost("reports/generate")]
    public async Task<IActionResult> GenerateReport([FromBody] GenerateReportRequest request, CancellationToken ct = default)
    {
        var currentUser = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.ICurrentUserService>();
        if (currentUser.TenantId is null) return Unauthorized();
        var tenantId = currentUser.TenantId.Value;
        var since = DateTime.UtcNow.AddDays(-30);

        if (request.Type.Equals("cost", StringComparison.OrdinalIgnoreCase))
        {
            var totalTokens = await dbContext.Messages
                .Where(m => m.TenantId == tenantId && m.TokensUsed != null && m.CreatedAt >= since)
                .SumAsync(m => (long?)m.TokensUsed, ct) ?? 0L;

            var perAssistant = await dbContext.Conversations
                .Where(c => c.TenantId == tenantId && c.ReferenceId != null && c.CreatedAt >= since)
                .GroupBy(c => c.ReferenceId)
                .Select(g => new { AssistantId = g.Key, Conversations = g.Count() })
                .ToListAsync(ct);

            return Ok(new
            {
                type = "Cost",
                period = "Last 30 days",
                totalTokens,
                estimatedCost = Math.Round(totalTokens * 0.000002m, 4),
                assistantBreakdown = perAssistant
            });
        }

        if (request.Type.Equals("assistant", StringComparison.OrdinalIgnoreCase))
        {
            var assistants = await dbContext.AssistantDefinitions
                .Where(a => a.TenantId == tenantId && !a.IsDeleted)
                .Select(a => new
                {
                    a.Id, a.Name, a.Type, a.PublishStatus, a.UsageCount, a.PublishedVersion, a.CreatedAt
                })
                .OrderByDescending(a => a.UsageCount)
                .ToListAsync(ct);

            var totalConversations = await dbContext.Conversations
                .Where(c => c.TenantId == tenantId && c.ReferenceId != null && c.CreatedAt >= since)
                .CountAsync(ct);

            return Ok(new
            {
                type = "Assistant",
                period = "Last 30 days",
                totalAssistants = assistants.Count,
                totalConversations,
                assistants
            });
        }

        return BadRequest(new { error = "Invalid report type. Use 'cost' or 'assistant'." });
    }

    public record GenerateReportRequest(string Type, string? Period = "last30days");

    [HttpGet("audit-logs/export")]
    public async Task<IActionResult> ExportAuditLogs(
        [FromQuery] string format = "csv",
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        R2WAI.Domain.Enums.AuditAction? parsedAction = null;
        if (!string.IsNullOrEmpty(action) && Enum.TryParse<R2WAI.Domain.Enums.AuditAction>(action, true, out var a))
            parsedAction = a;

        var query = new GetAuditLogsQuery
        {
            Page = 1,
            PageSize = 5000,
            UserId = userId,
            EntityType = entityType,
            Action = parsedAction,
            From = from,
            To = to,
        };
        var result = await mediator.Send(query, ct);
        var items = result.Items;

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            return File(Encoding.UTF8.GetBytes(json), "application/json", "audit-logs.json");
        }

        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Action,EntityType,EntityId,UserId");
        foreach (var item in items)
        {
            csv.AppendLine($"{item.Timestamp:yyyy-MM-dd HH:mm:ss},{item.Action},{item.EntityType},{item.EntityId},{item.UserId}");
        }
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "audit-logs.csv");
    }
}
