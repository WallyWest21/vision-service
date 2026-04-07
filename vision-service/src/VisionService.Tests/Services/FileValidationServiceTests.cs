using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VisionService.Configuration;
using VisionService.Services;
using Xunit;

namespace VisionService.Tests.Services;

public class FileValidationServiceTests
{
    private static FileValidationService CreateService(
        int maxFileSizeMb = 20,
        string[]? allowedExtensions = null)
    {
        var options = Options.Create(new StorageOptions
        {
            MaxFileSizeMb = maxFileSizeMb,
            AllowedExtensions = allowedExtensions ?? [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"]
        });
        return new FileValidationService(options);
    }

    private static IFormFile CreateFormFile(string fileName, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    [Fact]
    public async Task ValidateAsync_ValidJpeg_ReturnsSuccess()
    {
        var service = CreateService();
        // JPEG magic bytes: FF D8 FF
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var file = CreateFormFile("photo.jpg", content);

        var result = await service.ValidateAsync(file);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_InvalidExtension_ReturnsFail()
    {
        var service = CreateService();
        var content = new byte[] { 0xFF, 0xD8, 0xFF };
        var file = CreateFormFile("script.exe", content);

        var result = await service.ValidateAsync(file);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(".exe");
    }

    [Fact]
    public async Task ValidateAsync_OversizedFile_ReturnsFail()
    {
        var service = CreateService(maxFileSizeMb: 1);
        // 2 MB file — exceeds 1 MB limit
        var content = new byte[2 * 1024 * 1024];
        // Set JPEG magic bytes so that extension passes
        content[0] = 0xFF; content[1] = 0xD8; content[2] = 0xFF;
        var file = CreateFormFile("big.jpg", content);

        var result = await service.ValidateAsync(file);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds maximum");
    }

    [Fact]
    public async Task ValidateAsync_WrongMagicBytesForDeclaredExtension_ReturnsFail()
    {
        var service = CreateService();
        // Declare as PNG but put JPEG magic bytes
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var file = CreateFormFile("fake.png", content);

        var result = await service.ValidateAsync(file);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(".png");
    }

    [Fact]
    public async Task ValidateAsync_ValidPng_ReturnsSuccess()
    {
        var service = CreateService();
        // PNG magic bytes: 89 50 4E 47
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var file = CreateFormFile("image.png", content);

        var result = await service.ValidateAsync(file);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ValidWebP_ReturnsSuccess()
    {
        var service = CreateService();
        // WebP: RIFF at offset 0 (bytes 0-3) + 'WEBP' at offset 8 (bytes 8-11)
        var content = new byte[12];
        content[0] = 0x52; content[1] = 0x49; content[2] = 0x46; content[3] = 0x46; // RIFF
        content[8] = 0x57; content[9] = 0x45; content[10] = 0x42; content[11] = 0x50; // WEBP
        var file = CreateFormFile("image.webp", content);

        var result = await service.ValidateAsync(file);

        result.IsValid.Should().BeTrue();
    }
}
