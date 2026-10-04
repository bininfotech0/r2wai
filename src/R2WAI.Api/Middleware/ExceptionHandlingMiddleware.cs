using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Exceptions;

namespace R2WAI.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly bool _isDevelopment;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _isDevelopment = env.IsDevelopment() || env.IsEnvironment("Testing");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // The client hung up (navigated away, closed the tab, or the SPA aborted an
            // in-flight query on unmount). There is nobody left to send a response to, and this
            // is not a server fault — so don't let it fall through to the 500 branch below and
            // get logged as "Unhandled exception occurred". Doing so flooded the error log with
            // routine navigation and would trip any error-rate alert on a perfectly healthy API.
            if (ex is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug("Request {Method} {Path} aborted by the client.", context.Request.Method, context.Request.Path);
                return;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        HttpStatusCode statusCode;
        object problemDetails;

        switch (exception)
        {
            case NotFoundException notFound:
                statusCode = HttpStatusCode.NotFound;
                problemDetails = new { Status = 404, Title = "Not Found", Detail = notFound.Message, Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4" };
                break;

            case ValidationException validation:
                statusCode = HttpStatusCode.BadRequest;
                problemDetails = new { Status = 400, Title = "Validation Failed", Detail = validation.Message, Errors = validation.Errors, Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1" };
                break;

            case UnauthorizedException unauthorized:
                statusCode = HttpStatusCode.Unauthorized;
                problemDetails = new { Status = 401, Title = "Unauthorized", Detail = unauthorized.Message, Type = "https://tools.ietf.org/html/rfc7235#section-3.1" };
                break;

            case UnauthorizedAccessException accessDenied:
                statusCode = HttpStatusCode.Forbidden;
                problemDetails = new { Status = 403, Title = "Forbidden", Detail = "You do not have permission to perform this action.", Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3" };
                break;

            // ObjectDisposedException derives from InvalidOperationException in .NET, but it's
            // never a legitimate client-facing conflict — it always means a resource (e.g. a
            // connection) was disposed while still in use, which is a real bug. Must be checked
            // ahead of the InvalidOperationException case below, or it gets misclassified as a
            // clean 409 and the underlying bug goes unnoticed.
            case ObjectDisposedException:
                goto default;

            // Domain guards (e.g. entity.EnsureMutable()) throw this to signal an invalid state
            // transition — a client-correctable conflict, not a server fault.
            case InvalidOperationException invalidOp:
                statusCode = HttpStatusCode.Conflict;
                problemDetails = new { Status = 409, Title = "Conflict", Detail = invalidOp.Message, Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8" };
                break;

            // Unique-constraint violations reach here when a handler doesn't pre-check for
            // an existing row (e.g. duplicate application Code) — surface as 409, not 500.
            case DbUpdateException dbUpdate when IsUniqueConstraintViolation(dbUpdate):
                statusCode = HttpStatusCode.Conflict;
                problemDetails = new { Status = 409, Title = "Conflict", Detail = "A record with the same unique value already exists.", Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8" };
                break;

            // A save affected 0 rows where 1 was expected — the record was changed or removed
            // by another operation on the same request/DbContext since it was loaded (observed on
            // long-running AI chat turns where a tool call mid-stream saves the shared DbContext
            // before the outer handler's own final save runs). The request genuinely failed and
            // should be retried, not silently treated as success — 409, not a masked 200 or a raw 500.
            case DbUpdateConcurrencyException:
                statusCode = HttpStatusCode.Conflict;
                problemDetails = new { Status = 409, Title = "Conflict", Detail = "This record was changed by another operation while your request was processing. Please retry.", Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8" };
                break;

            // A required setting is missing or invalid (e.g. no ENCRYPTION_KEY configured) — a
            // deployment problem, not something the caller did wrong or can fix by retrying.
            // Genuinely a 500 (falls through to the same status as default), but logged as
            // Critical below rather than Error so it's immediately recognizable as an ops/config
            // issue instead of something to debug in application code.
            case ConfigurationException configEx:
                statusCode = HttpStatusCode.InternalServerError;
                problemDetails = new { Status = 500, Title = "Internal Server Error", Detail = _isDevelopment ? configEx.Message : "The server is misconfigured. Contact your administrator.", Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1" };
                break;

            // A downstream dependency (e.g. the AI provider) didn't respond in time — its own
            // HttpClient timeout fired. Distinct from the client itself disconnecting, which
            // cancels context.RequestAborted and can't be responded to anyway.
            case OperationCanceledException when !context.RequestAborted.IsCancellationRequested:
                statusCode = HttpStatusCode.GatewayTimeout;
                problemDetails = new { Status = 504, Title = "Gateway Timeout", Detail = "The request took too long to complete (an upstream service did not respond in time). Please try again.", Type = "https://tools.ietf.org/html/rfc7231#section-6.6.5" };
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                var detail = _isDevelopment
                    ? $"{exception.GetType().Name}: {exception.Message}"
                    : "An unexpected error occurred.";
                problemDetails = new { Status = 500, Title = "Internal Server Error", Detail = detail, Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1" };
                break;
        }

        if (exception is ConfigurationException)
        {
            _logger.LogCritical(exception, "Configuration error: {Message}", exception.Message);
        }
        else if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred");
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception: {ExceptionType}", exception.GetType().Name);
        }

        var correlationId = context.Items.TryGetValue("CorrelationId", out var cid) ? cid?.ToString() : null;
        correlationId ??= context.TraceIdentifier;

        // A streaming (SSE) response already flushed at least one chunk before this exception was
        // thrown — headers are sent, and setting StatusCode/ContentType now would itself throw
        // (response already started), masking the real exception with a confusing new one. The
        // three SSE controller actions each now catch a provider failure at the source and write
        // their own graceful "error" event instead of letting it reach here at all; this guard is
        // the remaining defense-in-depth for any other exception that manages to occur mid-stream.
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(exception, "Exception occurred after the response had already started ({Path}) — cannot write a problem-details body.", context.Request.Path);
            return;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        var enriched = new Dictionary<string, object?>
        {
            ["status"] = (int)statusCode,
            ["title"] = problemDetails.GetType().GetProperty("Title")?.GetValue(problemDetails),
            ["detail"] = problemDetails.GetType().GetProperty("Detail")?.GetValue(problemDetails),
            ["type"] = problemDetails.GetType().GetProperty("Type")?.GetValue(problemDetails),
            ["correlationId"] = correlationId,
        };

        if (problemDetails.GetType().GetProperty("Errors")?.GetValue(problemDetails) is { } errors)
            enriched["errors"] = errors;

        var json = JsonSerializer.Serialize(enriched, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });

        await context.Response.WriteAsync(json);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase) == true;
}
