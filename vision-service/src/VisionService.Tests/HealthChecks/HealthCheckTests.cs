using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using VisionService.Clients;
using Xunit;

namespace VisionService.Tests.HealthChecks;

/// <summary>Integration tests for the ASP.NET Core health-check endpoints.</summary>
public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithBackends(bool yoloHealthy, bool qwenHealthy)
    {
        var yolo = Substitute.For<IYoloClient>();
        yolo.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(yoloHealthy);

        var qwen = Substitute.For<IQwenVlClient>();
        qwen.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(qwenHealthy);

        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(yolo);
                services.AddSingleton(qwen);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task Live_AlwaysReturns200()
    {
        // /health/live uses Predicate = _ => false, so no checks run — always Healthy
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task Ready_Returns200_WhenBothBackendsAreHealthy()
    {
        var client = CreateClientWithBackends(yoloHealthy: true, qwenHealthy: true);

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task Ready_Returns503_WhenYoloBackendIsUnhealthy()
    {
        var client = CreateClientWithBackends(yoloHealthy: false, qwenHealthy: true);

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Unhealthy");
    }

    [Fact]
    public async Task Ready_Returns503_WhenQwenVlBackendIsUnhealthy()
    {
        var client = CreateClientWithBackends(yoloHealthy: true, qwenHealthy: false);

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Unhealthy");
    }

    [Fact]
    public async Task Health_ResponseIncludesServiceNameVersionAndTimestamp()
    {
        var client = CreateClientWithBackends(yoloHealthy: true, qwenHealthy: true);

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("VisionService");
        content.Should().Contain("version");
        content.Should().Contain("timestamp");
    }
}
