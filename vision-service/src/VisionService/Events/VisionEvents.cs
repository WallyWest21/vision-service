using VisionService.Models;

namespace VisionService.Events;

/// <summary>Raised when a detection operation completes.</summary>
public class DetectionCompleted
{
    /// <summary>List of detected objects.</summary>
    public IReadOnlyList<Detection> Detections { get; init; } = [];

    /// <summary>Processing time in milliseconds.</summary>
    public long ProcessingTimeMs { get; init; }
}

/// <summary>Raised when a vision-language analysis completes.</summary>
public class AnalysisCompleted
{
    /// <summary>The generated text response.</summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>Operation type (caption, ocr, ask, etc.).</summary>
    public string OperationType { get; init; } = string.Empty;
}

/// <summary>Raised when an AI backend becomes unhealthy.</summary>
public class BackendUnhealthy
{
    /// <summary>Backend name (YOLO or QwenVL).</summary>
    public string BackendName { get; init; } = string.Empty;

    /// <summary>Reason for unhealthy status.</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Raised when an AI backend recovers after being unhealthy.</summary>
public class BackendRecovered
{
    /// <summary>Backend name (YOLO or QwenVL).</summary>
    public string BackendName { get; init; } = string.Empty;
}
