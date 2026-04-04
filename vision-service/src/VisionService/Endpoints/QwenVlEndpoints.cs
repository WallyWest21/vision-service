using Microsoft.AspNetCore.Mvc;
using VisionService.Clients;

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
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(question))
                return Results.Problem("question is required", statusCode: 400);

            await using var stream = file.OpenReadStream();
            var response = await qwen.AskAsync(stream, question, ct);
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
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var response = await qwen.CaptionAsync(stream, ct);
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
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var response = await qwen.OcrAsync(stream, ct);
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
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(systemPrompt))
                return Results.Problem("systemPrompt is required", statusCode: 400);

            await using var stream = file.OpenReadStream();
            var response = await qwen.AnalyzeAsync(stream, systemPrompt, ct);
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
        CancellationToken ct = default)
    {
        try
        {
            await using var stream1 = file1.OpenReadStream();
            await using var stream2 = file2.OpenReadStream();
            var response = await qwen.CompareAsync(stream1, stream2, ct);
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
        CancellationToken ct = default)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var response = await qwen.AnalyzeAsync(stream,
                "You are a detailed scene analyst. Provide a comprehensive, structured description including objects, spatial relationships, colors, lighting, mood, and any text visible in the image.",
                ct);
            return Results.Ok(response);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Qwen-VL backend unavailable: " + ex.Message, statusCode: 503);
        }
    }
}
