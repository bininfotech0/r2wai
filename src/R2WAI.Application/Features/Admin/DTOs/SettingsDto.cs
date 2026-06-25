namespace R2WAI.Application.Features.Admin.DTOs;

public class SettingsDto
{
    public Guid TenantId { get; init; }
    public string? TenantSettings { get; init; }
    public string? Features { get; init; }
    public string? AiModels { get; init; }
}
