using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiClient.Services;

namespace MauiClient.ViewModels;

/// <summary>ViewModel for all Qwen-VL endpoints.</summary>
public partial class QwenVlViewModel : BaseViewModel
{
    private readonly VisionApiClient _api;

    [ObservableProperty] private string _selectedImagePath = string.Empty;
    [ObservableProperty] private ImageSource? _previewImage;
    [ObservableProperty] private string _question = string.Empty;
    [ObservableProperty] private string _systemPrompt = string.Empty;
    [ObservableProperty] private string _selectedImageBPath = string.Empty;
    [ObservableProperty] private ImageSource? _previewImageB;

    private byte[]? _imageBytes;
    private byte[]? _imageBBytes;

    public QwenVlViewModel(VisionApiClient api)
    {
        _api = api;
        Title = "Qwen-VL";
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select image A",
            FileTypes = FilePickerFileType.Images
        });
        if (result is null) return;
        SelectedImagePath = result.FullPath;
        _imageBytes = await File.ReadAllBytesAsync(result.FullPath);
        PreviewImage = ImageSource.FromFile(result.FullPath);
        SetResult(string.Empty);
    }

    [RelayCommand]
    private async Task PickImageBAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select image B (for Compare)",
            FileTypes = FilePickerFileType.Images
        });
        if (result is null) return;
        SelectedImageBPath = result.FullPath;
        _imageBBytes = await File.ReadAllBytesAsync(result.FullPath);
        PreviewImageB = ImageSource.FromFile(result.FullPath);
    }

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task CaptionAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.CaptionAsync(_imageBytes!, ct), ct);

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task AskAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.AskAsync(_imageBytes!, Question, ct), ct);

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task OcrAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.OcrAsync(_imageBytes!, ct), ct);

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task AnalyzeAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.AnalyzeAsync(_imageBytes!, SystemPrompt, ct), ct);

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task DescribeDetailedAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.DescribeDetailedAsync(_imageBytes!, ct), ct);

    [RelayCommand(CanExecute = nameof(CanCompare))]
    private async Task CompareAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.CompareAsync(_imageBytes!, _imageBBytes!, ct), ct);

    private bool CanCallApi() => _imageBytes is not null && IsNotBusy;
    private bool CanCompare() => _imageBytes is not null && _imageBBytes is not null && IsNotBusy;

    private async Task RunAsync(Func<Task<Models.VlResponse>> call, CancellationToken ct)
    {
        IsBusy = true;
        NotifyAllCommands();
        try
        {
            var r = await call();
            var sb = new StringBuilder();
            sb.AppendLine($"Model: {r.Model}  |  tokens in:{r.PromptTokens} out:{r.CompletionTokens}");
            sb.AppendLine();
            sb.Append(r.Text);
            SetResult(sb.ToString());
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            IsBusy = false;
            NotifyAllCommands();
        }
    }

    private void NotifyAllCommands()
    {
        CaptionCommand.NotifyCanExecuteChanged();
        AskCommand.NotifyCanExecuteChanged();
        OcrCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
        DescribeDetailedCommand.NotifyCanExecuteChanged();
        CompareCommand.NotifyCanExecuteChanged();
    }
}
