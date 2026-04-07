using System.Net;
using System.Text.Json;
using Polly.CircuitBreaker;

namespace VisionService.Middleware;

/// <summary>Global exception handling middleware that returns RFC 7807 ProblemDetails responses.</summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Initializes a new instance of <see cref="GlobalExceptionMiddleware"/>.</summary>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Processes the HTTP request, catching any unhandled exceptions.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — ignore
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "Circuit breaker is open for {Method} {Path} — backend unavailable",
                context.Request.Method, context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                context.Response.ContentType = "application/problem+json";

                var problem = new
                {
                    type = "https://tools.ietf.org/html/rfc7807",
                    title = "Service Unavailable",
                    status = 503,
                    detail = "A backend circuit breaker is open. The service is temporarily unavailable.",
                    instance = context.Request.Path.Value
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var problem = new
                {
                    type = "https://tools.ietf.org/html/rfc7807",
                    title = "Internal Server Error",
                    status = 500,
                    detail = "An unexpected error occurred.",
                    instance = context.Request.Path.Value
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
            }
        }
    }
}
