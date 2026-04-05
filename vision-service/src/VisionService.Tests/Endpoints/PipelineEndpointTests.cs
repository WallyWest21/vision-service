using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using VisionService.Clients;
using VisionService.Models;
using Xunit;

namespace VisionService.Tests.Endpoints;

public class PipelineEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PipelineEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(IYoloClient? yolo = null, IQwenVlClient? qwen = null)
    {
        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                if (yolo is not null) services.AddSingleton(yolo);
                if (qwen is not null) services.AddSingleton(qwen);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task DetectAndDescribe_WithValidImage_ReturnsOk()
    {
        var yolo = Substitute.For<IYoloClient>();
        yolo.DetectAsync(Arg.Any<Stream>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns([new Detection { Label = "car", Confidence = 0.8f }]);

        var qwen = Substitute.For<IQwenVlClient>();
        qwen.CaptionAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new VlResponse { Text = "A car on a road.", Model = "test" });

        var client = CreateClient(yolo, qwen);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0x01]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/pipeline/detect-and-describe", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("car");
    }

    [Fact]
    public async Task SafetyCheck_WithValidImage_ReturnsOk()
    {
        var yolo = Substitute.For<IYoloClient>();
        yolo.DetectAsync(Arg.Any<Stream>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var qwen = Substitute.For<IQwenVlClient>();
        qwen.AnalyzeAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VlResponse { Text = "SAFE: No harmful content detected.", Model = "test" });

        var client = CreateClient(yolo, qwen);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0x01]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/pipeline/safety-check", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("isSafe");
    }

    [Fact]
    public async Task Inventory_WithValidImage_ReturnsItemCounts()
    {
        var yolo = Substitute.For<IYoloClient>();
        yolo.DetectAsync(Arg.Any<Stream>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns([
                new Detection { Label = "apple", Confidence = 0.9f },
                new Detection { Label = "apple", Confidence = 0.85f },
                new Detection { Label = "banana", Confidence = 0.7f }
            ]);

        var qwen = Substitute.For<IQwenVlClient>();
        qwen.AnalyzeAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VlResponse { Text = "apple: 2\nbanana: 1", Model = "test" });

        var client = CreateClient(yolo, qwen);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0x01]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/pipeline/inventory", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("itemCounts");
    }

    [Fact]
    public async Task Scene_WithValidImage_ReturnsCombinedAnalysis()
    {
        var yolo = Substitute.For<IYoloClient>();
        yolo.DetectAsync(Arg.Any<Stream>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns([new Detection { Label = "person", Confidence = 0.9f }]);

        var qwen = Substitute.For<IQwenVlClient>();
        qwen.CaptionAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new VlResponse { Text = "A person standing.", Model = "test" });
        qwen.OcrAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new VlResponse { Text = "No text visible.", Model = "test" });

        var client = CreateClient(yolo, qwen);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0x01]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/pipeline/scene", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("caption");
        body.Should().Contain("detections");
    }

    [Fact]
    public async Task DetectAndDescribe_BackendUnavailable_Returns503()
    {
        var yolo = Substitute.For<IYoloClient>();
        yolo.DetectAsync(Arg.Any<Stream>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<Detection>>(new HttpRequestException("connection refused")));

        var qwen = Substitute.For<IQwenVlClient>();

        var client = CreateClient(yolo, qwen);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0x01]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/pipeline/detect-and-describe", content);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
