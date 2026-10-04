using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Integrations.Commands;
using R2WAI.Application.Features.Integrations.Queries;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI;
using R2WAI.Infrastructure.AI.DynamicTools;
using R2WAI.Infrastructure.Security;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class IntegrationsController(
    IMediator mediator,
    IRepository<ToolDefinition> toolDefinitions,
    ICurrentUserService currentUser,
    DynamicToolExecutor dynamicToolExecutor,
    IOpenApiImportService openApiImportService,
    IUnitOfWork unitOfWork,
    ILogger<IntegrationsController> logger) : ControllerBase
{
    // docs/api/MISSING-BACKEND-ENDPOINTS.md §3.5 #72 — curated suggestions the "Integration
    // Studio" grid renders; "Connect" pre-fills the same real create form below, not a fake
    // OAuth handshake (#73/#74 stay open — no per-provider app registrations exist to drive one).
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetIntegrationCatalogQuery(), ct);
        return Ok(result);
    }

    public record AnalyzeOpenApiRequest(string? Url, string? FileContent);
    public record CommitOpenApiRequest(string BaseUrl, List<OpenApiOperationCandidate> Operations);

    [HttpPost("openapi-import/analyze")]
    public async Task<IActionResult> AnalyzeOpenApiSpec([FromBody] AnalyzeOpenApiRequest request, CancellationToken ct = default)
    {
        var result = await openApiImportService.AnalyzeAsync(request.Url, request.FileContent, ct);
        return Ok(result);
    }

    [HttpPost("openapi-import/commit")]
    public async Task<IActionResult> CommitOpenApiImport([FromBody] CommitOpenApiRequest request, CancellationToken ct = default)
    {
        var ids = new List<Guid>();
        foreach (var op in request.Operations)
        {
            var id = await mediator.Send(new CreateIntegrationCommand
            {
                Name = op.SuggestedName,
                Type = "Http",
                Description = op.Summary,
                EndpointUrl = request.BaseUrl,
                HttpMethod = op.Method,
                EndpointPath = op.Path,
            }, ct);
            ids.Add(id);
        }

        logger.LogInformation("Committed {Count} integrations from OpenAPI import", ids.Count);
        return Ok(new { ids });
    }
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = new GetIntegrationsQuery
        {
            Search = search,
            Category = category,
            Page = page,
            PageSize = pageSize,
        };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var query = new GetIntegrationByIdQuery { Id = id };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIntegrationCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating integration: {Name}", command.Name);
        var id = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIntegrationCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct = default)
    {
        var command = new ToggleIntegrationCommand { Id = id };
        var isActive = await mediator.Send(command, ct);
        return Ok(new { id, isActive });
    }

    // Exercises the same dynamic tool-calling path AiFunctionAuditFilter/SemanticKernelService use
    // when the AI itself invokes this integration (DynamicToolExecutor → HttpTool) — not a separate,
    // simpler connectivity ping. So a green "Test" here is a real signal the AI can actually call
    // this integration, not just that its host is reachable.
    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        // Must eager-load ApplicationApi — without it, an ApplicationApi-linked tool's nav property
        // is null and this action can't tell it apart from an unlinked one (DynamicToolFunctionFactory,
        // the real AI-invocation path, already does this include; this action previously didn't).
        var toolDef = await toolDefinitions.GetByIdAsync(id, "ApplicationApi", ct)
            ?? throw new NotFoundException(nameof(ToolDefinition), id);
        if (toolDef.TenantId != tenantId)
            throw new UnauthorizedException();

        // This action used to call DynamicToolExecutor directly, bypassing the exact governance
        // (RequiredRole/ApprovalRequired/RiskLevel) AiFunctionAuditFilter enforces on the AI-invocation
        // path — meaning a tool an admin explicitly gated as high-risk or approval-required for AI use
        // was still fully callable by any authenticated tenant user through this endpoint. Reusing the
        // same pure decision function closes that gap without duplicating or diverging from the logic
        // the AI path already proved out.
        var governance = AiFunctionAuditFilter.EvaluateGovernance(toolDef, currentUser.Roles);
        if (governance != GovernanceDecision.Allow)
        {
            var reason = governance switch
            {
                GovernanceDecision.DenyMissingRole => $"This action requires the '{toolDef.RequiredRole}' role, which you do not have.",
                GovernanceDecision.DenyApprovalRequired => "This action requires administrator approval and cannot be tested directly.",
                _ => "This action is not permitted by tenant policy.",
            };
            return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = reason });
        }

        // Was previously an unconditional fake success here ("Connection validated") with zero actual
        // checking — DynamicToolFunctionFactory only ever turns ToolType.Http rows into callable
        // functions, so a Database/Email/Script/Custom integration is never actually reachable by an
        // AI assistant regardless of what this action claims. Report that honestly instead.
        if (toolDef.ToolType != Domain.Enums.ToolType.Http)
            return UnprocessableEntity(new
            {
                success = false,
                message = $"'{toolDef.ToolType}' integrations aren't executable yet — only REST/HTTP integrations can be tested or called by an assistant today."
            });

        var targetUrl = toolDef.ApplicationApi?.BaseUrl ?? toolDef.EndpointUrl;
        if (string.IsNullOrEmpty(targetUrl))
            return UnprocessableEntity(new { success = false, message = "No endpoint URL configured for this integration." });

        if (!IsAllowedTestEndpoint(targetUrl))
            return BadRequest(new { success = false, message = "Endpoint URL is not allowed. Internal network addresses are blocked." });

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var resultText = await dynamicToolExecutor.ExecuteAsync(toolDef, input: null, cts.Token);

            // DynamicToolExecutor never throws for a failed/blocked call — it returns a human-readable
            // message instead — so success is inferred from whether that message reads as a failure.
            var failed = resultText.StartsWith("API call failed:", StringComparison.Ordinal)
                || resultText.Contains("isn't supported for AI-invoked calls yet", StringComparison.Ordinal)
                || resultText.Contains("is not linked to a registered API", StringComparison.Ordinal)
                || resultText.Contains("temporarily unavailable", StringComparison.Ordinal);

            toolDef.RecordTestResult(success: !failed);
            await unitOfWork.SaveChangesAsync(ct);

            return failed
                ? UnprocessableEntity(new { success = false, message = resultText })
                : Ok(new { success = true, message = $"Connection to '{toolDef.Name}' succeeded." });
        }
        catch (TaskCanceledException)
        {
            toolDef.RecordTestResult(success: false);
            await unitOfWork.SaveChangesAsync(ct);
            return UnprocessableEntity(new { success = false, message = "Connection test timed out after 10 seconds." });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Integration connection test failed for {IntegrationId}", id);
            toolDef.RecordTestResult(success: false);
            await unitOfWork.SaveChangesAsync(ct);
            return UnprocessableEntity(new { success = false, message = $"Connection test failed: {ex.Message}" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteIntegrationCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    // Delegates to the shared EgressGuard (P0-8) — kept as its own method here only so the early
    // BadRequest fast-fail UX for the Test button is unchanged; DynamicToolExecutor.ExecuteAsync now
    // enforces the identical rule on the real call too, so this is a fast-fail convenience, not the
    // only place the block happens.
    private static bool IsAllowedTestEndpoint(string endpoint) => EgressGuard.IsAllowedUrl(endpoint);
}
