using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiClient.Services;

namespace MauiClient.ViewModels;

/// <summary>ViewModel for all YOLO endpoints: detect, segment, classify, pose.</summary>
public partial class YoloViewModel : BaseViewModel
{
    private readonly VisionApiClient _api;

    [ObservableProperty] private float _confidence = 0.5f;
    [ObservableProperty] private string _selectedImagePath = string.Empty;
    [ObservableProperty] private ImageSource? _previewImage;

    private byte[]? _imageBytes;

    public YoloViewModel(VisionApiClient api)
    {
        _api = api;
        Title = "YOLO";
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
        DetectCommand.NotifyCanExecuteChanged();
        SegmentCommand.NotifyCanExecuteChanged();
        ClassifyCommand.NotifyCanExecuteChanged();
        PoseCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task DetectAsync(CancellationToken ct = default)
    {
        await RunApiCallAsync(async () =>
        {
            var r = await _api.DetectAsync(_imageBytes!, Confidence, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms");
            sb.AppendLine($"Objects found: {r.Detections.Count}");
            foreach (var d in r.Detections)
                sb.AppendLine($"  • {d.Label}  {d.Confidence:P0}  [{d.BoundingBox.X1:F0},{d.BoundingBox.Y1:F0}→{d.BoundingBox.X2:F0},{d.BoundingBox.Y2:F0}]");
            return sb.ToString();
        });
    }

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task SegmentAsync(CancellationToken ct = default)
    {
        await RunApiCallAsync(async () =>
        {
            var r = await _api.SegmentAsync(_imageBytes!, Confidence, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms");
            sb.AppendLine($"Instances: {r.Segments.Count}");
            foreach (var s in r.Segments)
                sb.AppendLine($"  • {s.Label}  {s.Confidence:P0}  mask points: {s.Mask.Length / 2}");
            return sb.ToString();
        });
    }

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task ClassifyAsync(CancellationToken ct = default)
    {
        await RunApiCallAsync(async () =>
        {
            var r = await _api.ClassifyAsync(_imageBytes!, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms");
            foreach (var c in r.Classifications)
                sb.AppendLine($"  • {c.Label}  {c.Confidence:P1}");
            return sb.ToString();
        });
    }

    [RelayCommand(CanExecute = nameof(CanCallApi))]
    private async Task PoseAsync(CancellationToken ct = default)
    {
        await RunApiCallAsync(async () =>
        {
            var r = await _api.PoseAsync(_imageBytes!, Confidence, ct);
            var sb = new StringBuilder();
            sb.AppendLine($"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms");
            sb.AppendLine($"People: {r.Poses.Count}");
            foreach (var p in r.Poses)
                sb.AppendLine($"  • conf {p.Confidence:P0}  keypoints: {p.Keypoints.Count}");
            return sb.ToString();
        });
    }

    private bool CanCallApi() => _imageBytes is not null && IsNotBusy;

    private async Task RunApiCallAsync(Func<Task<string>> call)
    {
        IsBusy = true;
        DetectCommand.NotifyCanExecuteChanged();
        SegmentCommand.NotifyCanExecuteChanged();
        ClassifyCommand.NotifyCanExecuteChanged();
        PoseCommand.NotifyCanExecuteChanged();
        try
        {
            SetResult(await call());
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            IsBusy = false;
            DetectCommand.NotifyCanExecuteChanged();
            SegmentCommand.NotifyCanExecuteChanged();
            ClassifyCommand.NotifyCanExecuteChanged();
            PoseCommand.NotifyCanExecuteChanged();
        }
    }
}
