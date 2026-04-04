namespace VisionService.Models;

/// <summary>Single classification result.</summary>
public class ClassificationResult
{
    /// <summary>Class label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Confidence score (0–1).</summary>
    public float Confidence { get; set; }

    /// <summary>Class ID.</summary>
    public int ClassId { get; set; }
}
