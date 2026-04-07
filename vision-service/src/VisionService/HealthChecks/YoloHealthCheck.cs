using Microsoft.Extensions.Diagnostics.HealthChecks;
using VisionService.Clients;

namespace VisionService.HealthChecks;

/// <summary>Health check that probes the YOLO AI backend.</summary>
public sealed class YoloHealthCheck : IHealthCheck
{
    private readonly IYoloClient _yoloClient;

    /// <summary>Initializes a new instance of <see cref="YoloHealthCheck"/>.</summary>
    public YoloHealthCheck(IYoloClient yoloClient)
    {
        _yoloClient = yoloClient;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var healthy = await _yoloClient.IsHealthyAsync(cancellationToken);
            return healthy
                ? HealthCheckResult.Healthy("YOLO backend is reachable.")
                : HealthCheckResult.Unhealthy("YOLO backend returned unhealthy status.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("YOLO backend probe failed.", ex);
        }
    }
}
