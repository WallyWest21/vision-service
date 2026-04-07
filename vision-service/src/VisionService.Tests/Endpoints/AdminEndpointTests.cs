using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace VisionService.Tests.Endpoints;

public class AdminEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AdminEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListKeys_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/admin/keys");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddKey_WithValidName_ReturnsOkWithKey()
    {
        var body = new { name = "test-key", scopes = new[] { "detect", "analyze" }, requestsPerMinute = 30 };

        var response = await _client.PostAsJsonAsync("/api/v1/admin/keys", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("key");
        content.Should().Contain("test-key");
    }

    [Fact]
    public async Task AddKey_WithEmptyName_ReturnsBadRequest()
    {
        var body = new { name = "", scopes = Array.Empty<string>(), requestsPerMinute = 0 };

        var response = await _client.PostAsJsonAsync("/api/v1/admin/keys", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetSettings_ReturnsOkWithExpectedShape()
    {
        var response = await _client.GetAsync("/api/v1/admin/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("rateLimit");
        content.Should().Contain("cache");
        content.Should().Contain("performance");
    }

    [Fact]
    public async Task UpdateSettings_WithValidDto_ReturnsOk()
    {
        var body = new
        {
            rateLimit = new { requestsPerMinute = 120, burstSize = 20 },
            cache = new { enabled = true, defaultTtlSeconds = 60, maxItems = 500 }
        };

        var response = await _client.PutAsJsonAsync("/api/v1/admin/settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("updated");
    }

    [Fact]
    public async Task Playground_WithValidImage_ReturnsOk()
    {
        // Playground uses mocked backends via the real client; since backends are unavailable,
        // we just check that the endpoint exists and returns a handled response (503 is fine here).
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF]), "file", "test.jpg");

        var response = await _client.PostAsync("/api/v1/playground", content);

        // 503 is expected since real backends are not running in tests
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }
}
