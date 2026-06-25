namespace R2WAI.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendApprovalRequestAsync(string toEmail, string toName, string workflowName, string requesterName, string? details, Guid approvalId, CancellationToken ct = default);
    Task SendApprovalDecisionAsync(string toEmail, string toName, string workflowName, bool approved, string? comments, CancellationToken ct = default);
    Task SendEscalationAsync(string toEmail, string toName, string workflowName, int escalationLevel, Guid approvalId, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string toName, string resetToken, CancellationToken ct = default);
    Task SendUserInviteAsync(string toEmail, string inviterName, string tenantName, string inviteToken, CancellationToken ct = default);
}
