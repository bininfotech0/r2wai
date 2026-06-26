using System.ClientModel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Admin.Commands;
using R2WAI.Application.Features.Admin.Queries;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SystemAdmin")]
[Route("api/v1/[controller]")]
public class AdminController(IMediator mediator, ApplicationDbContext dbContext, IEncryptionService encryptionService, ILogger<AdminController> logger) : ControllerBase
{
    private static (int page, int pageSize) ClampPagination(int page, int pageSize, int maxPageSize = 100)
    {
        return (Math.Max(1, page), Math.Clamp(pageSize, 1, maxPageSize));
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        (page, pageSize) = ClampPagination(page, pageSize);
        var query = new GetUsersQuery { Page = page, PageSize = pageSize };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating user: {Email}", command.Email);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction("GetUsers", new { id = result.Id }, result);
    }

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteUserCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    public record InviteUserRequest(string Email, string? Role = null);

    [HttpPost("users/invite")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request, CancellationToken ct = default)
    {
        var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
        var currentUser = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.ICurrentUserService>();

        var tokenBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var inviteToken = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var inviter = await dbContext.Users.FindAsync([currentUser.UserId], ct);
        var tenant = await dbContext.Tenants.FindAsync([currentUser.TenantId], ct);

        await emailService.SendUserInviteAsync(
            request.Email,
            inviter is not null ? $"{inviter.FirstName} {inviter.LastName}" : "Admin",
            tenant?.Name ?? "R2WAI",
            inviteToken, ct);

        logger.LogInformation("User invitation sent to {Email}", request.Email);
        return Ok(new { message = $"Invitation sent to {request.Email}" });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        (page, pageSize) = ClampPagination(page, pageSize);
        var query = new GetRolesQuery { Page = page, PageSize = pageSize };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating role: {Name}", command.Name);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction("GetRoles", new { id = result.Id }, result);
    }

