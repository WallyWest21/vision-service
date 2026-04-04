using System.ComponentModel.DataAnnotations;

namespace VisionService.Configuration;

/// <summary>Configuration options for image storage.</summary>
public class StorageOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Storage";

    /// <summary>Root path for image storage.</summary>
    [Required]
    public string ImageStoragePath { get; set; } = "/data/images";

    /// <summary>Number of days to retain images before cleanup.</summary>
    [Range(1, 365)]
    public int RetentionDays { get; set; } = 7;

    /// <summary>Maximum allowed file size in megabytes.</summary>
    [Range(1, 100)]
    public int MaxFileSizeMb { get; set; } = 20;

    /// <summary>Allowed file extensions for uploads.</summary>
    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"];
}
