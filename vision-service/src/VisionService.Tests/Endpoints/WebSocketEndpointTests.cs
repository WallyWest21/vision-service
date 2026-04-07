using System.Net.WebSockets;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VisionService.Configuration;
using Xunit;

namespace VisionService.Tests.Endpoints;

public class WebSocketEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebSocketEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Stream_WithValidApiKey_ConnectsSuccessfully()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<AuthOptions>(opts =>
                {
                    opts.Enabled = true;
                    opts.ApiKeys =
                    [
                        new ApiKeyEntry { Key = "ws-test-key", Name = "test", Scopes = ["stream"] }
                    ];
                });
            }));

        var wsClient = factory.Server.CreateWebSocketClient();
        using var ws = await wsClient.ConnectAsync(
            new Uri("ws://localhost/ws/stream?apiKey=ws-test-key"),
            CancellationToken.None);

        ws.State.Should().Be(WebSocketState.Open);
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    [Fact]
    public async Task Stream_WithNoApiKey_WhenAuthEnabled_Returns401()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<AuthOptions>(opts =>
                {
                    opts.Enabled = true;
                    opts.ApiKeys =
                    [
                        new ApiKeyEntry { Key = "secret", Name = "test", Scopes = ["stream"] }
                    ];
                });
            }));

        var wsClient = factory.Server.CreateWebSocketClient();

        Func<Task> act = () => wsClient.ConnectAsync(
            new Uri("ws://localhost/ws/stream"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*401*");
    }

    [Fact]
    public async Task Stream_WhenConnectionLimitReached_Returns503()
    {
        // Inject a pre-exhausted semaphore (initialCount=0, maximumCount=1) to simulate
        // all connection slots being occupied.
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddKeyedSingleton<SemaphoreSlim>(
                    "ws-connection-limit",
                    new SemaphoreSlim(0, 1));
            }));

        var wsClient = factory.Server.CreateWebSocketClient();

        Func<Task> act = () => wsClient.ConnectAsync(
            new Uri("ws://localhost/ws/stream"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*503*");
    }
}
