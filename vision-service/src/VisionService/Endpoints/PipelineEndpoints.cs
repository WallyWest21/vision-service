using VisionService.Clients;
using VisionService.Models;

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

        return app;
    }

    private static async Task<IResult> DetectAndDescribeAsync(
        IFormFile file,
        IYoloClient yolo,
        IQwenVlClient qwen,
        CancellationToken ct = default)
    {
        try
        {
            byte[] imageBytes;
            await using (var ms = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(ms, ct);
                imageBytes = ms.ToArray();
            }

            var detections = await yolo.DetectAsync(new MemoryStream(imageBytes), ct: ct);
            var captionTask = qwen.CaptionAsync(new MemoryStream(imageBytes), ct);
            var caption = await captionTask;

            return Results.Ok(new
            {
                Detections = detections,
                Caption = caption.Text,
                ObjectCount = detections.Count
            });
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> SafetyCheckAsync(
        IFormFile file,
        IYoloClient yolo,
        IQwenVlClient qwen,
        CancellationToken ct = default)
    {
        try
        {
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
        CancellationToken ct = default)
    {
        try
        {
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
        CancellationToken ct = default)
    {
        try
        {
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
}
