using Microsoft.Extensions.Options;
using VisionService.Configuration;

namespace VisionService.Services;

/// <summary>Validates uploaded image files against extension, magic-byte, and size constraints.</summary>
public sealed class FileValidationService : IFileValidationService
{
    // Magic-byte signatures keyed by normalised extension.
    // Each entry is an array of (offset, bytes) segments that ALL must match.
    // Multiple alternative signatures are supported (e.g. different JPEG sub-variants are all the same here).
    private static readonly Dictionary<string, (int Offset, byte[] Bytes)[][]> MagicSignatures =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"]  = [[(0, new byte[] { 0xFF, 0xD8, 0xFF })]],
            [".jpeg"] = [[(0, new byte[] { 0xFF, 0xD8, 0xFF })]],
            [".png"]  = [[(0, new byte[] { 0x89, 0x50, 0x4E, 0x47 })]],
            // WebP: RIFF at offset 0 AND 'WEBP' at offset 8
            [".webp"] = [[(0, new byte[] { 0x52, 0x49, 0x46, 0x46 }), (8, new byte[] { 0x57, 0x45, 0x42, 0x50 })]],
            [".bmp"]  = [[(0, new byte[] { 0x42, 0x4D })]],
            [".gif"]  = [[(0, new byte[] { 0x47, 0x49, 0x46, 0x38 })]],
        };

    private readonly StorageOptions _options;

    /// <summary>Initialises a new instance of <see cref="FileValidationService"/>.</summary>
    public FileValidationService(IOptions<StorageOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<FileValidationResult> ValidateAsync(IFormFile file, CancellationToken ct = default)
    {
        // 1. Extension check
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) ||
            !_options.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return FileValidationResult.Fail(
                $"File extension '{ext}' is not allowed. Allowed: {string.Join(", ", _options.AllowedExtensions)}");
        }

        // 2. Size check (before reading the whole stream)
        long maxBytes = (long)_options.MaxFileSizeMb * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return FileValidationResult.Fail(
                $"File size {file.Length} bytes exceeds maximum allowed {maxBytes} bytes ({_options.MaxFileSizeMb} MB).");
        }

        // 3. Magic-byte check
        if (MagicSignatures.TryGetValue(ext, out var alternatives))
        {
            // Determine how many bytes we need to read (max offset + segment length across all alternatives)
            int headerLength = alternatives
                .SelectMany(segs => segs)
                .Max(seg => seg.Offset + seg.Bytes.Length);

            var header = new byte[headerLength];
            int bytesRead;
            await using (var stream = file.OpenReadStream())
            {
                bytesRead = await stream.ReadAsync(header.AsMemory(0, headerLength), ct);
            }

            // File matches if ANY alternative has ALL its segments satisfied
            bool matched = alternatives.Any(segments =>
                segments.All(seg =>
                    bytesRead >= seg.Offset + seg.Bytes.Length &&
                    header.AsSpan(seg.Offset, seg.Bytes.Length).SequenceEqual(seg.Bytes)));

            if (!matched)
            {
                return FileValidationResult.Fail(
                    $"File content does not match the expected format for extension '{ext}'. " +
                    "Ensure the file is a valid image.");
            }
        }

        return FileValidationResult.Ok();
    }
}
