using Microsoft.Extensions.Diagnostics.HealthChecks;
using VisionService.Clients;

namespace VisionService.HealthChecks;

/// <summary>Health check that probes the Qwen-VL AI backend.</summary>
public sealed class QwenVlHealthCheck : IHealthCheck
{
    private readonly IQwenVlClient _qwenVlClient;

    /// <summary>Initializes a new instance of <see cref="QwenVlHealthCheck"/>.</summary>
    public QwenVlHealthCheck(IQwenVlClient qwenVlClient)
    {
        _qwenVlClient = qwenVlClient;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var healthy = await _qwenVlClient.IsHealthyAsync(cancellationToken);
            return healthy
                ? HealthCheckResult.Healthy("Qwen-VL backend is reachable.")
                : HealthCheckResult.Unhealthy("Qwen-VL backend returned unhealthy status.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Qwen-VL backend probe failed.", ex);
        }
    }
}
