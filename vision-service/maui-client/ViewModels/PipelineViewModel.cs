using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiClient.Services;

namespace MauiClient.ViewModels;

/// <summary>ViewModel for all four pipeline endpoints.</summary>
public partial class PipelineViewModel : BaseViewModel
{
    private readonly VisionApiClient _api;

    [ObservableProperty] private string _selectedImagePath = string.Empty;
    [ObservableProperty] private ImageSource? _previewImage;

    private byte[]? _imageBytes;

    private static readonly JsonSerializerOptions _prettyJson =
        new() { WriteIndented = true };

    public PipelineViewModel(VisionApiClient api)
    {
        _api = api;
        Title = "Pipeline";
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select an image",
            FileTypes = FilePickerFileType.Images
        });
        if (result is null) return;
        SelectedImagePath = result.FullPath;
        _imageBytes = await File.ReadAllBytesAsync(result.FullPath);
        PreviewImage = ImageSource.FromFile(result.FullPath);
        SetResult(string.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task DetectAndDescribeAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.DetectAndDescribeAsync(_imageBytes!, ct), ct);

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task SafetyCheckAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.SafetyCheckAsync(_imageBytes!, ct), ct);

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task InventoryAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.InventoryAsync(_imageBytes!, ct), ct);

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task SceneAsync(CancellationToken ct = default) =>
        await RunAsync(() => _api.SceneAsync(_imageBytes!, ct), ct);

    private bool CanCallApi() => _imageBytes is not null && IsNotBusy;

    private async Task RunAsync(Func<Task<JsonElement>> call, CancellationToken ct)
    {
        IsBusy = true;
        NotifyAllCommands();
        try
        {
            var result = await call();
            SetResult(JsonSerializer.Serialize(result, _prettyJson));
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
        DetectAndDescribeCommand.NotifyCanExecuteChanged();
        SafetyCheckCommand.NotifyCanExecuteChanged();
        InventoryCommand.NotifyCanExecuteChanged();
        SceneCommand.NotifyCanExecuteChanged();
    }
}
