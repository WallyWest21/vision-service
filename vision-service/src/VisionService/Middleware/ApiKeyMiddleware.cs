using Microsoft.Extensions.Options;
using VisionService.Configuration;

namespace VisionService.Middleware;

/// <summary>Middleware that enforces API key authentication when enabled.</summary>
public class ApiKeyMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";
    private readonly RequestDelegate _next;

    /// <summary>Initializes a new instance of <see cref="ApiKeyMiddleware"/>.</summary>
    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Processes the HTTP request.</summary>
    public async Task InvokeAsync(HttpContext context, IOptions<AuthOptions> authOptions)
    {
        var auth = authOptions.Value;

        // Skip auth for health, swagger, and metrics
        var path = context.Request.Path.Value ?? string.Empty;
        if (!auth.Enabled || IsExemptPath(path))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { Error = "API key required" });
            return;
        }

        var entry = auth.ApiKeys.FirstOrDefault(k => k.Key == providedKey.ToString());
        if (entry is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { Error = "Invalid API key" });
            return;
        }

        context.Items["ApiKeyEntry"] = entry;
        await _next(context);
    }

    private static bool IsExemptPath(string path) =>
        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);
}
