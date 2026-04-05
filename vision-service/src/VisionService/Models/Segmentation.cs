namespace VisionService.Models;

/// <summary>Segmentation result for a single instance.</summary>
public class Segmentation
{
    /// <summary>Object class label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Detection confidence score (0–1).</summary>
    public float Confidence { get; set; }

    /// <summary>Bounding box of the instance.</summary>
    public BoundingBox BoundingBox { get; set; } = new();

    /// <summary>Polygon mask points as flat [x, y, x, y, ...] array.</summary>
    public float[] Mask { get; set; } = [];

    /// <summary>Class ID.</summary>
    public int ClassId { get; set; }
}
