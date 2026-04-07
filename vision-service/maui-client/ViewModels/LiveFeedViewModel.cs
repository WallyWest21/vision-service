using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiClient.Models;
using MauiClient.Services;
using System.Collections.ObjectModel;

namespace MauiClient.ViewModels;

/// <summary>ViewModel for the Live Feed page: streams MJPEG or polls a snapshot URL,
/// running the selected AI analysis on every captured frame.</summary>
public partial class LiveFeedViewModel : BaseViewModel
{
    private readonly VisionApiClient _api;
    private readonly MjpegStreamReader _streamReader;
    private readonly ILocalCameraService _localCamera;
    private CancellationTokenSource? _cts;
    private readonly Queue<long> _frameTimes = new();
    private bool _processingFrame;
    private long _lastAiCallTick;
    private const int MinAiIntervalMs = 500;

    /// <summary>Stream or snapshot URL to connect to.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartStreamCommand))]
    private string _streamUrl = string.Empty;

    /// <summary>Whether the live feed is currently active.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotStreaming))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(StartStreamCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopStreamCommand))]
    private bool _isStreaming;

    /// <summary>Currently selected feed mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSnapshotMode))]
    [NotifyPropertyChangedFor(nameof(IsLocalCameraMode))]
    [NotifyPropertyChangedFor(nameof(IsUrlMode))]
    [NotifyCanExecuteChangedFor(nameof(StartStreamCommand))]
    private string _selectedFeedMode = "MJPEG Stream";

    /// <summary>Camera devices available for the Local Camera feed mode.</summary>
    public ObservableCollection<CameraDeviceInfo> CameraDevices { get; } = [];

    /// <summary>Currently selected local camera device.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartStreamCommand))]
    private CameraDeviceInfo? _selectedCameraDevice;

    /// <summary><c>true</c> when camera devices are being enumerated.</summary>
    [ObservableProperty]
    private bool _isScanningCameras;

    /// <summary>Poll interval in milliseconds used in Snapshot Poll mode.</summary>
    [ObservableProperty]
    private int _pollIntervalMs = 500;

    /// <summary>Currently selected AI analysis mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConfidence))]
    [NotifyPropertyChangedFor(nameof(IsSmartQueryMode))]
    private string _selectedMode = "Detect";

    /// <summary>Confidence threshold for detection, segmentation and pose endpoints.</summary>
    [ObservableProperty]
    private float _confidence = 0.5f;

    /// <summary>Latest captured frame displayed as a live preview.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFrameAvailable))]
    [NotifyPropertyChangedFor(nameof(IsFrameNotAvailable))]
    private ImageSource? _currentFrame;

    /// <summary><c>true</c> once at least one frame has been received.</summary>
    public bool IsFrameAvailable => CurrentFrame is not null;

    /// <summary><c>true</c> when no frame has been received yet (placeholder visibility).</summary>
    public bool IsFrameNotAvailable => CurrentFrame is null;

    /// <summary>Calculated frames per second over the last 30 frames.</summary>
    [ObservableProperty]
    private double _fps;

    /// <summary>Total number of frames received since the feed started.</summary>
    [ObservableProperty]
    private int _frameCount;

    /// <summary>Processing time of the most recent AI call in milliseconds.</summary>
    [ObservableProperty]
    private double _processingTimeMs;

    /// <summary>Current frame detections used for the bounding box overlay.</summary>
    [ObservableProperty]
    private IReadOnlyList<Detection> _currentDetections = [];

    /// <summary>Current frame segmentation results used for the bounding box overlay.</summary>
    [ObservableProperty]
    private IReadOnlyList<Segmentation> _currentSegments = [];

    /// <summary>Current frame pose results used for the bounding box overlay.</summary>
    [ObservableProperty]
    private IReadOnlyList<PoseResult> _currentPoses = [];

    /// <summary>Natural pixel width of the current frame, used to map bounding box coordinates.</summary>
    [ObservableProperty]
    private float _imageNaturalWidth;

    /// <summary>Natural pixel height of the current frame, used to map bounding box coordinates.</summary>
    [ObservableProperty]
    private float _imageNaturalHeight;

    /// <summary>Available feed modes.</summary>
    public string[] FeedModes { get; } = ["MJPEG Stream", "Snapshot Poll", "Local Camera"];

    /// <summary>Available AI analysis modes.</summary>
    public string[] Modes { get; } = ["Detect", "Segment", "Classify", "Pose", "Smart Query"];

    /// <summary>Natural-language query specifying which objects to watch for in Smart Query mode.</summary>
    [ObservableProperty]
    private string _objectQuery = string.Empty;

