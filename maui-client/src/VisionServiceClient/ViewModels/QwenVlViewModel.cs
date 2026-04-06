using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionServiceClient.Models;
using VisionServiceClient.Services;

namespace VisionServiceClient.ViewModels;

/// <summary>View model for the Qwen-VL page, supporting ask, caption, OCR, analyze, compare, and describe modes.</summary>
public partial class QwenVlViewModel : BaseViewModel
{
    private readonly IVisionApiService _api;

    [ObservableProperty] private string[] _modes = ["Ask", "Caption", "OCR", "Analyze", "Compare", "Describe"];
    [ObservableProperty] private string _selectedMode = "Ask";
    [ObservableProperty] private string _selectedImagePath = string.Empty;
    [ObservableProperty] private string _selectedImage2Path = string.Empty;
    [ObservableProperty] private string _question = string.Empty;
    [ObservableProperty] private string _systemPrompt = string.Empty;
    [ObservableProperty] private string _resultText = string.Empty;
    [ObservableProperty] private bool _showQuestion;
    [ObservableProperty] private bool _showSystemPrompt;
    [ObservableProperty] private bool _showSecondImage;

    /// <summary>Initializes the Qwen-VL view model and sets initial UI visibility.</summary>
    public QwenVlViewModel(IVisionApiService api)
    {
        _api = api;
        UpdateVisibility();
    }

    partial void OnSelectedModeChanged(string value) => UpdateVisibility();

    private void UpdateVisibility()
    {
        ShowQuestion = SelectedMode == "Ask";
        ShowSystemPrompt = SelectedMode == "Analyze";
        ShowSecondImage = SelectedMode == "Compare";
    }

    /// <summary>Opens a file picker to select the primary image.</summary>
    [RelayCommand]
    private async Task PickImageAsync()
    {
        var result = await FilePicker.PickAsync(PickOptions.Images);
        if (result is not null) SelectedImagePath = result.FullPath;
    }

    /// <summary>Opens a file picker to select the second image for compare mode.</summary>
    [RelayCommand]
    private async Task PickImage2Async()
    {
        var result = await FilePicker.PickAsync(PickOptions.Images);
        if (result is not null) SelectedImage2Path = result.FullPath;
    }

    /// <summary>Runs the selected vision-language mode against the picked image(s).</summary>
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
            VlResponse r;
            await using var s = File.OpenRead(SelectedImagePath);
            switch (SelectedMode)
            {
                case "Ask":
                    if (string.IsNullOrWhiteSpace(Question)) { SetError("Please enter a question."); return; }
                    r = await _api.AskAsync(s, Path.GetFileName(SelectedImagePath), Question);
                    break;
                case "Caption":
                    r = await _api.CaptionAsync(s, Path.GetFileName(SelectedImagePath));
                    break;
                case "OCR":
                    r = await _api.OcrAsync(s, Path.GetFileName(SelectedImagePath));
                    break;
                case "Analyze":
                    if (string.IsNullOrWhiteSpace(SystemPrompt)) { SetError("Please enter a system prompt."); return; }
                    r = await _api.AnalyzeAsync(s, Path.GetFileName(SelectedImagePath), SystemPrompt);
                    break;
                case "Compare":
                    if (string.IsNullOrEmpty(SelectedImage2Path)) { SetError("Please select a second image."); return; }
                    await using (var s2 = File.OpenRead(SelectedImage2Path))
                    {
                        r = await _api.CompareAsync(s, Path.GetFileName(SelectedImagePath), s2, Path.GetFileName(SelectedImage2Path));
                    }
                    break;
                case "Describe":
                    r = await _api.DescribeDetailedAsync(s, Path.GetFileName(SelectedImagePath));
                    break;
                default:
                    return;
            }
            ResultText = $"Model: {r.Model}\nTokens: {r.PromptTokens} prompt + {r.CompletionTokens} completion\n\n{r.Text}";
            SetStatus($"{SelectedMode} completed successfully.");
        }
        catch (Exception ex) { SetError($"Error: {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
