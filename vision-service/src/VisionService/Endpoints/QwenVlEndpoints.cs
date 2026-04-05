using Microsoft.AspNetCore.Mvc;
using VisionService.Clients;
using VisionService.Services;

namespace VisionService.Endpoints;

/// <summary>Qwen-VL vision-language endpoints.</summary>
public static class QwenVlEndpoints
{
    /// <summary>Maps all Qwen-VL endpoints under /api/v1.</summary>
    public static IEndpointRouteBuilder MapQwenVlEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("QwenVL");

        group.MapPost("/ask", AskAsync)
            .WithName("Ask")
            .WithSummary("Ask a question about an uploaded image")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/caption", CaptionAsync)
            .WithName("Caption")
            .WithSummary("Generate a descriptive caption for an uploaded image")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/ocr", OcrAsync)
            .WithName("Ocr")
            .WithSummary("Extract text from an uploaded image")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/analyze", AnalyzeAsync)
            .WithName("Analyze")
            .WithSummary("Analyze an uploaded image with a custom system prompt")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/compare", CompareAsync)
            .WithName("Compare")
            .WithSummary("Compare two uploaded images")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/describe/detailed", DescribeDetailedAsync)
            .WithName("DescribeDetailed")
            .WithSummary("Generate a long-form scene description for an uploaded image")
            .WithOpenApi()
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> AskAsync(
        IFormFile file,
        [FromForm] string question,
        IQwenVlClient qwen,
        IResponseCacheService cache,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(question))
                return Results.Problem("question is required", statusCode: 400);

            var imageBytes = await ReadBytesAsync(file, ct);
            var cacheKey = cache.ComputeKey(imageBytes, "ask", question);
            SetETagHeader(httpContext, cacheKey);

            var response = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream = new MemoryStream(imageBytes);
                return await qwen.AskAsync(stream, question, ct);
            });
            return Results.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Qwen-VL backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> CaptionAsync(
        IFormFile file,
        IQwenVlClient qwen,
        IResponseCacheService cache,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        try
        {
            var imageBytes = await ReadBytesAsync(file, ct);
            var cacheKey = cache.ComputeKey(imageBytes, "caption");
            SetETagHeader(httpContext, cacheKey);

            var response = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream = new MemoryStream(imageBytes);
                return await qwen.CaptionAsync(stream, ct);
            });
            return Results.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Qwen-VL backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> OcrAsync(
        IFormFile file,
        IQwenVlClient qwen,
        IResponseCacheService cache,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        try
        {
            var imageBytes = await ReadBytesAsync(file, ct);
            var cacheKey = cache.ComputeKey(imageBytes, "ocr");
            SetETagHeader(httpContext, cacheKey);

            var response = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream = new MemoryStream(imageBytes);
                return await qwen.OcrAsync(stream, ct);
            });
            return Results.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Qwen-VL backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> AnalyzeAsync(
        IFormFile file,
        [FromForm] string systemPrompt,
        IQwenVlClient qwen,
        IResponseCacheService cache,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(systemPrompt))
                return Results.Problem("systemPrompt is required", statusCode: 400);

            var imageBytes = await ReadBytesAsync(file, ct);
            var cacheKey = cache.ComputeKey(imageBytes, "analyze", systemPrompt);
            SetETagHeader(httpContext, cacheKey);

            var response = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream = new MemoryStream(imageBytes);
                return await qwen.AnalyzeAsync(stream, systemPrompt, ct);
            });
            return Results.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Qwen-VL backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> CompareAsync(
        IFormFile file1,
        IFormFile file2,
        IQwenVlClient qwen,
        IResponseCacheService cache,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        try
        {
            var bytes1 = await ReadBytesAsync(file1, ct);
            var bytes2 = await ReadBytesAsync(file2, ct);
            // Combine bytes of both images for a deterministic cache key
            var combined = new byte[bytes1.Length + bytes2.Length];
            Buffer.BlockCopy(bytes1, 0, combined, 0, bytes1.Length);
            Buffer.BlockCopy(bytes2, 0, combined, bytes1.Length, bytes2.Length);
            var cacheKey = cache.ComputeKey(combined, "compare");
            SetETagHeader(httpContext, cacheKey);

            var response = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream1 = new MemoryStream(bytes1);
                await using var stream2 = new MemoryStream(bytes2);
                return await qwen.CompareAsync(stream1, stream2, ct);
            });
            return Results.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Qwen-VL backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> DescribeDetailedAsync(
        IFormFile file,
        IQwenVlClient qwen,
        IResponseCacheService cache,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        try
        {
            var imageBytes = await ReadBytesAsync(file, ct);
            var cacheKey = cache.ComputeKey(imageBytes, "describe-detailed");
            SetETagHeader(httpContext, cacheKey);

            var response = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream = new MemoryStream(imageBytes);
                return await qwen.AnalyzeAsync(stream,
                    "You are a detailed scene analyst. Provide a comprehensive, structured description including objects, spatial relationships, colors, lighting, mood, and any text visible in the image.",
                    ct);
            });
            return Results.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Qwen-VL backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<byte[]> ReadBytesAsync(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static void SetETagHeader(HttpContext httpContext, string cacheKey)
    {
        httpContext.Response.Headers.ETag = $"\"{cacheKey}\"";
    }
}