    /// <summary>System prompt sent to Qwen-VL in Smart Query mode (pre-filled with the default spatial-identification prompt).</summary>
    [ObservableProperty]
    private string _systemPrompt =
        "You are a real-time vision analysis assistant monitoring a live camera feed. " +
        "The user will specify one or more objects or conditions to watch for. " +
        "Your task:\n" +
        "1. State clearly whether each queried object/condition is PRESENT or ABSENT in the frame.\n" +
        "2. For each present object, describe its position using spatial terms " +
        "(top-left, top-center, top-right, center-left, center, center-right, bottom-left, bottom-center, bottom-right) " +
        "and estimate how much of the frame it occupies (small / medium / large).\n" +
        "3. Report any additional relevant context (e.g., partial occlusion, number of instances, notable attributes).\n" +
        "4. Keep responses concise and structured — one bullet per object. " +
        "Do not describe unrelated scene elements unless directly relevant to the query.";

    /// <summary><c>true</c> when the feed is not running.</summary>
    public bool IsNotStreaming => !IsStreaming;

    /// <summary><c>true</c> when Snapshot Poll mode is selected.</summary>
    public bool IsSnapshotMode => SelectedFeedMode == "Snapshot Poll";

    /// <summary><c>true</c> when Local Camera mode is selected.</summary>
    public bool IsLocalCameraMode => SelectedFeedMode == "Local Camera";

    /// <summary><c>true</c> when a URL-based mode (MJPEG or Snapshot) is selected.</summary>
    public bool IsUrlMode => !IsLocalCameraMode;

    /// <summary><c>true</c> for modes that accept a confidence threshold.</summary>
    public bool ShowConfidence => SelectedMode is "Detect" or "Segment" or "Pose";

    /// <summary><c>true</c> when Smart Query mode is selected.</summary>
    public bool IsSmartQueryMode => SelectedMode == "Smart Query";

    /// <summary>Human-readable status shown in the stats row.</summary>
    public string StatusText => IsStreaming ? "● Live" : "○ Stopped";

    /// <summary>Initialises a new <see cref="LiveFeedViewModel"/>.</summary>
    public LiveFeedViewModel(VisionApiClient api, MjpegStreamReader streamReader, ILocalCameraService localCamera)
    {
        _api = api;
        _streamReader = streamReader;
        _localCamera = localCamera;
        Title = "Live Feed";
    }

    /// <summary>Enumerates all locally attached camera devices (USB / wireless).</summary>
    [RelayCommand]
    private async Task ScanCamerasAsync()
    {
        IsScanningCameras = true;
        CameraDevices.Clear();
        SelectedCameraDevice = null;

        try
        {
            var devices = await _localCamera.GetCamerasAsync();
            foreach (var d in devices)
                CameraDevices.Add(d);

            SelectedCameraDevice = CameraDevices.FirstOrDefault();

            if (CameraDevices.Count == 0)
                SetResult("No camera devices found.");
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            IsScanningCameras = false;
        }
    }

    /// <summary>Starts the live feed and AI analysis loop.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartStreamAsync()
    {
        _cts = new CancellationTokenSource();
        IsStreaming = true;
        FrameCount = 0;
        _frameTimes.Clear();
        SetResult(string.Empty);

        try
        {
            if (IsLocalCameraMode)
            {
                await foreach (var frame in _localCamera.CaptureFramesAsync(SelectedCameraDevice!.Id, _cts.Token))
                    await ProcessFrameAsync(frame, _cts.Token);
            }
            else
            {
                int interval = IsSnapshotMode ? PollIntervalMs : 500;
                await foreach (var frame in _streamReader.ReadFramesAsync(StreamUrl, interval, _cts.Token))
                    await ProcessFrameAsync(frame, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => SetError(ex));
        }
        finally
        {
            IsStreaming = false;
            _processingFrame = false;
        }
    }

    /// <summary>Stops the live feed.</summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void StopStream()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        CurrentDetections = [];
        CurrentSegments = [];
        CurrentPoses = [];
    }

    private bool CanStart() => !IsStreaming &&
        (IsLocalCameraMode ? SelectedCameraDevice is not null
                           : !string.IsNullOrWhiteSpace(StreamUrl));
    private bool CanStop() => IsStreaming;

