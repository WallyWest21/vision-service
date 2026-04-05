using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VisionService.Configuration;
using VisionService.Services;
using Xunit;

namespace VisionService.Tests.Services;

public class ImageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ImageService _service;

    public ImageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new StorageOptions
        {
            ImageStoragePath = _tempDir,
            RetentionDays = 7,
            MaxFileSizeMb = 20,
            AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"]
        });

        _service = new ImageService(options, NullLogger<ImageService>.Instance);
    }

    [Fact]
    public async Task SaveAsync_ValidJpgFile_ReturnsMetadata()
    {
        var file = CreateFormFile("test.jpg", "image/jpeg", new byte[100]);

        var metadata = await _service.SaveAsync(file);

        metadata.ImageId.Should().NotBeNullOrEmpty();
        metadata.Extension.Should().Be(".jpg");
        metadata.FileSizeBytes.Should().Be(100);
    }

    [Fact]
    public async Task SaveAsync_OversizedFile_ThrowsArgumentException()
    {
        var bigFile = CreateFormFile("big.jpg", "image/jpeg", new byte[21 * 1024 * 1024]);

        await _service.Invoking(s => s.SaveAsync(bigFile))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds maximum*");
    }

    [Fact]
    public async Task SaveAsync_DisallowedExtension_ThrowsArgumentException()
    {
        var file = CreateFormFile("hack.exe", "application/octet-stream", new byte[100]);

        await _service.Invoking(s => s.SaveAsync(file))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not allowed*");
    }

    [Fact]
    public async Task LoadAsync_NonexistentImage_ThrowsFileNotFoundException()
    {
        await _service.Invoking(s => s.LoadAsync("nonexistent-id"))
            .Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_ReturnsOriginalContent()
    {
        var content = Encoding.UTF8.GetBytes("fake image data");
        var file = CreateFormFile("round.jpg", "image/jpeg", content);

        var metadata = await _service.SaveAsync(file);
        await using var stream = await _service.LoadAsync(metadata.ImageId);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        ms.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task ConvertToBase64Async_ValidStream_ReturnsBase64()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var result = await _service.ConvertToBase64Async(new MemoryStream(data));

        result.Should().Be(Convert.ToBase64String(data));
    }

    [Fact]
    public async Task CleanupExpiredAsync_NoFiles_ReturnsZero()
    {
        var count = await _service.CleanupExpiredAsync();

        count.Should().Be(0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
