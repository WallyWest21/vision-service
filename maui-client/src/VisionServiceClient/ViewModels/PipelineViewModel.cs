using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionServiceClient.Services;

namespace VisionServiceClient.ViewModels;

/// <summary>View model for the Pipeline page, supporting detect+describe, safety check, inventory, and scene analysis.</summary>
public partial class PipelineViewModel : BaseViewModel
{
    private readonly IVisionApiService _api;

    [ObservableProperty] private string[] _modes = ["Detect+Describe", "Safety Check", "Inventory", "Scene"];
    [ObservableProperty] private string _selectedMode = "Detect+Describe";
    [ObservableProperty] private string _selectedImagePath = string.Empty;
    [ObservableProperty] private string _resultText = string.Empty;

    /// <summary>Initializes the pipeline view model.</summary>
    public PipelineViewModel(IVisionApiService api) => _api = api;

    /// <summary>Opens a file picker to select an image.</summary>
    [RelayCommand]
    private async Task PickImageAsync()
    {
        var result = await FilePicker.PickAsync(PickOptions.Images);
        if (result is not null) SelectedImagePath = result.FullPath;
    }

    /// <summary>Runs the selected pipeline mode against the picked image.</summary>
    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrEmpty(SelectedImagePath)) { SetError("Please select an image."); return; }
        IsBusy = true;
        ClearMessages();
        ResultText = string.Empty;
        try
        {
            var name = Path.GetFileName(SelectedImagePath);
            switch (SelectedMode)
            {
                case "Detect+Describe":
                {
                    await using var s = File.OpenRead(SelectedImagePath);
                    var r = await _api.DetectAndDescribeAsync(s, name);
                    ResultText = $"Objects detected: {r.ObjectCount}\nCaption: {r.Caption}\n\nDetections:\n" +
                        string.Join("\n", r.Detections.Select(d => $"  {d.Label} ({d.Confidence:P1})"));
                    break;
                }
                case "Safety Check":
                {
                    await using var s = File.OpenRead(SelectedImagePath);
                    var r = await _api.SafetyCheckAsync(s, name);
                    ResultText = $"Safe: {(r.IsSafe ? "✅ Yes" : "❌ No")}\n\nAnalysis:\n{r.SafetyAnalysis}\n\nDetections ({r.Detections.Length}):\n" +
                        string.Join("\n", r.Detections.Select(d => $"  {d.Label} ({d.Confidence:P1})"));
                    break;
                }
                case "Inventory":
                {
                    await using var s = File.OpenRead(SelectedImagePath);
                    var r = await _api.InventoryAsync(s, name);
                    ResultText = $"Total detections: {r.TotalDetections}\n\nItem counts:\n" +
                        string.Join("\n", r.ItemCounts.Select(i => $"  {i.Item}: {i.Count}")) +
                        $"\n\nVL Inventory:\n{r.VlInventory}";
                    break;
                }
                case "Scene":
                {
                    await using var s = File.OpenRead(SelectedImagePath);
                    var r = await _api.SceneAsync(s, name);
                    ResultText = $"Detection count: {r.DetectionCount}\nCaption: {r.Caption}\n\nExtracted Text:\n{r.ExtractedText}\n\nObjects:\n" +
                        string.Join("\n", r.Detections.Select(d => $"  {d.Label} ({d.Confidence:P1})"));
                    break;
                }
            }
            SetStatus($"{SelectedMode} completed.");
        }
        catch (Exception ex) { SetError($"Error: {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
