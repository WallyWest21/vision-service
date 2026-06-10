namespace VisionService.Models;

/// <summary>
/// Structured inventory fields extracted from a single image by Qwen-VL.
/// Always treated as a <b>draft for human confirmation</b> — the vision model
/// reads what it can (labels via OCR, object class) and infers the rest, so it
/// must never write straight into the source-of-truth inventory.
/// </summary>
public class InventoryItemExtraction
{
    /// <summary>Best-guess product name. Strongest when a brand/model label is visible.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One of the caller-supplied categories (constrained by guided decoding).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Short human-readable descriptor (e.g. "Cordless power tool").</summary>
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>Capabilities the item affords — <b>inferred</b> from the recognised product, not "seen".</summary>
    public List<string> Capabilities { get; set; } = [];

    /// <summary>Known limitations — also inferred; confirm before relying on them.</summary>
    public List<string> Limitations { get; set; } = [];

    /// <summary>Count of distinct instances visible (1 if a single item / unsure).</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Raw text read off the item/label via OCR — drives <see cref="Name"/> reliability.</summary>
    public string VisibleText { get; set; } = string.Empty;

    /// <summary>Model self-rated confidence in the identification, 0.0–1.0.</summary>
    public double Confidence
    {
        get; set;
    }

    /// <summary>
    /// True when the result must be checked by a human before import — always set when
    /// confidence is low or JSON parsing fell back. Callers should keep the confirm step regardless.
    /// </summary>
    public bool NeedsReview { get; set; } = true;

    /// <summary>Raw model output, retained for debugging and when structured parsing fails.</summary>
    public string? RawResponse
    {
        get; set;
    }
}
