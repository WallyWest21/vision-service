using VisionService.Models;

namespace VisionService.Clients;

/// <summary>Client for the YOLO FastAPI AI backend.</summary>
public interface IYoloClient
{
    /// <summary>Detects objects in an image.</summary>
    Task<IReadOnlyList<Detection>> DetectAsync(Stream image, float confidence = 0.5f, CancellationToken ct = default);

    /// <summary>Performs instance segmentation on an image.</summary>
    Task<IReadOnlyList<Segmentation>> SegmentAsync(Stream image, float confidence = 0.5f, CancellationToken ct = default);

    /// <summary>Classifies the main subject(s) of an image.</summary>
    Task<IReadOnlyList<ClassificationResult>> ClassifyAsync(Stream image, int topN = 5, CancellationToken ct = default);

    /// <summary>Estimates human poses in an image.</summary>
    Task<IReadOnlyList<PoseResult>> PoseAsync(Stream image, float confidence = 0.5f, CancellationToken ct = default);

    /// <summary>Checks whether the YOLO backend is healthy.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
