using System.Net.Http.Json;
using System.Text.Json;
using VisionServiceClient.Models;

namespace VisionServiceClient.Services;

/// <summary>HTTP client implementation for the vision microservice.</summary>
public class VisionApiService : IVisionApiService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>Initializes the service with a default HttpClient.</summary>
    public VisionApiService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    /// <inheritdoc />
    public void Configure(string baseUrl, string apiKey)
    {
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Remove("X-Api-Key");
        if (!string.IsNullOrWhiteSpace(apiKey))
            _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    /// <inheritdoc />
    public async Task<HealthResponse> GetHealthAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<HealthResponse>("health", _json, ct)
           ?? throw new InvalidOperationException("No response from health endpoint.");

    /// <inheritdoc />
    public async Task<DetectResponse> DetectAsync(Stream image, string fileName, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync($"api/v1/detect?confidence={confidence}", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<DetectResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from detect endpoint.");
    }

    /// <inheritdoc />
    public async Task<BatchDetectItem[]> DetectBatchAsync(IEnumerable<(Stream image, string fileName)> files, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        foreach (var (stream, name) in files)
            form.Add(new StreamContent(stream), "files", name);
        var resp = await _http.PostAsync($"api/v1/detect/batch?confidence={confidence}", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<BatchDetectItem[]>(_json, ct) ?? [];
    }

    /// <inheritdoc />
    public async Task<SegmentResponse> SegmentAsync(Stream image, string fileName, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync($"api/v1/segment?confidence={confidence}", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SegmentResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from segment endpoint.");
    }

    /// <inheritdoc />
    public async Task<ClassifyResponse> ClassifyAsync(Stream image, string fileName, int topN = 5, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync($"api/v1/classify?topN={topN}", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ClassifyResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from classify endpoint.");
    }

    /// <inheritdoc />
    public async Task<PoseResponse> PoseAsync(Stream image, string fileName, float confidence = 0.5f, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync($"api/v1/pose?confidence={confidence}", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PoseResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from pose endpoint.");
    }

    /// <inheritdoc />
    public async Task<VlResponse> AskAsync(Stream image, string fileName, string question, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        form.Add(new StringContent(question), "question");
        var resp = await _http.PostAsync("api/v1/ask", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<VlResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from ask endpoint.");
    }

    /// <inheritdoc />
    public async Task<VlResponse> CaptionAsync(Stream image, string fileName, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync("api/v1/caption", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<VlResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from caption endpoint.");
    }

    /// <inheritdoc />
    public async Task<VlResponse> OcrAsync(Stream image, string fileName, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync("api/v1/ocr", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<VlResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from ocr endpoint.");
    }

    /// <inheritdoc />
    public async Task<VlResponse> AnalyzeAsync(Stream image, string fileName, string systemPrompt, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        form.Add(new StringContent(systemPrompt), "systemPrompt");
        var resp = await _http.PostAsync("api/v1/analyze", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<VlResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from analyze endpoint.");
    }

    /// <inheritdoc />
    public async Task<VlResponse> CompareAsync(Stream image1, string fileName1, Stream image2, string fileName2, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(image1), "file1", fileName1);
        form.Add(new StreamContent(image2), "file2", fileName2);
        var resp = await _http.PostAsync("api/v1/compare", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<VlResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from compare endpoint.");
    }

    /// <inheritdoc />
    public async Task<VlResponse> DescribeDetailedAsync(Stream image, string fileName, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync("api/v1/describe/detailed", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<VlResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from describe endpoint.");
    }

    /// <inheritdoc />
    public async Task<DetectAndDescribeResponse> DetectAndDescribeAsync(Stream image, string fileName, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync("api/v1/pipeline/detect-and-describe", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<DetectAndDescribeResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from detect-and-describe endpoint.");
    }

    /// <inheritdoc />
    public async Task<SafetyCheckResponse> SafetyCheckAsync(Stream image, string fileName, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync("api/v1/pipeline/safety-check", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SafetyCheckResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from safety-check endpoint.");
    }

    /// <inheritdoc />
    public async Task<InventoryResponse> InventoryAsync(Stream image, string fileName, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync("api/v1/pipeline/inventory", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<InventoryResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from inventory endpoint.");
    }

    /// <inheritdoc />
    public async Task<SceneResponse> SceneAsync(Stream image, string fileName, CancellationToken ct = default)
    {
        using var form = CreateImageForm(image, fileName);
        var resp = await _http.PostAsync("api/v1/pipeline/scene", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SceneResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from scene endpoint.");
    }

    /// <inheritdoc />
    public async Task<ApiKeyInfo[]> GetApiKeysAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<ApiKeyInfo[]>("api/v1/admin/keys", _json, ct) ?? [];

    /// <inheritdoc />
    public async Task<GenerateKeyResponse> GenerateApiKeyAsync(GenerateKeyRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/v1/admin/keys", request, _json, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<GenerateKeyResponse>(_json, ct)
               ?? throw new InvalidOperationException("No response from generate key endpoint.");
    }

    private static MultipartFormDataContent CreateImageForm(Stream image, string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StreamContent(image), "file", fileName);
        return form;
    }
}
