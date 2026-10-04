namespace R2WAI.Application.Common.Interfaces;

/// <param name="Blocked">True if the tenant's policy is configured to block on detected PII and this text contains any.</param>
/// <param name="ProcessedText">The text to actually use going forward — redacted if the policy's action is "redact" and PII was found, otherwise identical to the input.</param>
/// <param name="DetectedTypes">Distinct PII types found (e.g. "Aadhaar", "Email"), for audit logging. Empty when nothing was found or no policy is configured.</param>
public record PiiCheckResult(bool Blocked, string ProcessedText, IReadOnlyList<string> DetectedTypes);

/// <summary>
/// Reads the tenant's active "Pii" GlobalPolicy and applies its optional structured action
/// ({"action":"redact"} or {"action":"block"}) against a given piece of text before it's sent to
/// an AI provider. Additive-tightening only: a tenant with no policy configured, or one whose
/// content isn't the structured shape, always gets PiiCheckResult(false, text, []) — the exact
/// input back, unchanged.
/// </summary>
public interface IPiiPolicyService
{
    Task<PiiCheckResult> CheckAsync(string text, Guid tenantId, CancellationToken ct = default);
}
