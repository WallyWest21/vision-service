using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using VisionService.Configuration;
using VisionService.Models;

namespace VisionService.Clients;

/// <summary>HTTP client for the Qwen-VL vLLM OpenAI-compatible backend.</summary>
public class QwenVlClient : IQwenVlClient
{
    private readonly HttpClient _http;
    private readonly QwenVlOptions _options;
    private readonly ILogger<QwenVlClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Initializes a new instance of <see cref="QwenVlClient"/>.</summary>
    public QwenVlClient(HttpClient http, IOptions<QwenVlOptions> options, ILogger<QwenVlClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<VlResponse> AskAsync(Stream image, string question, CancellationToken ct = default)
    {
        var base64 = await ToBase64Async(image, ct);
        return await CallChatCompletionAsync(
            systemPrompt: "You are a helpful vision assistant.",
            userText: question,
            imageBase64: base64,
            ct: ct);
    }

    /// <inheritdoc/>
    public async Task<VlResponse> CaptionAsync(Stream image, CancellationToken ct = default)
    {
        var base64 = await ToBase64Async(image, ct);
        return await CallChatCompletionAsync(
            systemPrompt: "You are a vision assistant that describes images precisely and concisely.",
            userText: "Describe this image in detail.",
            imageBase64: base64,
            ct: ct);
    }

    /// <inheritdoc/>
    public async Task<VlResponse> OcrAsync(Stream image, CancellationToken ct = default)
    {
        var base64 = await ToBase64Async(image, ct);
        return await CallChatCompletionAsync(
            systemPrompt: "You are an OCR assistant. Extract all visible text from the image. Return only the extracted text.",
            userText: "Extract all text from this image.",
            imageBase64: base64,
            ct: ct);
    }

    /// <inheritdoc/>
    public async Task<VlResponse> AnalyzeAsync(Stream image, string systemPrompt, CancellationToken ct = default)
    {
        var base64 = await ToBase64Async(image, ct);
        return await CallChatCompletionAsync(
            systemPrompt: systemPrompt,
            userText: "Analyze this image according to the instructions.",
            imageBase64: base64,
            ct: ct);
    }

    /// <inheritdoc/>
    public async Task<VlResponse> CompareAsync(Stream image1, Stream image2, CancellationToken ct = default)
    {
        var base64_1 = await ToBase64Async(image1, ct);
        var base64_2 = await ToBase64Async(image2, ct);

        var request = new ChatCompletionRequest
        {
            Model = _options.ModelName,
            MaxTokens = _options.MaxTokens,
            Temperature = _options.Temperature,
            Messages =
            [
                new ChatMessage
                {
                    Role = "system",
                    Content = [new TextContent { Text = "You are a vision assistant that compares images." }]
                },
                new ChatMessage
                {
                    Role = "user",
                    Content =
                    [
                        new ImageContent { ImageUrl = new ImageUrl { Url = $"data:image/jpeg;base64,{base64_1}" } },
                        new ImageContent { ImageUrl = new ImageUrl { Url = $"data:image/jpeg;base64,{base64_2}" } },
                        new TextContent { Text = "Compare these two images and describe their key differences." }
                    ]
                }
            ]
        };

        return await SendRequestAsync(request, ct);
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

    private async Task<VlResponse> CallChatCompletionAsync(
        string systemPrompt, string userText, string imageBase64, CancellationToken ct)
    {
        var request = new ChatCompletionRequest
        {
            Model = _options.ModelName,
            MaxTokens = _options.MaxTokens,
            Temperature = _options.Temperature,
            Messages =
            [
                new ChatMessage
                {
                    Role = "system",
                    Content = [new TextContent { Text = systemPrompt }]
                },
                new ChatMessage
                {
                    Role = "user",
                    Content =
                    [
                        new ImageContent { ImageUrl = new ImageUrl { Url = $"data:image/jpeg;base64,{imageBase64}" } },
                        new TextContent { Text = userText }
                    ]
                }
            ]
        };

        return await SendRequestAsync(request, ct);
    }

    private async Task<VlResponse> SendRequestAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/v1/chat/completions", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, ct);
            if (result is null) throw new InvalidOperationException("Empty response from Qwen-VL");

            return new VlResponse
            {
                Text = result.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty,
                Model = result.Model,
                PromptTokens = result.Usage?.PromptTokens ?? 0,
                CompletionTokens = result.Usage?.CompletionTokens ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qwen-VL chat completion request failed");
            throw;
        }
    }

    private static async Task<string> ToBase64Async(Stream stream, CancellationToken ct)
    {
        if (!stream.CanSeek)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            return Convert.ToBase64String(ms.ToArray());
        }
        stream.Position = 0;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return Convert.ToBase64String(buffer.ToArray());
    }

    // OpenAI-compatible request/response DTOs
    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = [];
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; } = 1024;
        [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.7;
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public List<object> Content { get; set; } = [];
    }

    private sealed class TextContent
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "text";
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    private sealed class ImageContent
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "image_url";
        [JsonPropertyName("image_url")] public ImageUrl ImageUrl { get; set; } = new();
    }

    private sealed class ImageUrl
    {
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("choices")] public List<Choice> Choices { get; set; } = [];
        [JsonPropertyName("usage")] public UsageInfo? Usage { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public AssistantMessage? Message { get; set; }
    }

    private sealed class AssistantMessage
    {
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class UsageInfo
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
    }
}
