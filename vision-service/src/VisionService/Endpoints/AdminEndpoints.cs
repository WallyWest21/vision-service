using Microsoft.Extensions.Options;
using VisionService.Configuration;

namespace VisionService.Endpoints;

/// <summary>Admin endpoints for API key management and runtime settings.</summary>
public static class AdminEndpoints
{
    /// <summary>Maps admin API key management and settings endpoints under /api/v1/admin.</summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin").WithTags("Admin");

        group.MapGet("/keys", ListKeysAsync)
            .WithName("ListApiKeys")
            .WithSummary("List all configured API keys (names and scopes only — key values are masked)")
            .WithOpenApi();

        group.MapPost("/keys", AddKeyAsync)
            .WithName("AddApiKey")
            .WithSummary("Generate and register a new API key")
            .WithOpenApi();

        group.MapGet("/settings", GetSettingsAsync)
            .WithName("GetSettings")
            .WithSummary("Get all runtime-configurable performance and service settings")
            .WithOpenApi();

        group.MapPut("/settings", UpdateSettingsAsync)
            .WithName("UpdateSettings")
            .WithSummary("Update runtime-configurable performance and service settings")
            .WithOpenApi();

        return app;
    }

    private static IResult ListKeysAsync(IOptions<AuthOptions> auth)
    {
        var keys = auth.Value.ApiKeys.Select(k => new
        {
            k.Name,
            k.Scopes,
            k.RequestsPerMinute,
            KeyPreview = k.Key.Length > 4 ? $"...{k.Key[^4..]}" : "****"
        });
        return Results.Ok(keys);
    }

    private static IResult AddKeyAsync(NewApiKeyRequest request, IOptions<AuthOptions> auth)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.Problem("Name is required", statusCode: 400);

        var key = Guid.NewGuid().ToString("N");
        var entry = new ApiKeyEntry
        {
            Key = key,
            Name = request.Name,
            Scopes = request.Scopes ?? [],
            RequestsPerMinute = request.RequestsPerMinute
        };

        var existing = auth.Value.ApiKeys.ToList();
        existing.Add(entry);
        auth.Value.ApiKeys = [.. existing];

        return Results.Ok(new { key, entry.Name, entry.Scopes });
    }

    private static IResult GetSettingsAsync(
        IOptionsMonitor<RateLimitOptions> rateLimit,
        IOptionsMonitor<CacheOptions> cache,
        IOptionsMonitor<PerformanceOptions> performance,
        IOptionsMonitor<YoloOptions> yolo,
        IOptionsMonitor<QwenVlOptions> qwenVl,
        IOptionsMonitor<StorageOptions> storage)
    {
        return Results.Ok(new RuntimeSettingsDto
        {
            RateLimit = new RateLimitSettingsDto
            {
                RequestsPerMinute = rateLimit.CurrentValue.RequestsPerMinute,
                BurstSize = rateLimit.CurrentValue.BurstSize
            },
            Cache = new CacheSettingsDto
            {
                Enabled = cache.CurrentValue.Enabled,
                DefaultTtlSeconds = cache.CurrentValue.DefaultTtlSeconds,
                MaxItems = cache.CurrentValue.MaxItems
            },
            Performance = new PerformanceSettingsDto
            {
                MinAiIntervalMs = performance.CurrentValue.MinAiIntervalMs,
                MaxWebSocketFrameBytes = performance.CurrentValue.MaxWebSocketFrameBytes,
                HealthCheckIntervalSeconds = performance.CurrentValue.HealthCheckIntervalSeconds,
                ImageCleanupIntervalHours = performance.CurrentValue.ImageCleanupIntervalHours,
                MaxConcurrentAiRequests = performance.CurrentValue.MaxConcurrentAiRequests
            },
            Yolo = new YoloSettingsDto
            {
                BaseUrl = yolo.CurrentValue.BaseUrl,
                TimeoutSeconds = yolo.CurrentValue.TimeoutSeconds,
                MaxRetries = yolo.CurrentValue.MaxRetries
            },
            QwenVl = new QwenVlSettingsDto
            {
                BaseUrl = qwenVl.CurrentValue.BaseUrl,
                ModelName = qwenVl.CurrentValue.ModelName,
                MaxTokens = qwenVl.CurrentValue.MaxTokens,
                Temperature = qwenVl.CurrentValue.Temperature,
                TimeoutSeconds = qwenVl.CurrentValue.TimeoutSeconds
            },
            Storage = new StorageSettingsDto
            {
                RetentionDays = storage.CurrentValue.RetentionDays,
                MaxFileSizeMb = storage.CurrentValue.MaxFileSizeMb
            }
        });
    }

    private static IResult UpdateSettingsAsync(
        RuntimeSettingsDto dto,
        IOptionsMonitor<RateLimitOptions> rateLimit,
        IOptionsMonitor<CacheOptions> cache,
        IOptionsMonitor<PerformanceOptions> performance,
        IOptionsMonitor<YoloOptions> yolo,
        IOptionsMonitor<QwenVlOptions> qwenVl,
        IOptionsMonitor<StorageOptions> storage)
    {
        // Apply rate limit changes
        if (dto.RateLimit is { } rl)
        {
            if (rl.RequestsPerMinute.HasValue) rateLimit.CurrentValue.RequestsPerMinute = rl.RequestsPerMinute.Value;
            if (rl.BurstSize.HasValue) rateLimit.CurrentValue.BurstSize = rl.BurstSize.Value;
        }

        // Apply cache changes
        if (dto.Cache is { } c)
        {
            if (c.Enabled.HasValue) cache.CurrentValue.Enabled = c.Enabled.Value;
            if (c.DefaultTtlSeconds.HasValue) cache.CurrentValue.DefaultTtlSeconds = c.DefaultTtlSeconds.Value;
            if (c.MaxItems.HasValue) cache.CurrentValue.MaxItems = c.MaxItems.Value;
        }

        // Apply performance changes
        if (dto.Performance is { } p)
        {
            if (p.MinAiIntervalMs.HasValue) performance.CurrentValue.MinAiIntervalMs = p.MinAiIntervalMs.Value;
            if (p.MaxWebSocketFrameBytes.HasValue) performance.CurrentValue.MaxWebSocketFrameBytes = p.MaxWebSocketFrameBytes.Value;
            if (p.HealthCheckIntervalSeconds.HasValue) performance.CurrentValue.HealthCheckIntervalSeconds = p.HealthCheckIntervalSeconds.Value;
            if (p.ImageCleanupIntervalHours.HasValue) performance.CurrentValue.ImageCleanupIntervalHours = p.ImageCleanupIntervalHours.Value;
            if (p.MaxConcurrentAiRequests.HasValue) performance.CurrentValue.MaxConcurrentAiRequests = p.MaxConcurrentAiRequests.Value;
        }

        // Apply YOLO changes
        if (dto.Yolo is { } y)
        {
            if (y.TimeoutSeconds.HasValue) yolo.CurrentValue.TimeoutSeconds = y.TimeoutSeconds.Value;
            if (y.MaxRetries.HasValue) yolo.CurrentValue.MaxRetries = y.MaxRetries.Value;
        }

        // Apply QwenVl changes
        if (dto.QwenVl is { } q)
        {
            if (q.MaxTokens.HasValue) qwenVl.CurrentValue.MaxTokens = q.MaxTokens.Value;
            if (q.Temperature.HasValue) qwenVl.CurrentValue.Temperature = q.Temperature.Value;
            if (q.TimeoutSeconds.HasValue) qwenVl.CurrentValue.TimeoutSeconds = q.TimeoutSeconds.Value;
        }

        // Apply storage changes
        if (dto.Storage is { } s)
        {
            if (s.RetentionDays.HasValue) storage.CurrentValue.RetentionDays = s.RetentionDays.Value;
            if (s.MaxFileSizeMb.HasValue) storage.CurrentValue.MaxFileSizeMb = s.MaxFileSizeMb.Value;
        }

        return Results.Ok(new { Message = "Settings updated. Changes take effect immediately for most settings." });
    }
}

/// <summary>Request body for creating a new API key.</summary>
public class NewApiKeyRequest
{
    /// <summary>Display name for the key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Scopes to assign: detect, analyze, admin, stream.</summary>
    public string[]? Scopes { get; set; }

    /// <summary>Per-minute rate limit override for this key (0 = use default).</summary>
    public int RequestsPerMinute { get; set; } = 0;
}
