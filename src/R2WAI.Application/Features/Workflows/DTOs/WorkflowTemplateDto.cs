namespace R2WAI.Application.Features.Workflows.DTOs;

public record WorkflowTemplateStepDto(string Name, string Action, string AssignedRole, int Order);

public record WorkflowTemplateDto(string Id, string Name, string? Description, string Type, List<WorkflowTemplateStepDto> Steps);
