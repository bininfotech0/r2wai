using System.Net;
using System.Net.Mail;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendApprovalRequestAsync(string toEmail, string toName, string workflowName,
        string requesterName, string? details, Guid approvalId, CancellationToken ct = default)
    {
        var subject = $"[R2WAI] Approval Required: {workflowName}";
        var body = $"""
            <h2>Approval Request</h2>
            <p>Hello {toName},</p>
            <p><strong>{requesterName}</strong> has submitted a request that requires your approval.</p>
            <table style="border-collapse:collapse;margin:16px 0;">
                <tr><td style="padding:4px 12px 4px 0;font-weight:bold;">Workflow:</td><td>{workflowName}</td></tr>
                <tr><td style="padding:4px 12px 4px 0;font-weight:bold;">Requested By:</td><td>{requesterName}</td></tr>
                {(details is not null ? $"<tr><td style=\"padding:4px 12px 4px 0;font-weight:bold;\">Details:</td><td>{details}</td></tr>" : "")}
                <tr><td style="padding:4px 12px 4px 0;font-weight:bold;">Request ID:</td><td>{approvalId}</td></tr>
            </table>
            <p>Please log in to R2WAI to approve or reject this request.</p>
            <p style="color:#888;font-size:12px;">This is an automated message from R2WAI.</p>
            """;

        await SendEmailAsync(toEmail, subject, body, ct);
    }

    public async Task SendApprovalDecisionAsync(string toEmail, string toName, string workflowName,
        bool approved, string? comments, CancellationToken ct = default)
    {
        var decision = approved ? "Approved" : "Rejected";
        var subject = $"[R2WAI] Request {decision}: {workflowName}";
        var body = $"""
            <h2>Request {decision}</h2>
            <p>Hello {toName},</p>
            <p>Your request for <strong>{workflowName}</strong> has been <strong>{decision.ToLower()}</strong>.</p>
            {(comments is not null ? $"<p><strong>Comments:</strong> {comments}</p>" : "")}
            <p>Please log in to R2WAI for details.</p>
            <p style="color:#888;font-size:12px;">This is an automated message from R2WAI.</p>
            """;

        await SendEmailAsync(toEmail, subject, body, ct);
    }

    public async Task SendEscalationAsync(string toEmail, string toName, string workflowName,
        int escalationLevel, Guid approvalId, CancellationToken ct = default)
    {
        var subject = $"[R2WAI] ESCALATED: Approval Required for {workflowName}";
        var body = $"""
            <h2>Escalated Approval Request</h2>
            <p>Hello {toName},</p>
            <p>An approval request for <strong>{workflowName}</strong> has been escalated to you (Level {escalationLevel}).</p>
            <p>The original approver did not respond within the SLA. Please review and take action.</p>
            <p>Please log in to R2WAI to approve or reject this request.</p>
            <p style="color:#888;font-size:12px;">This is an automated message from R2WAI.</p>
            """;

        await SendEmailAsync(toEmail, subject, body, ct);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetToken, CancellationToken ct = default)
    {
        var appUrl = _configuration["App:BaseUrl"] ?? "http://localhost:8080";
        var subject = "[R2WAI] Password Reset Request";
        var body = $"""
            <h2>Password Reset</h2>
            <p>Hello {toName},</p>
            <p>A password reset was requested for your R2WAI account.</p>
            <p>Use this code to reset your password: <strong>{resetToken}</strong></p>
            <p>This code expires in 1 hour. If you did not request this, ignore this email.</p>
            <p style="color:#888;font-size:12px;">This is an automated message from R2WAI.</p>
            """;

        await SendEmailAsync(toEmail, subject, body, ct);
    }

    public async Task SendUserInviteAsync(string toEmail, string inviterName, string tenantName,
        string inviteToken, CancellationToken ct = default)
    {
        var subject = $"[R2WAI] You've been invited to {tenantName}";
        var body = $"""
            <h2>You're Invited!</h2>
            <p><strong>{inviterName}</strong> has invited you to join <strong>{tenantName}</strong> on R2WAI.</p>
            <p>Use this invitation code to create your account: <strong>{inviteToken}</strong></p>
            <p>This invitation expires in 7 days.</p>
            <p style="color:#888;font-size:12px;">This is an automated message from R2WAI.</p>
            """;

        await SendEmailAsync(toEmail, subject, body, ct);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        if (string.IsNullOrEmpty(smtpHost))
        {
            _logger.LogWarning("SMTP not configured — email to {To} with subject '{Subject}' was not sent", to, subject);
            return;
        }

        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var smtpUser = _configuration["Email:SmtpUser"];
        var smtpPassword = _configuration["Email:SmtpPassword"];
        var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@r2wai.com";
        var fromName = _configuration["Email:FromName"] ?? "R2WAI Platform";
        var enableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true");

        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(fromAddress, fromName);
            message.To.Add(to);
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = enableSsl,
                Timeout = 15000
            };

            if (!string.IsNullOrEmpty(smtpUser))
                client.Credentials = new NetworkCredential(smtpUser, smtpPassword);

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
        }
    }
}
