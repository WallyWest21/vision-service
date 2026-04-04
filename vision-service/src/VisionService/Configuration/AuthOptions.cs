namespace VisionService.Configuration;

/// <summary>Configuration options for API authentication.</summary>
public class AuthOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auth";

    /// <summary>Whether API key authentication is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>List of valid API keys with their metadata.</summary>
    public ApiKeyEntry[] ApiKeys { get; set; } = [];
}

/// <summary>Represents a single API key entry with scopes.</summary>
public class ApiKeyEntry
{
    /// <summary>The API key value.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display name for this key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Allowed scopes: detect, analyze, admin, stream.</summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>Requests per minute limit for this key (0 = use default).</summary>
    public int RequestsPerMinute { get; set; } = 0;
}
