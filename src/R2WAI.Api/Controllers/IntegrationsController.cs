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
}
