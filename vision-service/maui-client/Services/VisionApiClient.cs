using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MauiClient.Models;

namespace MauiClient.Services;

/// <summary>
/// Typed HTTP client that wraps every endpoint exposed by the VisionService.
/// The <see cref="BaseAddress"/> and <see cref="ApiKey"/> properties are
/// updated at runtime from the Settings page.
/// </summary>
public class VisionApiClient
{
    private readonly IHttpClientFactory _factory;
    private static readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web);

    /// <summary>Base URL of the running VisionService, e.g. http://100.108.155.28:5100.</summary>
    public string BaseAddress { get; set; } = "http://100.108.155.28:5100";

    /// <summary>API key sent in the X-Api-Key header (leave empty to skip).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public VisionApiClient(IHttpClientFactory factory) => _factory = factory;

    // ── helpers ─────────────────────────────────────────────────────────────

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.BaseAddress = new Uri(BaseAddress.TrimEnd('/') + '/');
        client.Timeout = TimeSpan.FromSeconds(120);
        if (!string.IsNullOrWhiteSpace(ApiKey))
            client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }

    private static MultipartFormDataContent BuildImageForm(byte[] imageBytes, string fileName = "image.jpg")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(imageBytes), "file", fileName);
        return form;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = response.ReasonPhrase ?? response.StatusCode.ToString();
            try
            {
                var body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String)
                        detail = d.GetString() ?? detail;
                    else if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                        detail = t.GetString() ?? detail;
                    else if (doc.RootElement.TryGetProperty("Error", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.String)
                        detail = e.GetString() ?? detail;
                }
            }
            catch { /* keep status-line detail */ }
            throw new HttpRequestException(detail, null, response.StatusCode);
        }
        var result = await response.Content.ReadFromJsonAsync<T>(_json)
            ?? throw new InvalidOperationException("Empty response body");
        return result;
    }

    // ── Health ───────────────────────────────────────────────────────────────

    /// <summary>GET /health — liveness check.</summary>
    public async Task<string> HealthAsync(CancellationToken ct = default)
    {
        using var client = CreateClient();
        var response = await client.GetAsync("health", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    // ── YOLO ─────────────────────────────────────────────────────────────────

    /// <summary>POST /api/v1/detect — object detection.</summary>
    public async Task<DetectionResponse> DetectAsync(byte[] imageBytes, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync($"api/v1/detect?confidence={confidence:F2}", form, ct);
        return await ReadAsync<DetectionResponse>(response);
    }

    /// <summary>POST /api/v1/detect/batch — batch object detection.</summary>
    public async Task<List<DetectionResponse>> DetectBatchAsync(IEnumerable<byte[]> images, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var client = CreateClient();
        var form = new MultipartFormDataContent();
        int idx = 0;
        foreach (var img in images)
            form.Add(new ByteArrayContent(img), "files", $"image{idx++}.jpg");
        var response = await client.PostAsync($"api/v1/detect/batch?confidence={confidence:F2}", form, ct);
        return await ReadAsync<List<DetectionResponse>>(response);
    }

    /// <summary>POST /api/v1/segment — instance segmentation.</summary>
    public async Task<SegmentationResponse> SegmentAsync(byte[] imageBytes, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync($"api/v1/segment?confidence={confidence:F2}", form, ct);
        return await ReadAsync<SegmentationResponse>(response);
    }

    /// <summary>POST /api/v1/classify — image classification.</summary>
    public async Task<ClassificationResponse> ClassifyAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync("api/v1/classify", form, ct);
        return await ReadAsync<ClassificationResponse>(response);
    }

    /// <summary>POST /api/v1/pose — human pose estimation.</summary>
    public async Task<PoseResponse> PoseAsync(byte[] imageBytes, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync($"api/v1/pose?confidence={confidence:F2}", form, ct);
        return await ReadAsync<PoseResponse>(response);
    }

    // ── Qwen-VL ──────────────────────────────────────────────────────────────

    /// <summary>POST /api/v1/ask — ask a question about an image.</summary>
    public async Task<VlResponse> AskAsync(byte[] imageBytes, string question, CancellationToken ct = default)
    {
        using var client = CreateClient();
        var form = BuildImageForm(imageBytes);
        form.Add(new StringContent(question, Encoding.UTF8), "question");
        var response = await client.PostAsync("api/v1/ask", form, ct);
        return await ReadAsync<VlResponse>(response);
    }

    /// <summary>POST /api/v1/caption — generate an image caption.</summary>
    public async Task<VlResponse> CaptionAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync("api/v1/caption", form, ct);
        return await ReadAsync<VlResponse>(response);
    }

    /// <summary>POST /api/v1/ocr — extract text from an image.</summary>
    public async Task<VlResponse> OcrAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync("api/v1/ocr", form, ct);
        return await ReadAsync<VlResponse>(response);
    }

    /// <summary>POST /api/v1/analyze — custom system-prompt analysis.</summary>
    public async Task<VlResponse> AnalyzeAsync(byte[] imageBytes, string systemPrompt, CancellationToken ct = default)
    {
        using var client = CreateClient();
        var form = BuildImageForm(imageBytes);
        form.Add(new StringContent(systemPrompt, Encoding.UTF8), "systemPrompt");
        var response = await client.PostAsync("api/v1/analyze", form, ct);
        return await ReadAsync<VlResponse>(response);
    }

    /// <summary>POST /api/v1/compare — compare two images.</summary>
    public async Task<VlResponse> CompareAsync(byte[] imageA, byte[] imageB, CancellationToken ct = default)
    {
        using var client = CreateClient();
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(imageA), "file", "imageA.jpg");
        form.Add(new ByteArrayContent(imageB), "file2", "imageB.jpg");
        var response = await client.PostAsync("api/v1/compare", form, ct);
        return await ReadAsync<VlResponse>(response);
    }

    /// <summary>POST /api/v1/describe/detailed — long-form scene description.</summary>
    public async Task<VlResponse> DescribeDetailedAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync("api/v1/describe/detailed", form, ct);
        return await ReadAsync<VlResponse>(response);
    }

    // ── Pipeline ─────────────────────────────────────────────────────────────

    /// <summary>POST /api/v1/pipeline/detect-and-describe.</summary>
    public async Task<JsonElement> DetectAndDescribeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync("api/v1/pipeline/detect-and-describe", form, ct);
        return await ReadAsync<JsonElement>(response);
    }

    /// <summary>POST /api/v1/pipeline/safety-check.</summary>
    public async Task<JsonElement> SafetyCheckAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync("api/v1/pipeline/safety-check", form, ct);
        return await ReadAsync<JsonElement>(response);
    }

    /// <summary>POST /api/v1/pipeline/inventory.</summary>
    public async Task<JsonElement> InventoryAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync("api/v1/pipeline/inventory", form, ct);
        return await ReadAsync<JsonElement>(response);
    }

    /// <summary>POST /api/v1/pipeline/scene.</summary>
    public async Task<JsonElement> SceneAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var form = BuildImageForm(imageBytes);
        var response = await client.PostAsync("api/v1/pipeline/scene", form, ct);
        return await ReadAsync<JsonElement>(response);
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/keys — list API keys (masked).</summary>
    public async Task<List<ApiKeyPreview>> ListKeysAsync(CancellationToken ct = default)
    {
        using var client = CreateClient();
        var response = await client.GetAsync("api/v1/admin/keys", ct);
        return await ReadAsync<List<ApiKeyPreview>>(response);
    }

    /// <summary>POST /api/v1/admin/keys — create a new API key.</summary>
    public async Task<NewApiKeyResponse> AddKeyAsync(NewApiKeyRequest request, CancellationToken ct = default)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/v1/admin/keys", request, _json, ct);
        return await ReadAsync<NewApiKeyResponse>(response);
    }
}
