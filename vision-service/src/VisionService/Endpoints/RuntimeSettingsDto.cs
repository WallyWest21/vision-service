namespace VisionService.Endpoints;

/// <summary>DTO representing all runtime-configurable settings.</summary>
public class RuntimeSettingsDto
{
    /// <summary>Rate limiting settings.</summary>
    public RateLimitSettingsDto? RateLimit { get; set; }

    /// <summary>Response cache settings.</summary>
    public CacheSettingsDto? Cache { get; set; }

    /// <summary>Performance tuning settings.</summary>
    public PerformanceSettingsDto? Performance { get; set; }

    /// <summary>YOLO backend settings.</summary>
    public YoloSettingsDto? Yolo { get; set; }

    /// <summary>Qwen-VL backend settings.</summary>
    public QwenVlSettingsDto? QwenVl { get; set; }

    /// <summary>Storage settings.</summary>
    public StorageSettingsDto? Storage { get; set; }
}

/// <summary>Rate limiting settings DTO.</summary>
public class RateLimitSettingsDto
{
    /// <summary>Maximum requests per minute per client.</summary>
    public int? RequestsPerMinute { get; set; }

    /// <summary>Token bucket burst size.</summary>
    public int? BurstSize { get; set; }
}

/// <summary>Cache settings DTO.</summary>
public class CacheSettingsDto
{
    /// <summary>Whether caching is enabled.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Default TTL in seconds.</summary>
    public int? DefaultTtlSeconds { get; set; }

    /// <summary>Maximum number of cached items.</summary>
    public int? MaxItems { get; set; }
}

/// <summary>Performance tuning settings DTO.</summary>
public class PerformanceSettingsDto
{
    /// <summary>Minimum interval in milliseconds between AI calls on WebSocket.</summary>
    public int? MinAiIntervalMs { get; set; }

    /// <summary>Maximum WebSocket frame size in bytes.</summary>
    public int? MaxWebSocketFrameBytes { get; set; }

    /// <summary>Health check probe interval in seconds.</summary>
    public int? HealthCheckIntervalSeconds { get; set; }

    /// <summary>Image cleanup job interval in hours.</summary>
    public int? ImageCleanupIntervalHours { get; set; }

    /// <summary>Maximum concurrent AI backend requests (0 = unlimited).</summary>
    public int? MaxConcurrentAiRequests { get; set; }
}

/// <summary>YOLO backend settings DTO.</summary>
public class YoloSettingsDto
{
    /// <summary>Base URL of the YOLO backend.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>HTTP timeout in seconds.</summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>Maximum retry attempts.</summary>
    public int? MaxRetries { get; set; }
}

/// <summary>Qwen-VL backend settings DTO.</summary>
public class QwenVlSettingsDto
{
    /// <summary>Base URL of the Qwen-VL backend.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Model name for inference.</summary>
    public string? ModelName { get; set; }

    /// <summary>Maximum response tokens.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Sampling temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>HTTP timeout in seconds.</summary>
    public int? TimeoutSeconds { get; set; }
}

/// <summary>Storage settings DTO.</summary>
public class StorageSettingsDto
{
    /// <summary>Image retention period in days.</summary>
    public int? RetentionDays { get; set; }

    /// <summary>Maximum file size in megabytes.</summary>
    public int? MaxFileSizeMb { get; set; }
}
