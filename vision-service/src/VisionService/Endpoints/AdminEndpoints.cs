using Microsoft.Extensions.Options;
using VisionService.Configuration;

namespace VisionService.Endpoints;

/// <summary>Admin endpoints for API key management.</summary>
public static class AdminEndpoints
{
    /// <summary>Maps admin API key management endpoints under /api/v1/admin.</summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin").WithTags("Admin");

        group.MapGet("/keys", ListKeysAsync)
            .WithName("ListApiKeys")
            .WithSummary("List all configured API keys (names and scopes only — key values are masked)")
            .WithOpenApi();

        group.MapPost("/keys", AddKeyAsync)
            .WithName("AddApiKey")
            .WithSummary("Generate and register a new API key")
            .WithOpenApi();

        return app;
    }

    private static IResult ListKeysAsync(IOptions<AuthOptions> auth)
    {
        var keys = auth.Value.ApiKeys.Select(k => new
        {
            k.Name,
            k.Scopes,
            k.RequestsPerMinute,
            KeyPreview = k.Key.Length > 4 ? $"...{k.Key[^4..]}" : "****"
        });
        return Results.Ok(keys);
    }

    private static IResult AddKeyAsync(NewApiKeyRequest request, IOptions<AuthOptions> auth)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.Problem("Name is required", statusCode: 400);

        var key = Guid.NewGuid().ToString("N");
        var entry = new ApiKeyEntry
        {
            Key = key,
            Name = request.Name,
            Scopes = request.Scopes ?? [],
            RequestsPerMinute = request.RequestsPerMinute
        };

        var existing = auth.Value.ApiKeys.ToList();
        existing.Add(entry);
        auth.Value.ApiKeys = [.. existing];

        return Results.Ok(new { key, entry.Name, entry.Scopes });
    }
}

/// <summary>Request body for creating a new API key.</summary>
public class NewApiKeyRequest
{
    /// <summary>Display name for the key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Scopes to assign: detect, analyze, admin, stream.</summary>
    public string[]? Scopes { get; set; }

    /// <summary>Per-minute rate limit override for this key (0 = use default).</summary>
    public int RequestsPerMinute { get; set; } = 0;
}
