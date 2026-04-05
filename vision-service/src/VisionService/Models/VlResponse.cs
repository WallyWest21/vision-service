namespace VisionService.Models;

/// <summary>Response from the Qwen-VL vision-language model.</summary>
public class VlResponse
{
    /// <summary>Generated text response.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Model used for inference.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Number of input tokens consumed.</summary>
    public int PromptTokens { get; set; }

    /// <summary>Number of output tokens generated.</summary>
    public int CompletionTokens { get; set; }

    /// <summary>Total tokens used.</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}
