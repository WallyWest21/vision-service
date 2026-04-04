using VisionService.Services;

namespace VisionService.Jobs;

/// <summary>Background job that periodically removes expired images.</summary>
public class ImageCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImageCleanupJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    /// <summary>Initializes a new instance of <see cref="ImageCleanupJob"/>.</summary>
    public ImageCleanupJob(IServiceScopeFactory scopeFactory, ILogger<ImageCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
