namespace R2WAI.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _csp;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;

        _csp = configuration["Security:ContentSecurityPolicy"]
            ?? "default-src 'self'; script-src 'self' https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; img-src 'self' data: blob:; font-src 'self' https://fonts.gstatic.com; connect-src 'self' wss:; frame-ancestors 'none'; form-action 'self'; base-uri 'self'";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(self), geolocation=()";
        context.Response.Headers["Content-Security-Policy"] = _csp;
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        context.Response.Headers["X-Frame-Options"] = "DENY";

        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.Headers["Cache-Control"] = "no-store";

        await _next(context);
    }
}
