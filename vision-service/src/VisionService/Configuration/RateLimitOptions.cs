using System.ComponentModel.DataAnnotations;

namespace VisionService.Configuration;

/// <summary>Configuration options for rate limiting.</summary>
public class RateLimitOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RateLimit";

    /// <summary>Default maximum requests per minute per client.</summary>
    [Range(1, 10000)]
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>Burst size above the rate limit.</summary>
    [Range(1, 1000)]
    public int BurstSize { get; set; } = 10;
}
