namespace VisionService.Models;

/// <summary>Single pose keypoint.</summary>
public class Keypoint
{
    /// <summary>Keypoint name (e.g., "nose", "left_shoulder").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>X coordinate in pixels.</summary>
    public float X { get; set; }

    /// <summary>Y coordinate in pixels.</summary>
    public float Y { get; set; }

    /// <summary>Visibility/confidence score (0–1).</summary>
    public float Confidence { get; set; }
}

/// <summary>Full pose estimation result for one person.</summary>
public class PoseResult
{
    /// <summary>Bounding box of the person.</summary>
    public BoundingBox BoundingBox { get; set; } = new();

    /// <summary>Detection confidence score (0–1).</summary>
    public float Confidence { get; set; }

    /// <summary>Keypoints for this person.</summary>
    public List<Keypoint> Keypoints { get; set; } = [];
}
