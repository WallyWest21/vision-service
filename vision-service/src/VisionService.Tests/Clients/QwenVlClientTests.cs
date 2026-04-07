using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VisionService.Clients;
using VisionService.Configuration;
using Xunit;

namespace VisionService.Tests.Clients;

public class QwenVlClientTests
{
    private static QwenVlClient CreateClient(HttpClient httpClient)
    {
        var options = Options.Create(new QwenVlOptions
        {
            BaseUrl = "http://test",
            ModelName = "test-model",
            MaxTokens = 100,
            Temperature = 0.7
        });
        return new QwenVlClient(httpClient, options, NullLogger<QwenVlClient>.Instance);
    }

    [Fact]
    public async Task AskAsync_SuccessResponse_ReturnsVlResponse()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            model = "test-model",
            choices = new[] { new { message = new { content = "This is a cat." } } },
            usage = new { prompt_tokens = 10, completion_tokens = 5 }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        var result = await client.AskAsync(new MemoryStream([0x01]), "What is this?");

        result.Text.Should().Be("This is a cat.");
        result.PromptTokens.Should().Be(10);
    }

    [Fact]
    public async Task AskAsync_ServerError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "error");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        await client.Invoking(c => c.AskAsync(new MemoryStream([0x01]), "What is this?"))
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CaptionAsync_SuccessResponse_ReturnsVlResponse()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            model = "test-model",
            choices = new[] { new { message = new { content = "A sunny landscape." } } },
            usage = new { prompt_tokens = 8, completion_tokens = 6 }
        });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        var result = await client.CaptionAsync(new MemoryStream([0x01, 0x02]));

        result.Text.Should().Be("A sunny landscape.");
        result.Model.Should().Be("test-model");
    }

    [Fact]
    public async Task IsHealthyAsync_ServerDown_ReturnsFalse()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        var result = await client.IsHealthyAsync();

        result.Should().BeFalse();
    }
}
