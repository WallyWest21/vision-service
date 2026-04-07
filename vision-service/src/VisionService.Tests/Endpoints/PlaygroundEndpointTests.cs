using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using VisionService.Clients;
using VisionService.Models;
using Xunit;

namespace VisionService.Tests.Endpoints;

public class PlaygroundEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PlaygroundEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(IYoloClient yolo, IQwenVlClient qwen)
    {
        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(yolo);
                services.AddSingleton(qwen);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task Playground_WithValidImage_ReturnsOk()
    {
        var yolo = Substitute.For<IYoloClient>();
        yolo.DetectAsync(Arg.Any<Stream>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns([new Detection { Label = "dog", Confidence = 0.9f }]);

        var qwen = Substitute.For<IQwenVlClient>();
        qwen.CaptionAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new VlResponse { Text = "A dog in the park.", Model = "test" });

        var client = CreateClient(yolo, qwen);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/playground", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("detections");
        body.Should().Contain("caption");
    }

    [Fact]
    public async Task Playground_BackendUnavailable_Returns503()
    {
        var yolo = Substitute.For<IYoloClient>();
        yolo.DetectAsync(Arg.Any<Stream>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<Detection>>(new HttpRequestException("connection refused")));

        var qwen = Substitute.For<IQwenVlClient>();

        var client = CreateClient(yolo, qwen);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/playground", content);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
