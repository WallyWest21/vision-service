using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using VisionService.Configuration;
using VisionService.Diagnostics;
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
        using var activity = VisionActivitySource.Source.StartActivity("QwenVlClient.Caption");
        var base64 = await ToBase64Async(image, ct);
        try
        {
            return await CallChatCompletionAsync(
                systemPrompt: "You are a vision assistant that describes images precisely and concisely.",
                userText: "Describe this image in detail.",
                imageBase64: base64,
                ct: ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Qwen-VL caption request failed");
            throw;
        }
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
    public async Task<VlResponse> AskWithSystemPromptAsync(Stream image, string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var base64 = await ToBase64Async(image, ct);
        return await CallChatCompletionAsync(
            systemPrompt: systemPrompt,
            userText: userMessage,
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

    private const string ExtractInventorySystemPrompt =
        "You are an inventory-cataloguing vision assistant for a maker/workshop app. " +
        "From the single image, identify the ONE primary item and return ONLY a JSON object — no prose, no markdown fences.\n" +
        "Rules:\n" +
        "1. Read any visible brand, model, or label text first (OCR) and use it to set `name`; put the raw text in `visibleText`. " +
        "If no text is legible, give a generic but specific name (e.g. \"Cordless drill\").\n" +
        "2. `category` MUST be exactly one of the allowed values provided by the user.\n" +
        "3. `capabilities` and `limitations` are what this product can / cannot do, inferred from what you recognise — " +
        "2–4 short bullet strings each. Do not invent specs you cannot reasonably attribute to the item.\n" +
        "4. `quantity` = number of identical instances visible (1 if single or unsure).\n" +
        "5. `confidence` = your 0.0–1.0 certainty in the identification. Be honest; lower it when the item is ambiguous or unlabeled.\n" +
        "Return keys exactly: name, category, subtitle, capabilities, limitations, quantity, visibleText, confidence.";

    /// <inheritdoc/>
    public async Task<InventoryItemExtraction> ExtractInventoryItemAsync(
        Stream image, IReadOnlyCollection<string> categories, CancellationToken ct = default)
    {
        using var activity = VisionActivitySource.Source.StartActivity("QwenVlClient.ExtractInventoryItem");
        var allowed = (categories is { Count: > 0 } ? categories : DefaultCategories).ToArray();
        var base64 = await ToBase64Async(image, ct);

        var schema = new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["name"] = new { type = "string" },
                ["category"] = new { type = "string", @enum = allowed },
                ["subtitle"] = new { type = "string" },
                ["capabilities"] = new { type = "array", items = new { type = "string" } },
                ["limitations"] = new { type = "array", items = new { type = "string" } },
                ["quantity"] = new { type = "integer", minimum = 0 },
                ["visibleText"] = new { type = "string" },
                ["confidence"] = new { type = "number", minimum = 0, maximum = 1 },
            },
            required = new[] { "name", "category", "capabilities", "limitations", "quantity", "confidence" },
        };

        var request = new ChatCompletionRequest
        {
            Model = _options.ModelName,
            MaxTokens = _options.MaxTokens,
            Temperature = 0.1, // low: this is extraction, not creative generation
            ResponseFormat = new { type = "json_object" },
            GuidedJson = schema, // vLLM OpenAI server: constrains output to the schema
            Messages =
            [
                new ChatMessage { Role = "system", Content = [new TextContent { Text = ExtractInventorySystemPrompt }] },
                new ChatMessage
                {
                    Role = "user",
                    Content =
                    [
                        new ImageContent { ImageUrl = new ImageUrl { Url = $"data:image/jpeg;base64,{base64}" } },
                        new TextContent { Text = "Allowed categories: " + string.Join(", ", allowed) + ". Catalogue the primary item." }
                    ]
                }
            ]
        };

        VlResponse raw;
        try
        {
            raw = await SendRequestAsync(request, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Qwen-VL inventory extraction request failed");
            throw;
        }

        return ParseExtraction(raw.Text, allowed);
    }

    /// <summary>Parses model output into <see cref="InventoryItemExtraction"/>, tolerating fenced/partial JSON.</summary>
    internal static InventoryItemExtraction ParseExtraction(string content, IReadOnlyCollection<string> allowed)
    {
        var json = ExtractJsonObject(content);
        if (json is not null)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<InventoryItemExtraction>(json, JsonOptions);
                if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Name))
                {
                    // Defend against the model ignoring the enum constraint.
                    if (!allowed.Contains(parsed.Category, StringComparer.OrdinalIgnoreCase))
                        parsed.Category = string.Empty;
                    if (parsed.Quantity < 1)
                        parsed.Quantity = 1;
                    parsed.RawResponse = content;
                    // Always confirm; force review when the model is unsure or category got dropped.
                    parsed.NeedsReview = parsed.Confidence < 0.75 || string.IsNullOrEmpty(parsed.Category);
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // fall through to the review-fallback below
            }
        }

        return new InventoryItemExtraction
        {
            Name = string.Empty,
            Confidence = 0,
            NeedsReview = true,
            RawResponse = content,
        };
    }

    private static string? ExtractJsonObject(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        return start >= 0 && end > start ? content[start..(end + 1)] : null;
    }

    private static readonly string[] DefaultCategories =
        ["Tools", "Machines", "Materials", "Consumables", "Software", "Workspace", "Transport", "Skills"];

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
            if (result is null)
                throw new InvalidOperationException("Empty response from Qwen-VL");

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

        /// <summary>OpenAI-style response format hint, e.g. <c>{ "type": "json_object" }</c>. Omitted when null.</summary>
        [JsonPropertyName("response_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? ResponseFormat
        {
            get; set;
        }

        /// <summary>vLLM guided-decoding JSON schema. Constrains output to a shape. Omitted when null.</summary>
        [JsonPropertyName("guided_json")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? GuidedJson
        {
            get; set;
        }
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
        [JsonPropertyName("usage")]
        public UsageInfo? Usage
        {
            get; set;
        }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public AssistantMessage? Message
        {
            get; set;
        }
    }

    private sealed class AssistantMessage
    {
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class UsageInfo
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens
        {
            get; set;
        }
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens
        {
            get; set;
        }
    }
}
