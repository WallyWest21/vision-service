using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionServiceClient.Models;
using VisionServiceClient.Services;

namespace VisionServiceClient.ViewModels;

/// <summary>View model for the YOLO page, supporting detect, segment, classify, pose, and batch detect.</summary>
public partial class YoloViewModel : BaseViewModel
{
    private readonly IVisionApiService _api;

    [ObservableProperty] private string[] _modes = ["Detect", "Segment", "Classify", "Pose", "Batch Detect"];
    [ObservableProperty] private string _selectedMode = "Detect";
    [ObservableProperty] private string _selectedImagePath = string.Empty;
    [ObservableProperty] private float _confidence = 0.5f;
    [ObservableProperty] private int _topN = 5;
    [ObservableProperty] private string _resultText = string.Empty;

    /// <summary>Initializes the YOLO view model.</summary>
    public YoloViewModel(IVisionApiService api) => _api = api;

    /// <summary>Opens a file picker to select an image.</summary>
    [RelayCommand]
    private async Task PickImageAsync()
    {
        var result = await FilePicker.PickAsync(PickOptions.Images);
        if (result is not null) SelectedImagePath = result.FullPath;
    }

    /// <summary>Runs the selected YOLO analysis mode against the picked image.</summary>
    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrEmpty(SelectedImagePath) && SelectedMode != "Batch Detect")
        {
            SetError("Please select an image first.");
            return;
        }
        IsBusy = true;
        ClearMessages();
        ResultText = string.Empty;
        try
        {
            switch (SelectedMode)
            {
                case "Detect":
                {
                    await using var s = File.OpenRead(SelectedImagePath);
                    var r = await _api.DetectAsync(s, Path.GetFileName(SelectedImagePath), Confidence);
                    ResultText = FormatDetections(r.Detections, r.Model, r.ProcessingTimeMs);
                    break;
                }
                case "Segment":
                {
                    await using var s = File.OpenRead(SelectedImagePath);
                    var r = await _api.SegmentAsync(s, Path.GetFileName(SelectedImagePath), Confidence);
                    ResultText = $"Model: {r.Model}\nSegmentations: {r.Segmentations.Length}\n\n" +
                        string.Join("\n", r.Segmentations.Select(seg =>
                            $"  {seg.Label} ({seg.Confidence:P1}) - Mask points: {seg.Mask.Length / 2}"));
                    break;
                }
                case "Classify":
                {
                    await using var s = File.OpenRead(SelectedImagePath);
                    var r = await _api.ClassifyAsync(s, Path.GetFileName(SelectedImagePath), TopN);
                    ResultText = $"Model: {r.Model}\nTop {TopN} Classifications:\n\n" +
                        string.Join("\n", r.Classifications.Select((c, i) =>
                            $"  {i + 1}. {c.Label} ({c.Confidence:P2})"));
                    break;
                }
                case "Pose":
                {
                    await using var s = File.OpenRead(SelectedImagePath);
                    var r = await _api.PoseAsync(s, Path.GetFileName(SelectedImagePath), Confidence);
                    ResultText = $"Model: {r.Model}\nPeople detected: {r.Poses.Length}\n\n" +
                        string.Join("\n\n", r.Poses.Select((p, i) =>
                            $"Person {i + 1} (conf: {p.Confidence:P1}):\n" +
                            string.Join("\n", p.Keypoints.Select(k =>
                                $"  {k.Name}: ({k.X:F0},{k.Y:F0}) conf:{k.Confidence:P1}"))
                        ));
                    break;
                }
                case "Batch Detect":
                {
                    var results = await FilePicker.PickMultipleAsync(PickOptions.Images);
                    if (results is null || !results.Any()) { SetError("No images selected."); return; }
                    var files = new List<(Stream, string)>();
                    foreach (var f in results) files.Add((File.OpenRead(f.FullPath), f.FileName));
                    try
                    {
                        var r = await _api.DetectBatchAsync(files, Confidence);
                        ResultText = $"Batch results ({r.Length} images):\n\n" +
                            string.Join("\n\n", r.Select(item =>
                                $"[{item.FileName}]\n" +
                                string.Join("\n", item.Detections.Select(d =>
                                    $"  {d.Label} ({d.Confidence:P1})"))));
                    }
                    finally { foreach (var (s, _) in files) await s.DisposeAsync(); }
                    break;
                }
            }
            SetStatus($"{SelectedMode} completed successfully.");
        }
        catch (Exception ex) { SetError($"Error: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private static string FormatDetections(Detection[] detections, string model, long ms)
        => $"Model: {model}\nProcessing: {ms}ms\nDetections: {detections.Length}\n\n" +
           string.Join("\n", detections.Select(d =>
               $"  {d.Label} ({d.Confidence:P1}) @ [{d.BoundingBox.X1:F0},{d.BoundingBox.Y1:F0}]-[{d.BoundingBox.X2:F0},{d.BoundingBox.Y2:F0}]"));
}
