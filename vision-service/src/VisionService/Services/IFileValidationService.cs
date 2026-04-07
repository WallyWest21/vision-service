namespace VisionService.Services;

/// <summary>Result of a file validation check.</summary>
public sealed record FileValidationResult(bool IsValid, string? ErrorMessage)
{
    /// <summary>A successful validation result.</summary>
    public static FileValidationResult Ok() => new(true, null);

    /// <summary>A failed validation result with the supplied error message.</summary>
    public static FileValidationResult Fail(string errorMessage) => new(false, errorMessage);
}

/// <summary>Validates uploaded image files against extension, magic-byte, and size constraints.</summary>
public interface IFileValidationService
{
    /// <summary>
    /// Validates the supplied form file against the configured allowed extensions,
    /// magic-byte signatures, and maximum file size.
    /// </summary>
    /// <param name="file">The uploaded form file to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="FileValidationResult"/> indicating success or the first validation failure.</returns>
    Task<FileValidationResult> ValidateAsync(IFormFile file, CancellationToken ct = default);
}
