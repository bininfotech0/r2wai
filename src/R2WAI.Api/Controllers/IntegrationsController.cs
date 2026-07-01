using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Features.Integrations.Commands;
using R2WAI.Application.Features.Integrations.Queries;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class IntegrationsController(IMediator mediator, IHttpClientFactory httpClientFactory, ILogger<IntegrationsController> logger) : ControllerBase
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

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct = default)
    {
        try
        {
            var query = new GetIntegrationByIdQuery { Id = id };
            var integration = await mediator.Send(query, ct);

            if (string.IsNullOrEmpty(integration.EndpointUrl))
                return UnprocessableEntity(new { success = false, message = "No endpoint URL configured for this integration." });

            if (!IsAllowedTestEndpoint(integration.EndpointUrl))
                return BadRequest(new { success = false, message = "Endpoint URL is not allowed. Internal network addresses are blocked." });

            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var testType = (integration.Type ?? "").ToLowerInvariant();
            if (testType is "http" or "rest" or "api" or "webhook" or "")
            {
                var response = await client.SendAsync(
                    new HttpRequestMessage(HttpMethod.Head, integration.EndpointUrl), cts.Token);

                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                    return Ok(new { success = true, message = $"Connection to '{integration.Name}' is reachable (HTTP {(int)response.StatusCode})." });

                return UnprocessableEntity(new { success = false, message = $"Endpoint returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}." });
            }

            return Ok(new { success = true, message = $"Connection to '{integration.Name}' validated (type: {integration.Type})." });
        }
        catch (TaskCanceledException)
        {
            return UnprocessableEntity(new { success = false, message = "Connection test timed out after 10 seconds." });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Integration connection test failed for {IntegrationId}", id);
            return UnprocessableEntity(new { success = false, message = $"Connection failed: {ex.Message}" });
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
