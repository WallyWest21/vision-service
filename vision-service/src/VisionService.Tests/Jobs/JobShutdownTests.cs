using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using VisionService.Clients;
using VisionService.Configuration;
using VisionService.Events;
using VisionService.Jobs;
using VisionService.Services;
using Xunit;

namespace VisionService.Tests.Jobs;

/// <summary>Minimal logger that records log messages for assertion.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = [];
    private readonly object _lock = new();

    /// <summary>Returns a snapshot of all captured messages taken under a lock.</summary>
    public IReadOnlyList<string> Messages
    {
        get { lock (_lock) { return _messages.ToList(); } }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lock) { _messages.Add(formatter(state, exception)); }
    }
}

public class ImageCleanupJobShutdownTests
{
    private sealed class Wrapper(IServiceScopeFactory f, ILogger<ImageCleanupJob> l, IOptionsMonitor<PerformanceOptions> o)
        : ImageCleanupJob(f, l, o)
    {
        public Task InvokeExecuteAsync(CancellationToken ct) => ExecuteAsync(ct);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStopped_LogsShutdownMessage()
    {
        var logger = new CapturingLogger<ImageCleanupJob>();
        var monitor = Substitute.For<IOptionsMonitor<PerformanceOptions>>();
        monitor.CurrentValue.Returns(new PerformanceOptions { ImageCleanupIntervalHours = 1 });

        var imageService = Substitute.For<IImageService>();
        imageService.CleanupExpiredAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));

        var services = new ServiceCollection();
        services.AddSingleton<IImageService>(imageService);
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var job = new Wrapper(scopeFactory, logger, monitor);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await job.InvokeExecuteAsync(cts.Token);

        logger.Messages.Should().Contain(m => m.Contains("Job stopping due to shutdown"));
    }
}

public class ModelHealthCheckJobShutdownTests
{
    private sealed class Wrapper(IServiceScopeFactory f, ILogger<ModelHealthCheckJob> l, IOptionsMonitor<PerformanceOptions> o)
        : ModelHealthCheckJob(f, l, o)
    {
        public Task InvokeExecuteAsync(CancellationToken ct) => ExecuteAsync(ct);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStopped_LogsShutdownMessage()
    {
        var logger = new CapturingLogger<ModelHealthCheckJob>();
        var monitor = Substitute.For<IOptionsMonitor<PerformanceOptions>>();
        monitor.CurrentValue.Returns(new PerformanceOptions { HealthCheckIntervalSeconds = 30 });

        var yolo = Substitute.For<IYoloClient>();
        yolo.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var qwen = Substitute.For<IQwenVlClient>();
        qwen.IsHealthyAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var eventBus = Substitute.For<IVisionEventBus>();

        var services = new ServiceCollection();
        services.AddSingleton<IYoloClient>(yolo);
        services.AddSingleton<IQwenVlClient>(qwen);
        services.AddSingleton<IVisionEventBus>(eventBus);
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var job = new Wrapper(scopeFactory, logger, monitor);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await job.InvokeExecuteAsync(cts.Token);

        logger.Messages.Should().Contain(m => m.Contains("Job stopping due to shutdown"));
    }
}
