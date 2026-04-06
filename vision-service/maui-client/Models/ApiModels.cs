using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MauiClient.Models;

// ── YOLO ────────────────────────────────────────────────────────────────────

/// <summary>Axis-aligned bounding box in pixel coordinates.</summary>
public class BoundingBox
{
    public float X1 { get; set; }
    public float Y1 { get; set; }
    public float X2 { get; set; }
    public float Y2 { get; set; }
    [JsonIgnore] public float Width => X2 - X1;
    [JsonIgnore] public float Height => Y2 - Y1;
}

/// <summary>Single object detection result.</summary>
public class Detection
{
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
    public int ClassId { get; set; }
}

/// <summary>Response from the detect endpoint.</summary>
public class DetectionResponse
{
    public List<Detection> Detections { get; set; } = [];
    public double ProcessingTimeMs { get; set; }
    public string Model { get; set; } = string.Empty;
}

/// <summary>Instance segmentation result.</summary>
public class Segmentation
{
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
    public float[] Mask { get; set; } = [];
    public int ClassId { get; set; }
}

/// <summary>Response from the segment endpoint.</summary>
public class SegmentationResponse
{
    public List<Segmentation> Segments { get; set; } = [];
    public double ProcessingTimeMs { get; set; }
    public string Model { get; set; } = string.Empty;
}

/// <summary>Single classification result.</summary>
public class ClassificationResult
{
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public int ClassId { get; set; }
}

/// <summary>Response from the classify endpoint.</summary>
public class ClassificationResponse
{
    public List<ClassificationResult> Classifications { get; set; } = [];
    public double ProcessingTimeMs { get; set; }
    public string Model { get; set; } = string.Empty;
}

/// <summary>2-D keypoint.</summary>
public class Keypoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Confidence { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Pose estimation result for one person.</summary>
public class PoseResult
{
    public BoundingBox BoundingBox { get; set; } = new();
    public float Confidence { get; set; }
    public List<Keypoint> Keypoints { get; set; } = [];
}

/// <summary>Response from the pose endpoint.</summary>
public class PoseResponse
{
    public List<PoseResult> Poses { get; set; } = [];
    public double ProcessingTimeMs { get; set; }
    public string Model { get; set; } = string.Empty;
}

// ── Qwen-VL ─────────────────────────────────────────────────────────────────

/// <summary>Response from any Qwen-VL endpoint.</summary>
public class VlResponse
{
    public string Text { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    [JsonIgnore] public int TotalTokens => PromptTokens + CompletionTokens;
}

// ── Admin ────────────────────────────────────────────────────────────────────

/// <summary>Masked API key entry returned by the admin list endpoint.</summary>
public class ApiKeyPreview
{
    public string Name { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public int RequestsPerMinute { get; set; }
    public string KeyPreview { get; set; } = string.Empty;
}

/// <summary>Request body for creating a new API key.</summary>
public class NewApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public string[]? Scopes { get; set; }
    public int RequestsPerMinute { get; set; } = 0;
}

/// <summary>Response when a new API key is created.</summary>
public class NewApiKeyResponse
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
}
