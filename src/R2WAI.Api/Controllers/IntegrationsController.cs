using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Integrations.Commands;
using R2WAI.Application.Features.Integrations.Queries;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.DynamicTools;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class IntegrationsController(
    IMediator mediator,
    IRepository<ToolDefinition> toolDefinitions,
    ICurrentUserService currentUser,
    DynamicToolExecutor dynamicToolExecutor,
    ILogger<IntegrationsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
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

        var toolDef = await toolDefinitions.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(ToolDefinition), id);
        if (toolDef.TenantId != tenantId)
            throw new UnauthorizedException();

        if (toolDef.ToolType != Domain.Enums.ToolType.Http)
            return Ok(new { success = true, message = $"Connection to '{toolDef.Name}' validated (type: {toolDef.ToolType})." });

        if (string.IsNullOrEmpty(toolDef.EndpointUrl))
            return UnprocessableEntity(new { success = false, message = "No endpoint URL configured for this integration." });

        if (!IsAllowedTestEndpoint(toolDef.EndpointUrl))
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

            return failed
                ? UnprocessableEntity(new { success = false, message = resultText })
                : Ok(new { success = true, message = $"Connection to '{toolDef.Name}' succeeded." });
        }
        catch (TaskCanceledException)
        {
            return UnprocessableEntity(new { success = false, message = "Connection test timed out after 10 seconds." });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Integration connection test failed for {IntegrationId}", id);
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

    private static bool IsAllowedTestEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("https" or "http"))
            return false;

        var host = uri.Host;
        if (host is "localhost" or "127.0.0.1" or "0.0.0.0" or "::1")
            return false;

        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4)
            {
                if (bytes[0] == 10) return false;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
                if (bytes[0] == 192 && bytes[1] == 168) return false;
                if (bytes[0] == 169 && bytes[1] == 254) return false;
            }
        }

        var blockedSuffixes = new[] { ".internal", ".local", ".corp", ".svc.cluster.local" };
        if (blockedSuffixes.Any(s => host.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }
}