    [HttpPut("roles/{id:guid}")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("roles/{id:guid}")]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteRoleCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        CancellationToken ct = default)
    {
        (page, pageSize) = ClampPagination(page, pageSize);
        var query = new GetAuditLogsQuery
        {
            Page = page,
            PageSize = pageSize,
            UserId = userId,
            EntityType = entityType
        };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct = default)
    {
        var query = new GetSettingsQuery();
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsCommand command, CancellationToken ct = default)
    {
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("models")]
    public async Task<IActionResult> GetModels([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        (page, pageSize) = ClampPagination(page, pageSize);
        var query = new GetModelsQuery { Page = page, PageSize = pageSize };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("models")]
    public async Task<IActionResult> CreateModel([FromBody] CreateModelCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Creating model: {Name}", command.Name);
        var result = await mediator.Send(command, ct);
        return CreatedAtAction("GetModels", new { id = result.Id }, result);
    }

    [HttpPut("models/{id:guid}")]
    public async Task<IActionResult> UpdateModel(Guid id, [FromBody] UpdateModelCommand command, CancellationToken ct = default)
    {
        command = command with { Id = id };
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("models/{id:guid}")]
    public async Task<IActionResult> DeleteModel(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteModelCommand { Id = id };
        await mediator.Send(command, ct);
        return NoContent();
    }

    public record SetApiKeyRequest(string ApiKey);

    [HttpPut("models/{id:guid}/api-key")]
    public async Task<IActionResult> SetModelApiKey(Guid id, [FromBody] SetApiKeyRequest request, CancellationToken ct = default)
    {
        var model = await dbContext.ModelConfigurations
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (model is null)
            return NotFound(new { error = "Model not found" });

        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return BadRequest(new { error = "API key is required" });

        model.SetApiKey(encryptionService.Encrypt(request.ApiKey));
        await dbContext.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "API key updated" });
    }

    [HttpDelete("models/{id:guid}/api-key")]
    public async Task<IActionResult> RemoveModelApiKey(Guid id, CancellationToken ct = default)
    {
        var model = await dbContext.ModelConfigurations
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (model is null)
            return NotFound(new { error = "Model not found" });

        model.SetApiKey(string.Empty);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "API key removed" });
    }

    [HttpPost("models/{id:guid}/test")]
    public async Task<IActionResult> TestModelConnection(Guid id, CancellationToken ct = default)
    {
        var model = await dbContext.ModelConfigurations
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (model is null)
            return NotFound(new { error = "Model not found" });

        try
        {
            var builder = Kernel.CreateBuilder();
            var apiKey = !string.IsNullOrEmpty(model.ApiKeyEncrypted)
                ? encryptionService.Decrypt(model.ApiKeyEncrypted)
                : string.Empty;

            var provider = (model.Provider ?? "").ToLowerInvariant();
            if (provider == "ollama")
            {
                if (string.IsNullOrEmpty(model.Endpoint))
                    return UnprocessableEntity(new { success = false, message = "Ollama endpoint not configured for this model." });
                var endpoint = model.Endpoint;
                var ollamaUri = new Uri($"{endpoint.TrimEnd('/')}/v1");
                var ollamaClient = new OpenAIClient(new ApiKeyCredential("ollama"), new OpenAIClientOptions { Endpoint = ollamaUri });
                builder.AddOpenAIChatCompletion(model.ModelId, ollamaClient);
            }
            else if (provider is "openai" or "azureopenai")
            {
                if (string.IsNullOrEmpty(apiKey))
                    return UnprocessableEntity(new { success = false, message = "No API key configured for this model." });

                if (!string.IsNullOrEmpty(model.Endpoint))
                {
                    var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(model.Endpoint) });
                    builder.AddOpenAIChatCompletion(model.ModelId, client);
                }
                else
                    builder.AddOpenAIChatCompletion(model.ModelId, apiKey);
            }
            else
            {
                return UnprocessableEntity(new { success = false, message = $"Unsupported provider: {model.Provider}" });
            }

            var kernel = builder.Build();
            var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a test assistant.");
            chatHistory.AddUserMessage("Say 'OK' in one word.");

            var result = await chatCompletion.GetChatMessageContentAsync(chatHistory, cancellationToken: ct);
            return Ok(new { success = true, message = "Connection successful", response = result.Content });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Model connection test failed for {ModelId}", id);
            return UnprocessableEntity(new { success = false, message = $"Connection failed: {ex.Message}" });
        }
    }

    public record ContentModerationSettings
    {
        public string ModerationLevel { get; init; } = "Medium";
        public ContentModerationRules Rules { get; init; } = new();
        public List<string> BlockedKeywords { get; init; } = [];
        public ContentModerationTemplates Templates { get; init; } = new();
    }

    public record ContentModerationRules
    {
        public bool BlockHateSpeech { get; init; } = true;
        public bool BlockPersonalData { get; init; } = true;
        public bool BlockCodeExecution { get; init; }
        public bool RequireCitations { get; init; }
        public bool BlockCompetitorMentions { get; init; }
        public bool FlagFinancialAdvice { get; init; } = true;
        public bool FlagMedicalLegalAdvice { get; init; } = true;
    }

    public record ContentModerationTemplates
    {
        public string BlockedResponse { get; init; } = "I'm unable to provide that information. Please contact your administrator.";
        public string FlaggedWarning { get; init; } = "This response may contain sensitive information. Please verify before sharing.";
    }

    [HttpGet("content-moderation")]
    public async Task<IActionResult> GetContentModeration(CancellationToken ct = default)
    {
        var currentUser = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.ICurrentUserService>();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        var tenant = await dbContext.Tenants.FindAsync([tenantId], ct);
        if (tenant is null) return NotFound();

        ContentModerationSettings? settings = null;
        if (!string.IsNullOrEmpty(tenant.Settings))
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(tenant.Settings);
                if (doc.RootElement.TryGetProperty("contentModeration", out var cm))
                    settings = System.Text.Json.JsonSerializer.Deserialize<ContentModerationSettings>(cm.GetRawText(),
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }

        return Ok(settings ?? new ContentModerationSettings());
    }

    [HttpPost("content-moderation")]
    public async Task<IActionResult> SaveContentModeration([FromBody] ContentModerationSettings settings, CancellationToken ct = default)
    {
        var currentUser = HttpContext.RequestServices.GetRequiredService<R2WAI.Application.Common.Interfaces.ICurrentUserService>();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();

        var tenant = await dbContext.Tenants.FindAsync([tenantId], ct);
        if (tenant is null) return NotFound();

        var existingSettings = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(tenant.Settings))
        {
            try
            {
                existingSettings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(tenant.Settings) ?? new();
            }
            catch { }
        }

        existingSettings["contentModeration"] = settings;
        var newSettings = System.Text.Json.JsonSerializer.Serialize(existingSettings,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        tenant.UpdateSettings(newSettings);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Content moderation settings updated for tenant {TenantId}", tenantId);
        return Ok(settings);
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics(CancellationToken ct = default)
    {
        var query = new GetAnalyticsQuery();
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("analytics/usage")]
    public async Task<IActionResult> GetUsageAnalytics([FromQuery] int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-days);

        var assistantTask = dbContext.AssistantDefinitions
            .Select(a => new { a.Id, a.IsActive }).ToListAsync(ct);
        var convTask = dbContext.Conversations.CountAsync(c => c.CreatedAt >= since, ct);
        var msgTask = dbContext.Messages.CountAsync(m => m.CreatedAt >= since, ct);
        var wfTotalTask = dbContext.WorkflowInstances.CountAsync(w => w.CreatedAt >= since, ct);
        var wfCompletedTask = dbContext.WorkflowInstances.CountAsync(
            w => w.Status == Domain.Enums.WorkflowInstanceStatus.Completed && w.CreatedAt >= since, ct);
        var wfFailedTask = dbContext.WorkflowInstances.CountAsync(
            w => w.Status == Domain.Enums.WorkflowInstanceStatus.Failed && w.CreatedAt >= since, ct);
        var aprTotalTask = dbContext.ApprovalRequests.CountAsync(a => a.CreatedAt >= since, ct);
        var aprApprovedTask = dbContext.ApprovalRequests.CountAsync(
            a => a.Status == Domain.Enums.ApprovalStatus.Approved && a.CreatedAt >= since, ct);
        var aprPendingTask = dbContext.ApprovalRequests.CountAsync(
            a => a.Status == Domain.Enums.ApprovalStatus.Pending, ct);
        var docTask = dbContext.Documents.CountAsync(d => d.CreatedAt >= since, ct);

        await Task.WhenAll(assistantTask, convTask, msgTask, wfTotalTask, wfCompletedTask,
            wfFailedTask, aprTotalTask, aprApprovedTask, aprPendingTask, docTask);

        var assistantList = await assistantTask;

        return Ok(new
        {
            Period = $"Last {days} days",
            Assistants = new { Total = assistantList.Count, Active = assistantList.Count(a => a.IsActive) },
            Conversations = new { Total = await convTask, Messages = await msgTask },
            Workflows = new { Executions = await wfTotalTask, Completed = await wfCompletedTask, Failed = await wfFailedTask },
            Approvals = new { Total = await aprTotalTask, Approved = await aprApprovedTask, Pending = await aprPendingTask },
            Documents = new { Uploaded = await docTask }
        });
    }
}
