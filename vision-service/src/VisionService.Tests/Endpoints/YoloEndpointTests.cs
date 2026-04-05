using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using VisionService.Clients;
using VisionService.Models;
using Xunit;

namespace VisionService.Tests.Endpoints;

public class YoloEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public YoloEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithMockedYolo(IYoloClient mockedYolo)
    {
        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(mockedYolo);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task Detect_WithValidImage_ReturnsOk()
    {
        var mockedYolo = Substitute.For<IYoloClient>();
        mockedYolo.DetectAsync(Arg.Any<Stream>(), Arg.Any<float>(), Arg.Any<CancellationToken>())
            .Returns([new Detection { Label = "person", Confidence = 0.9f }]);

        var client = CreateClientWithMockedYolo(mockedYolo);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/detect", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("person");
    }

    [Fact]
    public async Task Detect_WithInvalidConfidence_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0x01]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/detect?confidence=1.5", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Classify_WithValidImage_ReturnsOk()
    {
        var mockedYolo = Substitute.For<IYoloClient>();
        mockedYolo.ClassifyAsync(Arg.Any<Stream>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new ClassificationResult { Label = "cat", Confidence = 0.95f }]);

        var client = CreateClientWithMockedYolo(mockedYolo);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0x01]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/classify", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
