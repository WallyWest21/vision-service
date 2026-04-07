using MauiClient.Models;

namespace MauiClient.Views;

/// <summary>
/// Draws YOLO detection bounding boxes with labels on top of the live feed image.
/// Handles the AspectFit letterbox transform so box coordinates (image pixels) map
/// correctly onto the <see cref="Microsoft.Maui.Controls.GraphicsView"/> canvas.
/// </summary>
public class BoundingBoxDrawable : IDrawable
{
    private static readonly Color[] Palette =
    [
        Color.FromArgb("#FF3B3B"),
        Color.FromArgb("#3BFF3B"),
        Color.FromArgb("#3BFFFF"),
        Color.FromArgb("#FF3BFF"),
        Color.FromArgb("#FF9A3B"),
        Color.FromArgb("#FFFF3B"),
        Color.FromArgb("#3B9AFF"),
        Color.FromArgb("#FF3B9A"),
    ];

    /// <summary>Detection results to render (Detect mode).</summary>
    public IReadOnlyList<Detection> Detections { get; set; } = [];

    /// <summary>Segmentation results to render (Segment mode).</summary>
    public IReadOnlyList<Segmentation> Segments { get; set; } = [];

    /// <summary>Pose estimation results to render (Pose mode).</summary>
    public IReadOnlyList<PoseResult> Poses { get; set; } = [];

    /// <summary>Natural pixel width of the source image.</summary>
    public float ImageWidth { get; set; }

    /// <summary>Natural pixel height of the source image.</summary>
    public float ImageHeight { get; set; }

    /// <inheritdoc/>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (ImageWidth <= 0 || ImageHeight <= 0) return;
        if (Detections.Count == 0 && Segments.Count == 0 && Poses.Count == 0) return;

        // Compute AspectFit letterbox: scale to fit, centre in view
        float scale = Math.Min(dirtyRect.Width / ImageWidth, dirtyRect.Height / ImageHeight);
        float offX = (dirtyRect.Width - ImageWidth * scale) / 2f;
        float offY = (dirtyRect.Height - ImageHeight * scale) / 2f;

        canvas.SaveState();
        canvas.Antialias = true;

        for (int i = 0; i < Detections.Count; i++)
        {
            var d = Detections[i];
            DrawBox(canvas, d.BoundingBox, d.Label, d.Confidence,
                Palette[i % Palette.Length], scale, offX, offY);
        }

        for (int i = 0; i < Segments.Count; i++)
        {
            var s = Segments[i];
            DrawBox(canvas, s.BoundingBox, s.Label, s.Confidence,
                Palette[i % Palette.Length], scale, offX, offY);
        }

        for (int i = 0; i < Poses.Count; i++)
        {
            var p = Poses[i];
            var color = Color.FromArgb("#3BFF3B");
            DrawBox(canvas, p.BoundingBox, "person", p.Confidence, color, scale, offX, offY);
            DrawKeypoints(canvas, p.Keypoints, color, scale, offX, offY);
        }

        canvas.RestoreState();
    }

    private static void DrawBox(ICanvas canvas, BoundingBox box, string label, float confidence,
        Color color, float scale, float offX, float offY)
    {
        float x = offX + box.X1 * scale;
        float y = offY + box.Y1 * scale;
        float w = box.Width * scale;
        float h = box.Height * scale;

        // Bounding rectangle
        canvas.StrokeColor = color;
        canvas.StrokeSize = 2f;
        canvas.DrawRectangle(x, y, w, h);

        // Label chip — placed above the box, or below if near the top edge
        string text = $"{label} {confidence:P0}";
        const float FontSize = 11f;
        const float ChipH = FontSize + 5f;
        float chipW = text.Length * FontSize * 0.58f + 8f;
        float chipY = y >= ChipH ? y - ChipH : y + h;

        canvas.FillColor = color.WithAlpha(0.80f);
        canvas.FillRectangle(x, chipY, chipW, ChipH);

        canvas.FontColor = Colors.White;
        canvas.FontSize = FontSize;
        canvas.DrawString(text, x + 4f, chipY + 2f, chipW, ChipH,
            HorizontalAlignment.Left, VerticalAlignment.Top);
    }

    private static void DrawKeypoints(ICanvas canvas, IList<Keypoint> keypoints,
        Color color, float scale, float offX, float offY)
    {
        canvas.FillColor = color;
        foreach (var kp in keypoints)
        {
            if (kp.Confidence < 0.3f) continue;
            canvas.FillCircle(offX + kp.X * scale, offY + kp.Y * scale, 3f);
        }
    }
}
