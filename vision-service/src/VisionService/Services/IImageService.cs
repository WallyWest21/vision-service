using Microsoft.AspNetCore.Http;
using VisionService.Models;

namespace VisionService.Services;

/// <summary>Service for storing, retrieving, and processing images.</summary>
public interface IImageService
{
    /// <summary>Saves an uploaded file and returns its metadata.</summary>
    Task<ImageMetadata> SaveAsync(IFormFile file, CancellationToken ct = default);

    /// <summary>Loads an image stream by its ID.</summary>
    Task<Stream> LoadAsync(string imageId, CancellationToken ct = default);

    /// <summary>Deletes a stored image by its ID.</summary>
    Task DeleteAsync(string imageId, CancellationToken ct = default);

    /// <summary>Converts a stream to a Base64-encoded string.</summary>
    Task<string> ConvertToBase64Async(Stream image, CancellationToken ct = default);

    /// <summary>Deletes images older than the configured retention period.</summary>
    Task<int> CleanupExpiredAsync(CancellationToken ct = default);
}
