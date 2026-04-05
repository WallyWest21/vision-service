using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace VisionService.Tests.Middleware;

public class MiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CorrelationId_RequestWithoutHeader_ResponseIncludesGeneratedId()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.Should().ContainKey("X-Correlation-Id");
        var correlationId = response.Headers.GetValues("X-Correlation-Id").First();
        correlationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CorrelationId_RequestWithHeader_ResponseEchoesId()
    {
        var client = _factory.CreateClient();
        var expectedId = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", expectedId);

        var response = await client.GetAsync("/health");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.First().Should().Be(expectedId);
    }

    [Fact]
    public async Task SecurityHeaders_AllResponses_IncludeXContentTypeOptions()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.Should().ContainKey("X-Content-Type-Options");
    }

    [Fact]
    public async Task SecurityHeaders_AllResponses_IncludeXFrameOptions()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.Should().ContainKey("X-Frame-Options");
    }

    [Fact]
    public async Task GlobalExceptionHandler_NormalRequest_ReturnsExpectedStatus()
    {
        // Verify that the exception middleware does not break normal request flow.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RateLimit_ExemptPaths_NotThrottled()
    {
        var client = _factory.CreateClient();

        // Health endpoint is exempt from rate limiting — should always return 200
        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/health");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
