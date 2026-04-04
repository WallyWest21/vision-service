namespace VisionService.Models;

/// <summary>Represents a single detected object.</summary>
public class Detection
{
    /// <summary>Object class label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Detection confidence score (0–1).</summary>
    public float Confidence { get; set; }

    /// <summary>Bounding box of the detected object.</summary>
    public BoundingBox BoundingBox { get; set; } = new();

    /// <summary>Class ID.</summary>
    public int ClassId { get; set; }
}

/// <summary>Axis-aligned bounding box in pixel coordinates.</summary>
public class BoundingBox
{
    /// <summary>Left edge (x1).</summary>
    public float X1 { get; set; }

    /// <summary>Top edge (y1).</summary>
    public float Y1 { get; set; }

    /// <summary>Right edge (x2).</summary>
    public float X2 { get; set; }

    /// <summary>Bottom edge (y2).</summary>
    public float Y2 { get; set; }

    /// <summary>Width of the box.</summary>
    public float Width => X2 - X1;

    /// <summary>Height of the box.</summary>
    public float Height => Y2 - Y1;
}
