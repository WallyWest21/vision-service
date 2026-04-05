using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using VisionService.Configuration;

namespace VisionService.Middleware;

/// <summary>Middleware that enforces per-IP rate limiting using a sliding window.</summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _entries = new();

    /// <summary>Initializes a new instance of <see cref="RateLimitMiddleware"/>.</summary>
    public RateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Processes the HTTP request, enforcing rate limits per client IP.</summary>
    public async Task InvokeAsync(HttpContext context, IOptions<RateLimitOptions> options)
    {
        var opts = options.Value;
        var path = context.Request.Path.Value ?? string.Empty;

        // Exempt health, swagger, and metrics from rate limiting
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var entry = _entries.GetOrAdd(ip, _ => new RateLimitEntry());

        if (!entry.TryConsume(opts.RequestsPerMinute))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsJsonAsync(new { Error = "Rate limit exceeded. Try again later." });
            return;
        }

        await _next(context);
    }

    private sealed class RateLimitEntry
    {
        private int _count;
        private DateTime _windowStart = DateTime.UtcNow;
        private readonly object _lock = new();

        public bool TryConsume(int limit)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                if ((now - _windowStart).TotalMinutes >= 1.0)
                {
                    _count = 0;
                    _windowStart = now;
                }
                if (_count >= limit) return false;
                _count++;
                return true;
            }
        }
    }
}
