using System.ComponentModel.DataAnnotations;

namespace VisionService.Configuration;

/// <summary>Configuration options for the Qwen-VL vLLM backend.</summary>
public class QwenVlOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "QwenVl";

    /// <summary>Base URL of the Qwen-VL vLLM server.</summary>
    [Required, Url]
    public string BaseUrl { get; set; } = "http://qwen-vl:8000";

    /// <summary>Model identifier to use for inference.</summary>
    [Required]
    public string ModelName { get; set; } = "Qwen/Qwen2.5-VL-7B-Instruct";

    /// <summary>Maximum number of tokens in the response.</summary>
    [Range(1, 8192)]
    public int MaxTokens { get; set; } = 1024;

    /// <summary>Sampling temperature for text generation.</summary>
    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.7;

    /// <summary>HTTP request timeout in seconds.</summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Maximum number of retry attempts.</summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>Number of consecutive failures before the circuit breaker opens.</summary>
    [Range(1, 100)]
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>Duration in seconds the circuit breaker stays open before moving to half-open.</summary>
    [Range(1, 600)]
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
