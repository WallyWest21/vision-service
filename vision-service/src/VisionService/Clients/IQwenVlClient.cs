using VisionService.Models;

namespace VisionService.Clients;

/// <summary>Client for the Qwen-VL vLLM vision-language backend.</summary>
public interface IQwenVlClient
{
    /// <summary>Asks a question about an image.</summary>
    Task<VlResponse> AskAsync(Stream image, string question, CancellationToken ct = default);

    /// <summary>Generates a descriptive caption for an image.</summary>
    Task<VlResponse> CaptionAsync(Stream image, CancellationToken ct = default);

    /// <summary>Extracts text from an image via OCR.</summary>
    Task<VlResponse> OcrAsync(Stream image, CancellationToken ct = default);

    /// <summary>Analyses an image with a custom system prompt.</summary>
    Task<VlResponse> AnalyzeAsync(Stream image, string systemPrompt, CancellationToken ct = default);

    /// <summary>Compares two images and describes their differences.</summary>
    Task<VlResponse> CompareAsync(Stream image1, Stream image2, CancellationToken ct = default);

    /// <summary>Checks whether the Qwen-VL backend is healthy.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
