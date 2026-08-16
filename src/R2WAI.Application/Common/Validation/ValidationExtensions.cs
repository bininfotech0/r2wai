using FluentValidation;

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
}
