namespace VisionService.Models;

/// <summary>Metadata about a stored image.</summary>
public class ImageMetadata
{
    /// <summary>Unique image identifier.</summary>
    public string ImageId { get; set; } = string.Empty;

    /// <summary>Original file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>File extension.</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Image width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Image height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Content type (MIME type).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Storage path relative to root.</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the image was stored.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
