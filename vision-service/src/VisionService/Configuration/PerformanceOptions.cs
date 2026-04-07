using System.ComponentModel.DataAnnotations;

namespace VisionService.Configuration;

/// <summary>Configuration options for service-wide performance tuning.</summary>
public class PerformanceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Performance";

    /// <summary>Minimum interval in milliseconds between AI inference calls on the WebSocket stream. Defaults to 500.</summary>
    [Range(50, 10000)]
    public int MinAiIntervalMs { get; set; } = 500;

    /// <summary>Maximum WebSocket frame size in bytes. Defaults to 5 MB.</summary>
    [Range(65536, 52_428_800)]
    public int MaxWebSocketFrameBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Health check interval in seconds for AI backend probes. Defaults to 30.</summary>
    [Range(5, 600)]
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>Image cleanup job interval in hours. Defaults to 6.</summary>
    [Range(1, 168)]
    public int ImageCleanupIntervalHours { get; set; } = 6;

    /// <summary>Maximum number of concurrent requests forwarded to AI backends. 0 = unlimited. Defaults to 0.</summary>
    [Range(0, 1000)]
    public int MaxConcurrentAiRequests { get; set; } = 0;

    /// <summary>Maximum number of concurrent WebSocket connections. Defaults to 10.</summary>
    [Range(1, 1000)]
    public int MaxWebSocketConnections { get; set; } = 10;
}
