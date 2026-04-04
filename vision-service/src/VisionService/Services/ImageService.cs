using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VisionService.Configuration;
using VisionService.Models;

namespace VisionService.Services;

/// <summary>File-system–based image storage service.</summary>
public class ImageService : IImageService
{
    private readonly StorageOptions _options;
    private readonly ILogger<ImageService> _logger;

    /// <summary>Initializes a new instance of <see cref="ImageService"/>.</summary>
    public ImageService(IOptions<StorageOptions> options, ILogger<ImageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ImageMetadata> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        ValidateFile(file);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var imageId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var relativePath = Path.Combine(now.Year.ToString(), now.Month.ToString("D2"), now.Day.ToString("D2"), $"{imageId}{ext}");
        var fullPath = Path.Combine(_options.ImageStoragePath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var dest = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(dest, ct);

        _logger.LogInformation("Saved image {ImageId} to {Path}", imageId, fullPath);

        return new ImageMetadata
        {
            ImageId = imageId,
            FileName = file.FileName,
            Extension = ext,
            FileSizeBytes = file.Length,
            ContentType = file.ContentType,
            StoragePath = relativePath,
            CreatedAt = now
        };
    }

    /// <inheritdoc/>
    public Task<Stream> LoadAsync(string imageId, CancellationToken ct = default)
    {
        var path = FindImagePath(imageId) ?? throw new FileNotFoundException($"Image {imageId} not found.");
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string imageId, CancellationToken ct = default)
    {
        var path = FindImagePath(imageId);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInformation("Deleted image {ImageId}", imageId);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<string> ConvertToBase64Async(Stream image, CancellationToken ct = default)
    {
        if (image.CanSeek) image.Position = 0;
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, ct);
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <inheritdoc/>
    public Task<int> CleanupExpiredAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var deleted = 0;
        if (!Directory.Exists(_options.ImageStoragePath)) return Task.FromResult(0);

        foreach (var file in Directory.EnumerateFiles(_options.ImageStoragePath, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (File.GetCreationTimeUtc(file) < cutoff)
            {
                File.Delete(file);
                deleted++;
            }
        }

        _logger.LogInformation("Cleanup removed {Count} expired images", deleted);
        return Task.FromResult(deleted);
    }

    private void ValidateFile(IFormFile file)
    {
        var maxBytes = _options.MaxFileSizeMb * 1024 * 1024;
        if (file.Length > maxBytes)
            throw new ArgumentException($"File size {file.Length} bytes exceeds maximum {maxBytes} bytes.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File extension '{ext}' is not allowed. Allowed: {string.Join(", ", _options.AllowedExtensions)}");
    }

    private string? FindImagePath(string imageId)
    {
        if (!Directory.Exists(_options.ImageStoragePath)) return null;
        return Directory.EnumerateFiles(_options.ImageStoragePath, $"{imageId}.*", SearchOption.AllDirectories).FirstOrDefault();
    }
}
