using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;
using R2WAI.Infrastructure.AI.DynamicTools;
using R2WAI.Infrastructure.Security;

namespace R2WAI.Api.Controllers;

/// <summary>
/// Tenant-owned MCP server allowlist + the discover-then-commit two-step for turning a server's
/// advertised tools into governed <see cref="ToolDefinition"/> rows — mirrors
/// <see cref="IntegrationsController"/>'s OpenAPI analyze/commit flow exactly (implementation plan
/// Phase 3), except committed rows land <b>inactive</b>: an MCP server can advertise arbitrary,
/// server-controlled tools, so nothing becomes agent-callable until an admin reviews and activates
/// it explicitly (see <see cref="Commit"/>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/mcp-connections")]
public class McpConnectionsController(
    IRepository<McpServerConnection> connections,
    IRepository<ToolDefinition> toolDefinitions,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    McpClientAdapter mcpClient,
    IUnitOfWork unitOfWork,
    ILogger<McpConnectionsController> logger) : ControllerBase
{
    private Guid TenantId => currentUser.TenantId ?? throw new UnauthorizedException();

    public record CreateMcpConnectionRequest(string Name, string EndpointUrl, string? AuthHeaderName, string? Credential);
    public record UpdateMcpConnectionRequest(string Name, string EndpointUrl, string? AuthHeaderName, string? Credential);
    public record McpToolCandidateResponse(string Name, string? Description);
    public record CommitMcpToolsRequest(List<McpToolCandidateResponse> Tools);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var list = await connections.FindAsync(c => c.TenantId == tenantId, ct);
        return Ok(list.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var connection = await GetOwnedConnectionAsync(id, ct);
        return Ok(ToDto(connection));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMcpConnectionRequest request, CancellationToken ct = default)
    {
        if (!EgressGuard.IsAllowedUrl(request.EndpointUrl))
            throw new ValidationException(nameof(request.EndpointUrl), "This URL is not allowed. Internal network addresses are blocked.");

        var credentialEncrypted = string.IsNullOrWhiteSpace(request.Credential)
            ? null
            : encryptionService.Encrypt(request.Credential);

        var connection = new McpServerConnection(
            Guid.NewGuid(), TenantId, request.Name, request.EndpointUrl, request.AuthHeaderName, credentialEncrypted);

        await connections.AddAsync(connection, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Created MCP server connection '{Name}'", connection.Name);
        return CreatedAtAction(nameof(GetById), new { id = connection.Id }, ToDto(connection));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMcpConnectionRequest request, CancellationToken ct = default)
    {
        if (!EgressGuard.IsAllowedUrl(request.EndpointUrl))
            throw new ValidationException(nameof(request.EndpointUrl), "This URL is not allowed. Internal network addresses are blocked.");

        var connection = await GetOwnedConnectionAsync(id, ct);

        // Blank Credential in an update means "leave the stored one alone" — the client never gets the
        // decrypted value back (GetById/ToDto never returns it), so it has no old value to resubmit.
        var credentialEncrypted = string.IsNullOrWhiteSpace(request.Credential)
            ? connection.CredentialEncrypted
            : encryptionService.Encrypt(request.Credential);

        connection.Update(request.Name, request.EndpointUrl, request.AuthHeaderName, credentialEncrypted);
        await unitOfWork.SaveChangesAsync(ct);
        return Ok(ToDto(connection));
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct = default)
    {
        var connection = await GetOwnedConnectionAsync(id, ct);
        if (connection.IsActive) connection.Deactivate(); else connection.Activate();
        await unitOfWork.SaveChangesAsync(ct);
        return Ok(new { id, isActive = connection.IsActive });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var connection = await GetOwnedConnectionAsync(id, ct);
        connections.Delete(connection);
        await unitOfWork.SaveChangesAsync(ct);
        return NoContent();
    }

    // Live connectivity check, same role as IntegrationsController.Test — records a real, persisted
    // Connected/Error status rather than an unconditional fake success.
    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct = default)
    {
        var connection = await GetOwnedConnectionAsync(id, ct);
        var credential = DecryptCredentialOrNull(connection);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            var tools = await mcpClient.DiscoverToolsAsync(connection.EndpointUrl, connection.AuthHeaderName, credential, cts.Token);

            connection.RecordTestResult(success: true);
            await unitOfWork.SaveChangesAsync(ct);
            return Ok(new { success = true, message = $"Connection to '{connection.Name}' succeeded.", toolCount = tools.Count });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP connection test failed for {ConnectionId}", id);
            connection.RecordTestResult(success: false);
            await unitOfWork.SaveChangesAsync(ct);
            return UnprocessableEntity(new { success = false, message = ex.Message });
        }
    }

    // Analyze step — lists the server's advertised tools for admin review. Nothing is persisted here;
    // see Commit for the step that actually creates governed ToolDefinition rows.
    [HttpGet("{id:guid}/discover")]
    public async Task<IActionResult> Discover(Guid id, CancellationToken ct = default)
    {
        var connection = await GetOwnedConnectionAsync(id, ct);
        var credential = DecryptCredentialOrNull(connection);

        var candidates = await mcpClient.DiscoverToolsAsync(connection.EndpointUrl, connection.AuthHeaderName, credential, ct);
        return Ok(candidates.Select(c => new McpToolCandidateResponse(c.Name, c.Description)));
    }

    // Commit step — creates one inactive ToolDefinition per selected tool. Deliberately stricter than
    // IntegrationsController's OpenAPI commit (which lands active): an MCP server's tool list is
    // server-controlled and can change or expand at any time, so nothing here becomes agent-callable
    // until an admin reviews and explicitly activates it (Toggle on IntegrationsController, which
    // already governs every ToolType including Mcp).
    [HttpPost("{id:guid}/commit")]
    public async Task<IActionResult> Commit(Guid id, [FromBody] CommitMcpToolsRequest request, CancellationToken ct = default)
    {
        var connection = await GetOwnedConnectionAsync(id, ct);
        var tenantId = TenantId;

        var ids = new List<Guid>();
        foreach (var tool in request.Tools)
        {
            var toolDef = new ToolDefinition(Guid.NewGuid(), tenantId, tool.Name, ToolType.Mcp, tool.Description);
            toolDef.LinkMcpServer(connection.Id, tool.Name);
            toolDef.Deactivate();

            await toolDefinitions.AddAsync(toolDef, ct);
            ids.Add(toolDef.Id);
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Committed {Count} MCP tools from connection '{Connection}'", ids.Count, connection.Name);
        return Ok(new { ids });
    }

    private async Task<McpServerConnection> GetOwnedConnectionAsync(Guid id, CancellationToken ct)
    {
        var connection = await connections.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(McpServerConnection), id);
        if (connection.TenantId != TenantId)
            throw new UnauthorizedException();
        return connection;
    }

    private string? DecryptCredentialOrNull(McpServerConnection connection)
    {
        if (string.IsNullOrEmpty(connection.CredentialEncrypted))
            return null;
        try
        {
            return encryptionService.Decrypt(connection.CredentialEncrypted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decrypt stored credential for McpServerConnection {ConnectionId}", connection.Id);
            return null;
        }
    }

    // Never includes the encrypted credential — this is the shape returned to the client, and the
    // client should never see even the ciphertext, only whether one is configured.
    private static object ToDto(McpServerConnection c) => new
    {
        c.Id,
        c.Name,
        c.EndpointUrl,
        c.AuthHeaderName,
        HasCredential = !string.IsNullOrEmpty(c.CredentialEncrypted),
        c.IsActive,
        c.LastTestStatus,
        c.LastTestedAt,
    };
}
