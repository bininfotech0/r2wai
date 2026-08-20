using FluentValidation;
using R2WAI.Domain.Enums;

namespace R2WAI.Application.Common.Validation;

public static class ValidationExtensions
{
    /// <summary>
    /// Requires an absolute http/https URL. Used for fields that go on to make real outbound
    /// calls (an ApplicationApi's BaseUrl, an AI model's Endpoint) — previously these only had a
    /// MaximumLength check or no check at all, so anything from an empty scheme to a non-http
    /// URI scheme reached the point of an actual HTTP call unvalidated.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MustBeValidHttpUrl<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder.Must(url =>
            string.IsNullOrEmpty(url) ||
            (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        .WithMessage("Must be a valid absolute http:// or https:// URL.");

    /// <summary>
    /// Only "Ollama" runs fully self-hosted; every other provider option ships prompts/documents
    /// off-premises to a third-party API. DLP boundary: Confidential/Restricted-classified model
    /// configs must stay on a local provider, or citizen/government data leaves the tenant.
    /// </summary>
    private static readonly string[] LocalOnlyProviders = ["Ollama"];

    public static bool ViolatesDataClassificationBoundary(string? dataClassification, string? provider) =>
        Enum.TryParse<DataClassification>(dataClassification, true, out var classification) &&
        classification is DataClassification.Confidential or DataClassification.Restricted &&
        !string.IsNullOrEmpty(provider) &&
        !LocalOnlyProviders.Contains(provider, StringComparer.OrdinalIgnoreCase);
}
