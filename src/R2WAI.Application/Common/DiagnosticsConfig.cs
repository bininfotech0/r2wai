using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace R2WAI.Application.Common;

/// <summary>
/// Shared OpenTelemetry instruments. Business metrics (AI/tool/workflow) beyond the built-in
/// ASP.NET Core/Kestrel ones — see Program.cs's WithMetrics wiring, which adds this Meter by
/// name alongside them. Traces still export console-only until OpenTelemetry:OtlpEndpoint is
/// set (see docker/docker-compose.monitoring.yml); metrics follow the same exporter.
/// </summary>
public static class DiagnosticsConfig
{
    public const string ServiceName = "R2WAI";
    public const string ServiceVersion = "1.0.0";
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    public static readonly Counter<long> AiRequests =
        Meter.CreateCounter<long>("r2wai.ai.requests", description: "AI chat/completion requests, tagged by provider and outcome.");
    public static readonly Histogram<double> AiRequestDuration =
        Meter.CreateHistogram<double>("r2wai.ai.request.duration", unit: "ms", description: "AI chat/completion request latency.");
    public static readonly Counter<long> AiTokens =
        Meter.CreateCounter<long>("r2wai.ai.tokens", description: "AI prompt/completion tokens consumed, tagged by provider and token type. Only populated where the provider reports usage (non-streaming chat today).");

    public static readonly Counter<long> ToolInvocations =
        Meter.CreateCounter<long>("r2wai.tool.invocations", description: "AI-invoked tool/function calls (built-in or dynamic), tagged by tool name and outcome.");
    public static readonly Histogram<double> ToolInvocationDuration =
        Meter.CreateHistogram<double>("r2wai.tool.invocation.duration", unit: "ms", description: "Tool/function call latency.");

    public static readonly Counter<long> WorkflowExecutions =
        Meter.CreateCounter<long>("r2wai.workflow.executions", description: "Completed workflow executions, tagged by terminal status.");
    public static readonly Histogram<double> WorkflowExecutionDuration =
        Meter.CreateHistogram<double>("r2wai.workflow.execution.duration", unit: "ms", description: "Workflow execution wall-clock duration, start to terminal status.");
}
