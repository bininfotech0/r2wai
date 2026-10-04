using System.Text.Json;
using Elsa.Http;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Models;
using R2WAI.Infrastructure.Security;

namespace R2WAI.Api.Workflows.NodeProviders;

/// <summary>
/// P0-9 (2026-09-20 audit): the workflow "API Call" step used Elsa's raw SendHttpRequest directly,
/// with no SSRF guard at all — unlike every AI-invoked/approval-resumed tool call, which now goes
/// through DynamicToolExecutor's EgressGuard check (P0-8, same session). The scope cut here is
/// deliberate: this only closes the SSRF half. A full audit trail for this step type (matching what
/// AiFunctionAuditFilter writes for governed tool calls) would need a new custom Elsa activity with
/// DI-resolved DB access — a bigger, separate change this codebase currently has no way to verify
/// end to end (every Postgres-backed regression test here, including this session's own, explicitly
/// runs in the "Testing" environment, which Program.cs skips AddElsa/real activity execution for
/// entirely) — flagged as a real, still-open follow-on rather than built blind.
///
/// The check runs at node-creation time, not inside a custom activity's ExecuteAsync: ApiUrl is
/// static per-step config (no per-run templating at this layer), and CreateActivityForStep already
/// runs fresh on every execution (see StepActivityFactory's own doc comment), so this is exactly
/// equivalent to an execution-time check for this step type, with far less new surface area.
/// </summary>
public sealed class ApiCallNodeProvider : INodeProvider
{
    public string StepType => "API Call";

    public IActivity CreateActivity(NodeCreationContext context)
    {
        var config = context.Config;

        if (!EgressGuard.IsAllowedUrl(config?.ApiUrl))
        {
            return new WriteLine(
                $"Step '{context.Step.Name}' was not run: its target address is not allowed and cannot be called.")
            { Name = context.Step.Name };
        }

        return new SendHttpRequest
        {
            Name = context.Step.Name,
            Url = new Input<Uri?>(new Uri(config?.ApiUrl ?? "about:blank", UriKind.RelativeOrAbsolute)),
            Method = new Input<string>((config?.ApiMethod ?? "GET").ToUpperInvariant()),
            Content = string.IsNullOrEmpty(config?.ApiBody) ? null! : new Input<object?>(config.ApiBody),
            RequestHeaders = new Input<HttpHeaders?>(ParseHeaders(config?.ApiHeaders))
        };
    }

    private static HttpHeaders ParseHeaders(string? rawHeadersJson)
    {
        if (string.IsNullOrWhiteSpace(rawHeadersJson))
            return new HttpHeaders();

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(rawHeadersJson) ?? [];
            var dict = parsed.ToDictionary(kv => kv.Key, kv => new[] { kv.Value });
            return new HttpHeaders(dict);
        }
        catch (JsonException)
        {
            return new HttpHeaders();
        }
    }
}
