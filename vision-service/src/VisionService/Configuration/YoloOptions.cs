using System.ComponentModel.DataAnnotations;

namespace VisionService.Configuration;

/// <summary>Configuration options for the YOLO AI backend.</summary>
public class YoloOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Yolo";

    /// <summary>Base URL of the YOLO FastAPI server.</summary>
    [Required, Url]
    public string BaseUrl { get; set; } = "http://yolo-api:7860";

    /// <summary>HTTP request timeout in seconds.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum number of retry attempts.</summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;
}
