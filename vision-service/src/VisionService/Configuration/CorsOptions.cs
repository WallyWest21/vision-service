namespace VisionService.Configuration;

/// <summary>Configuration options for the CORS policy.</summary>
public class CorsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Cors";

    /// <summary>Origins that are allowed to make cross-origin requests. Defaults to ["*"].</summary>
    public string[] AllowedOrigins { get; set; } = ["*"];
}
