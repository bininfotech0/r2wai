namespace R2WAI.Api.Middleware;

/// <summary>
/// AuthController.Login issues a restricted token — just enough access to resolve whatever the
/// tenant's "Auth" GlobalPolicy is blocking full sign-in on — to a credential-verified user
/// instead of a normal session token, for two cases: MFA enrollment required (claim
/// "mfa_setup_pending"="true", scoped to mfa/*) and password expired (claim
/// "password_change_pending"="true", scoped to change-password). This middleware is the actual
/// enforcement of both restrictions: without it, either claim would be inert and the "restricted"
/// token would work exactly like a full session token against every [Authorize]-only endpoint.
/// Filename kept as the original MFA-only version to avoid an unrelated rename diff.
/// </summary>
public class MfaSetupScopeMiddleware
{
    // No trailing slash: PathString.StartsWithSegments treats a trailing "/" as introducing an
    // empty final segment, which fails to match e.g. "/api/v1/auth/mfa/setup" (confirmed live — a
    // trailing-slash prefix here silently blocked every mfa/* route it was meant to allow).
    private static readonly (string Claim, string[] AllowedPathPrefixes, string ErrorMessage)[] RestrictedScopes =
    [
        ("mfa_setup_pending",
            ["/api/v1/auth/mfa", "/api/v1/auth/logout"],
            "Complete MFA enrollment before accessing this resource."),
        ("password_change_pending",
            ["/api/v1/auth/change-password", "/api/v1/auth/logout"],
            "Change your password before accessing this resource."),
    ];

    private readonly RequestDelegate _next;

    public MfaSetupScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            foreach (var (claim, allowedPathPrefixes, errorMessage) in RestrictedScopes)
            {
                if (!context.User.HasClaim(claim, "true"))
                    continue;

                if (allowedPathPrefixes.Any(p => context.Request.Path.StartsWithSegments(p)))
                    break;

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = errorMessage });
                return;
            }
        }

        await _next(context);
    }
}
