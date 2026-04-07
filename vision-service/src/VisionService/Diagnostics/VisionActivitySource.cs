using System.Diagnostics;

namespace VisionService.Diagnostics;

/// <summary>
/// Provides the shared <see cref="ActivitySource"/> for VisionService distributed tracing.
/// Register this source with OpenTelemetry via <c>AddSource(VisionActivitySource.Name)</c>.
/// </summary>
public static class VisionActivitySource
{
    /// <summary>The name of the activity source, used to register it with OpenTelemetry.</summary>
    public const string Name = "VisionService";

    /// <summary>
    /// The shared <see cref="ActivitySource"/> instance for VisionService.
    /// This instance is intentionally not disposed as it is a static singleton
    /// that lives for the entire application lifetime.
    /// </summary>
    public static readonly ActivitySource Source = new(Name);
}
