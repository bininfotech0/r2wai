using R2WAI.Application.Features.Workflows.DTOs;

namespace R2WAI.Infrastructure.Workflows;

/// <summary>
/// The 5 starter templates WorkflowsController.GetTemplates used to return as hardcoded anonymous
/// objects (docs/api/MISSING-BACKEND-ENDPOINTS.md §3.7 #82) — moved here, unchanged, so a tenant
/// override (WorkflowTemplateOverride) has a real default to fall back to, the same relationship
/// SystemPromptTemplates has with PromptTemplate.
/// Every step's Action is a real executable step type (StepActivityFactory). The old leading
/// "Submit …" steps were type "Action" — a no-op that only logged a line, standing in for what is
/// really the trigger — and were removed 2026-10-16.
/// </summary>
public static class WorkflowTemplateDefaults
{
    public static List<WorkflowTemplateDto> GetAll() =>
    [
        new("invoice-approval", "Invoice Approval",
            "Three-level invoice approval workflow with amount-based routing", "Approval",
            [
                new("Manager Approval", "Approval", "Manager", 0),
                new("Finance Review", "Approval", "Finance", 1),
                new("Process Payment", "API Call", "System", 2),
                new("Send Confirmation", "Email", "System", 3),
            ]),
        new("purchase-request", "Purchase Request",
            "Purchase order request with budget check and approval", "Approval",
            [
                new("Budget Check", "AI Generate", "System", 0),
                new("Manager Approval", "Approval", "Manager", 1),
                new("Procurement Review", "Approval", "Procurement", 2),
                new("Create PO", "API Call", "System", 3),
            ]),
        new("employee-onboarding", "Employee Onboarding",
            "New employee onboarding workflow with IT and HR tasks", "Process",
            [
                new("Generate Welcome Pack", "AI Generate", "System", 0),
                new("IT Setup Request", "API Call", "IT", 1),
                new("Manager Introduction", "Email", "System", 2),
                new("HR Approval", "Approval", "HR", 3),
            ]),
        new("travel-request", "Travel Request",
            "Travel approval with policy check and booking", "Approval",
            [
                new("Policy Check", "AI Generate", "System", 0),
                new("Manager Approval", "Approval", "Manager", 1),
                new("Book Travel", "API Call", "System", 2),
            ]),
        new("vendor-approval", "Vendor Approval",
            "New vendor onboarding with compliance and legal review", "Approval",
            [
                new("Compliance Check", "AI Generate", "System", 0),
                new("Legal Review", "Approval", "Legal", 1),
                new("Finance Approval", "Approval", "Finance", 2),
                new("Register Vendor", "API Call", "System", 3),
            ]),
    ];
}
