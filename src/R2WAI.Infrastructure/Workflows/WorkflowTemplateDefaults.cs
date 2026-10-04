using R2WAI.Application.Features.Workflows.DTOs;

namespace R2WAI.Infrastructure.Workflows;

/// <summary>
/// The 5 starter templates WorkflowsController.GetTemplates used to return as hardcoded anonymous
/// objects (docs/api/MISSING-BACKEND-ENDPOINTS.md §3.7 #82) — moved here, unchanged, so a tenant
/// override (WorkflowTemplateOverride) has a real default to fall back to, the same relationship
/// SystemPromptTemplates has with PromptTemplate.
/// </summary>
public static class WorkflowTemplateDefaults
{
    public static List<WorkflowTemplateDto> GetAll() =>
    [
        new("invoice-approval", "Invoice Approval",
            "Three-level invoice approval workflow with amount-based routing", "Approval",
            [
                new("Submit Invoice", "Action", "Submitter", 0),
                new("Manager Approval", "Approval", "Manager", 1),
                new("Finance Review", "Approval", "Finance", 2),
                new("Process Payment", "API Call", "System", 3),
                new("Send Confirmation", "Email", "System", 4),
            ]),
        new("purchase-request", "Purchase Request",
            "Purchase order request with budget check and approval", "Approval",
            [
                new("Submit Request", "Action", "Requester", 0),
                new("Budget Check", "AI Generate", "System", 1),
                new("Manager Approval", "Approval", "Manager", 2),
                new("Procurement Review", "Approval", "Procurement", 3),
                new("Create PO", "API Call", "System", 4),
            ]),
        new("employee-onboarding", "Employee Onboarding",
            "New employee onboarding workflow with IT and HR tasks", "Process",
            [
                new("HR Intake", "Action", "HR", 0),
                new("Generate Welcome Pack", "AI Generate", "System", 1),
                new("IT Setup Request", "API Call", "IT", 2),
                new("Manager Introduction", "Email", "System", 3),
                new("HR Approval", "Approval", "HR", 4),
            ]),
        new("travel-request", "Travel Request",
            "Travel approval with policy check and booking", "Approval",
            [
                new("Submit Travel Request", "Action", "Employee", 0),
                new("Policy Check", "AI Generate", "System", 1),
                new("Manager Approval", "Approval", "Manager", 2),
                new("Book Travel", "API Call", "System", 3),
            ]),
        new("vendor-approval", "Vendor Approval",
            "New vendor onboarding with compliance and legal review", "Approval",
            [
                new("Submit Vendor Info", "Action", "Procurement", 0),
                new("Compliance Check", "AI Generate", "System", 1),
                new("Legal Review", "Approval", "Legal", 2),
                new("Finance Approval", "Approval", "Finance", 3),
                new("Register Vendor", "API Call", "System", 4),
            ]),
    ];
}