    private async Task ProcessFrameAsync(byte[] frameBytes, CancellationToken ct)
    {
        // Always update the live preview image regardless of AI processing state
        var copy = frameBytes;
        var (imgW, imgH) = ReadImageDimensions(frameBytes);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentFrame = ImageSource.FromStream(() => new MemoryStream(copy));
            FrameCount++;
            UpdateFps();
            if (imgW > 0 && imgH > 0)
            {
                ImageNaturalWidth = imgW;
                ImageNaturalHeight = imgH;
            }
        });

        // Skip AI call if previous analysis is still running or throttle interval not elapsed
        if (_processingFrame) return;
        var nowTick = Environment.TickCount64;
        if (nowTick - _lastAiCallTick < MinAiIntervalMs) return;
        _processingFrame = true;
        _lastAiCallTick = nowTick;

        try
        {
            var sw = Stopwatch.StartNew();
            DetectionResponse? detections = null;
            SegmentationResponse? segments = null;
            PoseResponse? poses = null;
            SmartQueryResponse? smartQuery = null;
            string result = SelectedMode switch
            {
                "Detect"      => FormatDetections(detections = await _api.DetectAsync(frameBytes, Confidence, ct)),
                "Segment"     => FormatSegments(segments = await _api.SegmentAsync(frameBytes, Confidence, ct)),
                "Classify"    => FormatClassifications(await _api.ClassifyAsync(frameBytes, ct)),
                "Pose"        => FormatPoses(poses = await _api.PoseAsync(frameBytes, Confidence, ct)),
                "Smart Query" => FormatSmartQuery(smartQuery = await _api.SmartQueryAsync(frameBytes, ObjectQuery, SystemPrompt, ct)),
                _             => string.Empty
            };
            sw.Stop();
            var elapsed = sw.Elapsed.TotalMilliseconds;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ProcessingTimeMs = elapsed;
                SetResult(result);
                CurrentDetections = smartQuery?.Detections ?? detections?.Detections ?? [];
                CurrentSegments = segments?.Segments ?? [];
                CurrentPoses = poses?.Poses ?? [];
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            MainThread.BeginInvokeOnMainThread(() => SetError(ex));
        }
        finally
        {
            _processingFrame = false;
        }
    }

    /// <summary>
    /// Extracts the pixel dimensions from the header of a JPEG or PNG byte array
    /// without fully decoding the image.
    /// </summary>
    private static (int Width, int Height) ReadImageDimensions(byte[] data)
    {
        if (data.Length < 24) return (0, 0);

        // PNG: 8-byte signature, then IHDR chunk — width at offset 16, height at 20
        if (data[0] == 0x89 && data[1] == 0x50)
        {
            return (
                (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19],
                (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23]
            );
        }

        // JPEG: scan for SOF0–SOF3 markers (0xFFC0–0xFFC3)
        if (data[0] == 0xFF && data[1] == 0xD8)
        {
            int pos = 2;
            while (pos + 3 < data.Length)
            {
                if (data[pos] != 0xFF) break;
                byte marker = data[pos + 1];
                if (marker == 0xD9) break; // EOI
                if (marker is >= 0xC0 and <= 0xC3 && pos + 8 < data.Length)
                {
                    return (
                        (data[pos + 7] << 8) | data[pos + 8],
                        (data[pos + 5] << 8) | data[pos + 6]
                    );
                }
                int segLen = (data[pos + 2] << 8) | data[pos + 3];
                if (segLen < 2) break;
                pos += 2 + segLen;
            }
        }

        return (0, 0);
    }

    private void UpdateFps()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _frameTimes.Enqueue(now);
        while (_frameTimes.Count > 30) _frameTimes.Dequeue();
        if (_frameTimes.Count >= 2)
        {
            double span = (now - _frameTimes.Peek()) / 1000.0;
            Fps = span > 0 ? (_frameTimes.Count - 1) / span : 0;
        }
    }

    private static string FormatDetections(DetectionResponse r)
    {
        if (r.Detections.Count == 0)
            return $"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms\nNo detections.";
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms  |  {r.Detections.Count} object(s)");
        foreach (var d in r.Detections)
            lines.AppendLine($"  • {d.Label}  {d.Confidence:P0}  [{d.BoundingBox.X1:F0},{d.BoundingBox.Y1:F0}→{d.BoundingBox.X2:F0},{d.BoundingBox.Y2:F0}]");
        return lines.ToString().TrimEnd();
    }

    private static string FormatSegments(SegmentationResponse r)
    {
        if (r.Segments.Count == 0)
            return $"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms\nNo instances.";
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms  |  {r.Segments.Count} instance(s)");
        foreach (var s in r.Segments)
            lines.AppendLine($"  • {s.Label}  {s.Confidence:P0}  mask pts: {s.Mask.Length / 2}");
        return lines.ToString().TrimEnd();
    }

    private static string FormatClassifications(ClassificationResponse r)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms");
        foreach (var c in r.Classifications)
            lines.AppendLine($"  • {c.Label}  {c.Confidence:P1}");
        return lines.ToString().TrimEnd();
    }

    private static string FormatPoses(PoseResponse r)
    {
        if (r.Poses.Count == 0)
            return $"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms\nNo people detected.";
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Model: {r.Model}  |  {r.ProcessingTimeMs:F0} ms  |  {r.Poses.Count} person/people");
        foreach (var p in r.Poses)
            lines.AppendLine($"  • conf {p.Confidence:P0}  keypoints: {p.Keypoints.Count}");
        return lines.ToString().TrimEnd();
    }

    private static string FormatSmartQuery(SmartQueryResponse r)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Query: \"{r.Query}\"  |  {r.ProcessingTimeMs:F0} ms  |  {r.TotalDetections} detection(s)");
        if (!string.IsNullOrWhiteSpace(r.VlAnalysis))
        {
            lines.AppendLine();
            lines.Append(r.VlAnalysis);
        }
        return lines.ToString().TrimEnd();
    }
}
