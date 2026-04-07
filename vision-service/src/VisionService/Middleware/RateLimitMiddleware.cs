using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using VisionService.Configuration;

namespace VisionService.Middleware;

/// <summary>Middleware that enforces per-IP rate limiting using a token-bucket algorithm.</summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();

    /// <summary>Initializes a new instance of <see cref="RateLimitMiddleware"/>.</summary>
    public RateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Processes the HTTP request, enforcing rate limits per client IP.</summary>
    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<RateLimitOptions> options)
    {
        var opts = options.CurrentValue;
        var path = context.Request.Path.Value ?? string.Empty;

        // Exempt health, swagger, metrics, and settings endpoints from rate limiting
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/admin/settings", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var bucket = _buckets.GetOrAdd(ip, _ => new TokenBucket(opts.RequestsPerMinute, opts.BurstSize));

        // Update bucket parameters in case they changed at runtime
        bucket.UpdateLimits(opts.RequestsPerMinute, opts.BurstSize);

        if (!bucket.TryConsume())
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = "1";
            await context.Response.WriteAsJsonAsync(new
            {
                Error = "Too Many Requests",
                RequestsPerMinute = opts.RequestsPerMinute,
                BurstSize = opts.BurstSize,
                Hint = "Increase RateLimit:RequestsPerMinute or RateLimit:BurstSize in appsettings.json or via the settings endpoint."
            });
            return;
        }

        await _next(context);
    }

    /// <summary>Token bucket rate limiter that refills tokens proportionally over time.</summary>
    private sealed class TokenBucket
    {
        private double _tokens;
        private double _maxTokens;
        private double _refillRatePerSecond;
        private DateTime _lastRefill;
        private readonly object _lock = new();

        public TokenBucket(int requestsPerMinute, int burstSize)
        {
            _refillRatePerSecond = requestsPerMinute / 60.0;
            _maxTokens = burstSize;
            _tokens = burstSize; // start full
            _lastRefill = DateTime.UtcNow;
        }

        public void UpdateLimits(int requestsPerMinute, int burstSize)
        {
            lock (_lock)
            {
                _refillRatePerSecond = requestsPerMinute / 60.0;
                _maxTokens = burstSize;
            }
        }

        public bool TryConsume()
        {
            lock (_lock)
            {
                Refill();
                if (_tokens < 1.0) return false;
                _tokens -= 1.0;
                return true;
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefill).TotalSeconds;
            _tokens = Math.Min(_maxTokens, _tokens + elapsed * _refillRatePerSecond);
            _lastRefill = now;
        }
    }
}
