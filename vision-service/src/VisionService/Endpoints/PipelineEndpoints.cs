using System.Diagnostics;
using VisionService.Clients;
using VisionService.Diagnostics;
using VisionService.Models;
using VisionService.Services;

namespace VisionService.Endpoints;

/// <summary>Combined pipeline endpoints that orchestrate YOLO + Qwen-VL.</summary>
public static class PipelineEndpoints
{
    /// <summary>Maps all pipeline endpoints under /api/v1/pipeline.</summary>
    public static IEndpointRouteBuilder MapPipelineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/pipeline").WithTags("Pipeline");

        group.MapPost("/detect-and-describe", DetectAndDescribeAsync)
            .WithName("DetectAndDescribe")
            .WithSummary("YOLO detect objects, then Qwen-VL describe each detection")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/safety-check", SafetyCheckAsync)
            .WithName("SafetyCheck")
            .WithSummary("YOLO detect + Qwen-VL safety analysis")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/inventory", InventoryAsync)
            .WithName("Inventory")
            .WithSummary("YOLO detect + Qwen-VL classify/count items")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/scene", SceneAsync)
            .WithName("Scene")
            .WithSummary("Full scene analysis: detections + caption + OCR combined")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/smart-query", SmartQueryAsync)
            .WithName("SmartQuery")
            .WithSummary("YOLO detect + Qwen-VL identify queried objects with spatial markers")
            .WithOpenApi()
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> DetectAndDescribeAsync(
        IFormFile file,
        IYoloClient yolo,
        IQwenVlClient qwen,
        IFileValidationService fileValidator,
        CancellationToken ct = default)
    {
        using var activity = VisionActivitySource.Source.StartActivity("Pipeline.DetectAndDescribe");
        try
        {
            var validation = await fileValidator.ValidateAsync(file, ct);
            if (!validation.IsValid)
                return Results.Problem(validation.ErrorMessage, statusCode: 400);

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            var detections = await yolo.DetectAsync(new MemoryStream(imageBytes), ct: ct);
            var captionTask = qwen.CaptionAsync(new MemoryStream(imageBytes), ct);
            var caption = await captionTask;

            activity?.SetTag("pipeline.detection_count", detections.Count);

            return Results.Ok(new
            {
                Detections = detections,
                Caption = caption.Text,
                ObjectCount = detections.Count
            });
        }
        catch (HttpRequestException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem("Backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> SafetyCheckAsync(
        IFormFile file,
        IYoloClient yolo,
        IQwenVlClient qwen,
        IFileValidationService fileValidator,
        CancellationToken ct = default)
    {
        try
        {
            var validation = await fileValidator.ValidateAsync(file, ct);
            if (!validation.IsValid)
                return Results.Problem(validation.ErrorMessage, statusCode: 400);

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            var detectTask = yolo.DetectAsync(new MemoryStream(imageBytes), ct: ct);
            var safetyTask = qwen.AnalyzeAsync(new MemoryStream(imageBytes),
                "You are a safety analysis system. Determine if this image contains any unsafe, dangerous, or inappropriate content. Respond with: SAFE or UNSAFE followed by a brief explanation.",
                ct);

            await Task.WhenAll(detectTask, safetyTask);

            var detections = await detectTask;
            var safetyResponse = await safetyTask;
            var isSafe = safetyResponse.Text.StartsWith("SAFE", StringComparison.OrdinalIgnoreCase);

            return Results.Ok(new
            {
                IsSafe = isSafe,
                SafetyAnalysis = safetyResponse.Text,
                Detections = detections
            });
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> InventoryAsync(
        IFormFile file,
        IYoloClient yolo,
        IQwenVlClient qwen,
        IFileValidationService fileValidator,
        CancellationToken ct = default)
    {
        try
        {
            var validation = await fileValidator.ValidateAsync(file, ct);
            if (!validation.IsValid)
                return Results.Problem(validation.ErrorMessage, statusCode: 400);

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            var detectTask = yolo.DetectAsync(new MemoryStream(imageBytes), ct: ct);
            var inventoryTask = qwen.AnalyzeAsync(new MemoryStream(imageBytes),
                "You are an inventory management assistant. List all distinct items visible in this image with their quantities. Format: item name: count",
                ct);

            await Task.WhenAll(detectTask, inventoryTask);

            var detections = await detectTask;
            var inventoryResponse = await inventoryTask;

            var itemCounts = detections
                .GroupBy(d => d.Label)
                .Select(g => new { Item = g.Key, Count = g.Count() })
                .ToList();

            return Results.Ok(new
            {
                ItemCounts = itemCounts,
                VlInventory = inventoryResponse.Text,
                TotalDetections = detections.Count
            });
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> SceneAsync(
        IFormFile file,
        IYoloClient yolo,
        IQwenVlClient qwen,
        IFileValidationService fileValidator,
        CancellationToken ct = default)
    {
        try
        {
            var validation = await fileValidator.ValidateAsync(file, ct);
            if (!validation.IsValid)
                return Results.Problem(validation.ErrorMessage, statusCode: 400);

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            var detectTask = yolo.DetectAsync(new MemoryStream(imageBytes), ct: ct);
            var captionTask = qwen.CaptionAsync(new MemoryStream(imageBytes), ct);
            var ocrTask = qwen.OcrAsync(new MemoryStream(imageBytes), ct);

            await Task.WhenAll(detectTask, captionTask, ocrTask);

            var detections = await detectTask;
            return Results.Ok(new
            {
                Detections = detections,
                Caption = (await captionTask).Text,
                ExtractedText = (await ocrTask).Text,
                DetectionCount = detections.Count
            });
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private const string SmartQueryDefaultSystemPrompt =
        "You are a real-time vision analysis assistant monitoring a live camera feed. " +
        "The user will specify one or more objects or conditions to watch for. " +
        "Your task:\n" +
        "1. State clearly whether each queried object/condition is PRESENT or ABSENT in the frame.\n" +
        "2. For each present object, describe its position using spatial terms " +
        "(top-left, top-center, top-right, center-left, center, center-right, bottom-left, bottom-center, bottom-right) " +
        "and estimate how much of the frame it occupies (small / medium / large).\n" +
        "3. Report any additional relevant context (e.g., partial occlusion, number of instances, notable attributes).\n" +
        "4. Keep responses concise and structured — one bullet per object. " +
        "Do not describe unrelated scene elements unless directly relevant to the query.";

    private static async Task<IResult> SmartQueryAsync(
        IFormFile file,
        [Microsoft.AspNetCore.Mvc.FromForm] string query,
        [Microsoft.AspNetCore.Mvc.FromForm] string? systemPrompt,
        IYoloClient yolo,
        IQwenVlClient qwen,
        IFileValidationService fileValidator,
        CancellationToken ct = default)
    {
        using var activity = VisionActivitySource.Source.StartActivity("Pipeline.SmartQuery");
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return Results.Problem("query is required", statusCode: 400);

            var validation = await fileValidator.ValidateAsync(file, ct);
            if (!validation.IsValid)
                return Results.Problem(validation.ErrorMessage, statusCode: 400);

            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            var effectiveSystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? SmartQueryDefaultSystemPrompt
                : systemPrompt;

            var userMessage = $"Query: {query}";

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var detectTask = yolo.DetectAsync(new MemoryStream(imageBytes), ct: ct);
            var vlTask = qwen.AskWithSystemPromptAsync(new MemoryStream(imageBytes), effectiveSystemPrompt, userMessage, ct);

            await Task.WhenAll(detectTask, vlTask);
            sw.Stop();

            var detections = await detectTask;
            var vlResponse = await vlTask;

            activity?.SetTag("pipeline.smart_query", query);
            activity?.SetTag("pipeline.detection_count", detections.Count);

            return Results.Ok(new SmartQueryResponse
            {
                Query = query,
                Detections = [.. detections],
                VlAnalysis = vlResponse.Text,
                TotalDetections = detections.Count,
                ProcessingTimeMs = sw.Elapsed.TotalMilliseconds,
                YoloModel = "yolo"
            });
        }
        catch (HttpRequestException ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            return Results.Problem("Backend unavailable: " + ex.Message, statusCode: 503);
        }
    }
}
