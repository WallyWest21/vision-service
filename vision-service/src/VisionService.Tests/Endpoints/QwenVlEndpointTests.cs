using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using VisionService.Clients;
using VisionService.Models;
using Xunit;

namespace VisionService.Tests.Endpoints;

public class QwenVlEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public QwenVlEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithMockedQwen(IQwenVlClient mockedQwen)
    {
        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(mockedQwen);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task Caption_WithValidImage_ReturnsOk()
    {
        var mockedQwen = Substitute.For<IQwenVlClient>();
        mockedQwen.CaptionAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new VlResponse { Text = "A beautiful scene.", Model = "test" });

        var client = CreateClientWithMockedQwen(mockedQwen);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/caption", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("beautiful");
    }

    [Fact]
    public async Task Ask_WithEmptyQuestion_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0x01]), "file", "test.jpg");
        content.Add(new StringContent(""), "question");

        var response = await client.PostAsync("/api/v1/ask", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ocr_WithValidImage_ReturnsOk()
    {
        var mockedQwen = Substitute.For<IQwenVlClient>();
        mockedQwen.OcrAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new VlResponse { Text = "Hello World", Model = "test" });

        var client = CreateClientWithMockedQwen(mockedQwen);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF]), "file", "test.jpg");

        var response = await client.PostAsync("/api/v1/ocr", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
