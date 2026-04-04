using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VisionService.Clients;
using VisionService.Configuration;
using VisionService.Models;
using Xunit;

namespace VisionService.Tests.Clients;

public class YoloClientTests
{
    private static YoloClient CreateClient(HttpClient httpClient)
    {
        var options = Options.Create(new YoloOptions { BaseUrl = "http://test", TimeoutSeconds = 30, MaxRetries = 3 });
        return new YoloClient(httpClient, options, NullLogger<YoloClient>.Instance);
    }

    [Fact]
    public async Task DetectAsync_SuccessResponse_ReturnsDetections()
    {
        var detections = new[] { new Detection { Label = "person", Confidence = 0.9f } };
        var responseBody = JsonSerializer.Serialize(new { Detections = detections });

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        var result = await client.DetectAsync(new MemoryStream([0x01, 0x02]));

        result.Should().HaveCount(1);
        result[0].Label.Should().Be("person");
    }

    [Fact]
    public async Task DetectAsync_ServerError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "error");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        await client.Invoking(c => c.DetectAsync(new MemoryStream([0x01])))
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task IsHealthyAsync_SuccessResponse_ReturnsTrue()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        var result = await client.IsHealthyAsync();

        result.Should().BeTrue();
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
    public async Task IsHealthyAsync_ServerDown_ReturnsFalse()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var client = CreateClient(httpClient);

        var result = await client.IsHealthyAsync();

        result.Should().BeFalse();
    }
}

internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;

    public FakeHttpMessageHandler(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

internal class ThrowingHttpMessageHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => throw _exception;
}
