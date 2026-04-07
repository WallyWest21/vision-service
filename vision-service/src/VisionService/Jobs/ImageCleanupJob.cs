using Microsoft.Extensions.Options;
using VisionService.Configuration;
using VisionService.Services;

namespace VisionService.Jobs;

/// <summary>Background job that periodically removes expired images.</summary>
public class ImageCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImageCleanupJob> _logger;
    private readonly IOptionsMonitor<PerformanceOptions> _perfOptions;

    /// <summary>Initializes a new instance of <see cref="ImageCleanupJob"/>.</summary>
    public ImageCleanupJob(IServiceScopeFactory scopeFactory, ILogger<ImageCleanupJob> logger, IOptionsMonitor<PerformanceOptions> perfOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _perfOptions = perfOptions;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImageCleanupJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();
                var deleted = await imageService.CleanupExpiredAsync(stoppingToken);
                _logger.LogInformation("ImageCleanupJob: removed {Count} expired images", deleted);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "ImageCleanupJob encountered an error");
            }

            await Task.Delay(TimeSpan.FromHours(_perfOptions.CurrentValue.ImageCleanupIntervalHours), stoppingToken);
        }
    }
}
