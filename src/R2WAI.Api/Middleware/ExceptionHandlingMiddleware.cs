using System.Net;
using System.Text.Json;
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

            default:
                statusCode = HttpStatusCode.InternalServerError;
                var detail = _isDevelopment
                    ? $"{exception.GetType().Name}: {exception.Message}"
                    : "An unexpected error occurred.";
                problemDetails = new { Status = 500, Title = "Internal Server Error", Detail = detail, Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1" };
                break;
        }

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred");
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception: {ExceptionType}", exception.GetType().Name);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
