using Microsoft.Extensions.Options;
using VisionService.Clients;
using VisionService.Configuration;
using VisionService.Events;

namespace VisionService.Jobs;

/// <summary>Background job that periodically checks AI backend health.</summary>
public class ModelHealthCheckJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModelHealthCheckJob> _logger;
    private readonly IOptionsMonitor<PerformanceOptions> _perfOptions;
    private bool _yoloWasHealthy = true;
    private bool _qwenWasHealthy = true;

    /// <summary>Initializes a new instance of <see cref="ModelHealthCheckJob"/>.</summary>
    public ModelHealthCheckJob(IServiceScopeFactory scopeFactory, ILogger<ModelHealthCheckJob> logger, IOptionsMonitor<PerformanceOptions> perfOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _perfOptions = perfOptions;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ModelHealthCheckJob started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var yolo = scope.ServiceProvider.GetRequiredService<IYoloClient>();
                var qwen = scope.ServiceProvider.GetRequiredService<IQwenVlClient>();
                var eventBus = scope.ServiceProvider.GetRequiredService<IVisionEventBus>();

                _yoloWasHealthy = await CheckBackendAsync("YOLO", () => yolo.IsHealthyAsync(stoppingToken),
                    _yoloWasHealthy, eventBus, stoppingToken);

                _qwenWasHealthy = await CheckBackendAsync("QwenVL", () => qwen.IsHealthyAsync(stoppingToken),
                    _qwenWasHealthy, eventBus, stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(_perfOptions.CurrentValue.HealthCheckIntervalSeconds), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the host signals shutdown
        }

        _logger.LogInformation("Job stopping due to shutdown");
    }

    private async Task<bool> CheckBackendAsync(string name, Func<Task<bool>> check,
        bool wasHealthy, IVisionEventBus eventBus, CancellationToken ct)
    {
        try
        {
            var isHealthy = await check();
            if (!isHealthy && wasHealthy)
            {
                _logger.LogWarning("{Backend} backend became unhealthy", name);
                await eventBus.PublishAsync(new BackendUnhealthy { BackendName = name, Reason = "Health check failed" }, ct);
                return false;
            }
            else if (isHealthy && !wasHealthy)
            {
                _logger.LogInformation("{Backend} backend recovered", name);
                await eventBus.PublishAsync(new BackendRecovered { BackendName = name }, ct);
                return true;
            }
            return wasHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking {Backend} backend health", name);
            return wasHealthy;
        }
    }
}
