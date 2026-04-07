using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VisionService.Configuration;
using VisionService.Diagnostics;
using VisionService.Models;

namespace VisionService.Clients;

/// <summary>HTTP client for the YOLO FastAPI AI backend.</summary>
public class YoloClient : IYoloClient
{
    private readonly HttpClient _http;
    private readonly YoloOptions _options;
    private readonly ILogger<YoloClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Initializes a new instance of <see cref="YoloClient"/>.</summary>
    public YoloClient(HttpClient http, IOptions<YoloOptions> options, ILogger<YoloClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Detection>> DetectAsync(Stream image, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var activity = VisionActivitySource.Source.StartActivity("YoloClient.Detect");
        activity?.SetTag("yolo.confidence", confidence);
        using var content = BuildMultipartContent(image, confidence);
        try
        {
            var response = await _http.PostAsync("/detect", content, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<YoloDetectResponse>(JsonOptions, ct);
            var detections = result?.Detections ?? [];
            activity?.SetTag("yolo.detection_count", detections.Count);
            return detections;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "YOLO detect request failed");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Segmentation>> SegmentAsync(Stream image, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var content = BuildMultipartContent(image, confidence);
        try
        {
            var response = await _http.PostAsync("/segment", content, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<YoloSegmentResponse>(JsonOptions, ct);
            return result?.Segmentations ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YOLO segment request failed");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClassificationResult>> ClassifyAsync(Stream image, int topN = 5, CancellationToken ct = default)
    {
        using var content = BuildMultipartContent(image, 0f);
        content.Add(new StringContent(topN.ToString()), "top_n");
        try
        {
            var response = await _http.PostAsync("/classify", content, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<YoloClassifyResponse>(JsonOptions, ct);
            return result?.Classifications ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YOLO classify request failed");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PoseResult>> PoseAsync(Stream image, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var content = BuildMultipartContent(image, confidence);
        try
        {
            var response = await _http.PostAsync("/pose", content, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<YoloPoseResponse>(JsonOptions, ct);
            return result?.Poses ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YOLO pose request failed");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static MultipartFormDataContent BuildMultipartContent(Stream image, float confidence)
    {
        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(image);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(streamContent, "file", "image.jpg");
        if (confidence > 0f)
            content.Add(new StringContent(confidence.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)), "confidence");
        return content;
    }

    // Internal response DTOs
    private sealed class YoloDetectResponse { public List<Detection> Detections { get; set; } = []; }
    private sealed class YoloSegmentResponse { public List<Segmentation> Segmentations { get; set; } = []; }
    private sealed class YoloClassifyResponse { public List<ClassificationResult> Classifications { get; set; } = []; }
    private sealed class YoloPoseResponse { public List<PoseResult> Poses { get; set; } = []; }
}
