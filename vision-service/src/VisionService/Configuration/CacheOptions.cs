using System.ComponentModel.DataAnnotations;

namespace VisionService.Configuration;

/// <summary>Configuration options for the in-memory response cache.</summary>
public class CacheOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cache";

    /// <summary>Whether the response cache is enabled. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Default TTL in seconds for cached responses. Defaults to 300 (5 minutes).</summary>
    [Range(1, 86400)]
    public int DefaultTtlSeconds { get; set; } = 300;

    /// <summary>Maximum number of entries in the cache. Defaults to 1000.</summary>
    [Range(1, 100_000)]
    public int MaxItems { get; set; } = 1000;
}
