using System.Diagnostics;

namespace R2WAI.Application.Common;

public static class DiagnosticsConfig
{
    public const string ServiceName = "R2WAI";
    public const string ServiceVersion = "1.0.0";
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);
}
