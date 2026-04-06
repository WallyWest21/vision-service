using VisionServiceClient.Models;

namespace VisionServiceClient.Services;

/// <summary>Provides access to all vision microservice endpoints.</summary>
public interface IVisionApiService
{
    /// <summary>Configures the base URL and API key for all requests.</summary>
    void Configure(string baseUrl, string apiKey);

    /// <summary>Checks service health.</summary>
    Task<HealthResponse> GetHealthAsync(CancellationToken ct = default);

    /// <summary>Runs object detection on a single image.</summary>
    Task<DetectResponse> DetectAsync(Stream image, string fileName, float confidence = 0.5f, CancellationToken ct = default);

    /// <summary>Runs object detection on multiple images in a single request.</summary>
    Task<BatchDetectItem[]> DetectBatchAsync(IEnumerable<(Stream image, string fileName)> files, float confidence = 0.5f, CancellationToken ct = default);

    /// <summary>Runs instance segmentation on an image.</summary>
    Task<SegmentResponse> SegmentAsync(Stream image, string fileName, float confidence = 0.5f, CancellationToken ct = default);

    /// <summary>Runs image classification on an image.</summary>
    Task<ClassifyResponse> ClassifyAsync(Stream image, string fileName, int topN = 5, CancellationToken ct = default);

    /// <summary>Runs pose estimation on an image.</summary>
    Task<PoseResponse> PoseAsync(Stream image, string fileName, float confidence = 0.5f, CancellationToken ct = default);

    /// <summary>Asks a natural-language question about an image.</summary>
    Task<VlResponse> AskAsync(Stream image, string fileName, string question, CancellationToken ct = default);

    /// <summary>Generates a caption for an image.</summary>
    Task<VlResponse> CaptionAsync(Stream image, string fileName, CancellationToken ct = default);

    /// <summary>Extracts text from an image via OCR.</summary>
    Task<VlResponse> OcrAsync(Stream image, string fileName, CancellationToken ct = default);

    /// <summary>Analyzes an image with a custom system prompt.</summary>
    Task<VlResponse> AnalyzeAsync(Stream image, string fileName, string systemPrompt, CancellationToken ct = default);

    /// <summary>Compares two images using the vision-language model.</summary>
    Task<VlResponse> CompareAsync(Stream image1, string fileName1, Stream image2, string fileName2, CancellationToken ct = default);

    /// <summary>Generates a detailed description of an image.</summary>
    Task<VlResponse> DescribeDetailedAsync(Stream image, string fileName, CancellationToken ct = default);

    /// <summary>Runs the detect-and-describe pipeline.</summary>
    Task<DetectAndDescribeResponse> DetectAndDescribeAsync(Stream image, string fileName, CancellationToken ct = default);

    /// <summary>Runs the safety-check pipeline.</summary>
    Task<SafetyCheckResponse> SafetyCheckAsync(Stream image, string fileName, CancellationToken ct = default);

    /// <summary>Runs the inventory pipeline.</summary>
    Task<InventoryResponse> InventoryAsync(Stream image, string fileName, CancellationToken ct = default);

    /// <summary>Runs the scene analysis pipeline.</summary>
    Task<SceneResponse> SceneAsync(Stream image, string fileName, CancellationToken ct = default);

    /// <summary>Lists all configured API keys.</summary>
    Task<ApiKeyInfo[]> GetApiKeysAsync(CancellationToken ct = default);

    /// <summary>Generates a new API key.</summary>
    Task<GenerateKeyResponse> GenerateApiKeyAsync(GenerateKeyRequest request, CancellationToken ct = default);
}
