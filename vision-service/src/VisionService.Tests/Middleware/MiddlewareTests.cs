using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VisionService.Configuration;
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

    [Fact]
    public async Task RateLimit_ExceededOnNonExemptPath_Returns429()
    {
        // Configure a factory with a very low rate limit (1 request/minute) so we can
        // trigger a 429 with a small number of requests in the test.
        var lowLimitFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<RateLimitOptions>(opts =>
                {
                    opts.RequestsPerMinute = 1;
                    opts.BurstSize = 1;
                });
            });
        });

        var client = lowLimitFactory.CreateClient();

        // First request should succeed (non-exempt path /api/v1/detect returns 415 for GET,
        // but the rate limit middleware runs before the endpoint; any non-exempt path works)
        var first = await client.GetAsync("/api/v1/detect");
        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        // Second request should be rate-limited
        var second = await client.GetAsync("/api/v1/detect");
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
