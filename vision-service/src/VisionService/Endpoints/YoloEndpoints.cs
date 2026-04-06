using Microsoft.AspNetCore.Mvc;
using VisionService.Clients;
using VisionService.Models;
using VisionService.Services;

namespace VisionService.Endpoints;

/// <summary>YOLO detection, segmentation, classification, and pose endpoints.</summary>
public static class YoloEndpoints
{
    /// <summary>Maps all YOLO endpoints under /api/v1.</summary>
    public static IEndpointRouteBuilder MapYoloEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("YOLO");

        group.MapPost("/detect", DetectAsync)
            .WithName("Detect")
            .WithSummary("Detect objects in an uploaded image")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/detect/batch", DetectBatchAsync)
            .WithName("DetectBatch")
            .WithSummary("Detect objects in multiple uploaded images")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/segment", SegmentAsync)
            .WithName("Segment")
            .WithSummary("Instance segmentation on an uploaded image")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/classify", ClassifyAsync)
            .WithName("Classify")
            .WithSummary("Classify the main subject of an uploaded image")
            .WithOpenApi()
            .DisableAntiforgery();

        group.MapPost("/pose", PoseAsync)
            .WithName("Pose")
            .WithSummary("Estimate human poses in an uploaded image")
            .WithOpenApi()
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> DetectAsync(
        IFormFile file,
        IYoloClient yolo,
        IImageService imageService,
        IResponseCacheService cache,
        HttpContext httpContext,
        [FromQuery] float confidence = 0.5f,
        CancellationToken ct = default)
    {
        try
        {
            ValidateConfidence(confidence);
            var imageBytes = await ReadBytesAsync(file, ct);
            var cacheKey = cache.ComputeKey(imageBytes, "detect", confidence.ToString("F2"));
            SetETagHeader(httpContext, cacheKey);

            var result = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream = new MemoryStream(imageBytes);
                var detections = await yolo.DetectAsync(stream, confidence, ct);
                return new DetectionResponse
                {
                    Detections = detections,
                    ProcessingTimeMs = 0,
                    Model = "YOLOv8"
                };
            });
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: 400);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("YOLO backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> DetectBatchAsync(
        IEnumerable<IFormFile> files,
        IYoloClient yolo,
        IResponseCacheService cache,
        [FromQuery] float confidence = 0.5f,
        CancellationToken ct = default)
    {
        try
        {
            ValidateConfidence(confidence);
            var tasks = files.Select(async f =>
            {
                var imageBytes = await ReadBytesAsync(f, ct);
                var cacheKey = cache.ComputeKey(imageBytes, "detect", confidence.ToString("F2"));
                return await cache.GetOrCreateAsync(cacheKey, async () =>
                {
                    await using var stream = new MemoryStream(imageBytes);
                    var detections = await yolo.DetectAsync(stream, confidence, ct);
                    return new { FileName = f.FileName, Detections = detections };
                });
            });
            var results = await Task.WhenAll(tasks);
            return Results.Ok(results);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: 400);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("YOLO backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> SegmentAsync(
        IFormFile file,
        IYoloClient yolo,
        IResponseCacheService cache,
        HttpContext httpContext,
        [FromQuery] float confidence = 0.5f,
        CancellationToken ct = default)
    {
        try
        {
            ValidateConfidence(confidence);
            var imageBytes = await ReadBytesAsync(file, ct);
            var cacheKey = cache.ComputeKey(imageBytes, "segment", confidence.ToString("F2"));
            SetETagHeader(httpContext, cacheKey);

            var result = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream = new MemoryStream(imageBytes);
                var segmentations = await yolo.SegmentAsync(stream, confidence, ct);
                return new SegmentationResponse
                {
                    Segments = segmentations,
                    Model = "YOLOv8-Seg"
                };
            });
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: 400);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("YOLO backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> ClassifyAsync(
        IFormFile file,
        IYoloClient yolo,
        IResponseCacheService cache,
        HttpContext httpContext,
        [FromQuery] int topN = 5,
        CancellationToken ct = default)
    {
        try
        {
            if (topN < 1 || topN > 100) return Results.Problem("topN must be between 1 and 100", statusCode: 400);
            var imageBytes = await ReadBytesAsync(file, ct);
            var cacheKey = cache.ComputeKey(imageBytes, "classify", topN.ToString());
            SetETagHeader(httpContext, cacheKey);

            var result = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream = new MemoryStream(imageBytes);
                var classifications = await yolo.ClassifyAsync(stream, topN, ct);
                return new ClassificationResponse
                {
                    Classifications = classifications,
                    Model = "YOLOv8-Cls"
                };
            });
            return Results.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("YOLO backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static async Task<IResult> PoseAsync(
        IFormFile file,
        IYoloClient yolo,
        IResponseCacheService cache,
        HttpContext httpContext,
        [FromQuery] float confidence = 0.5f,
        CancellationToken ct = default)
    {
        try
        {
            ValidateConfidence(confidence);
            var imageBytes = await ReadBytesAsync(file, ct);
            var cacheKey = cache.ComputeKey(imageBytes, "pose", confidence.ToString("F2"));
            SetETagHeader(httpContext, cacheKey);

            var result = await cache.GetOrCreateAsync(cacheKey, async () =>
            {
                await using var stream = new MemoryStream(imageBytes);
                var poses = await yolo.PoseAsync(stream, confidence, ct);
                return new PoseResponse
                {
                    Poses = poses,
                    Model = "YOLOv8-Pose"
                };
            });
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(ex.Message, statusCode: 400);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("YOLO backend unavailable: " + ex.Message, statusCode: 503);
        }
    }

    private static void ValidateConfidence(float confidence)
    {
        if (confidence < 0f || confidence > 1f)
            throw new ArgumentException("confidence must be between 0.0 and 1.0");
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

/// <summary>Response model for detection endpoints.</summary>
public class DetectionResponse
{
    /// <summary>List of detected objects.</summary>
    public IReadOnlyList<Detection> Detections { get; set; } = [];

    /// <summary>Processing time in milliseconds.</summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>Model used for detection.</summary>
    public string Model { get; set; } = string.Empty;
}

/// <summary>Response model for segmentation endpoint.</summary>
public class SegmentationResponse
{
    /// <summary>List of segmentation results.</summary>
    public IReadOnlyList<Segmentation> Segments { get; set; } = [];

    /// <summary>Model used for segmentation.</summary>
    public string Model { get; set; } = string.Empty;
}

/// <summary>Response model for classification endpoint.</summary>
public class ClassificationResponse
{
    /// <summary>List of classification results.</summary>
    public IReadOnlyList<ClassificationResult> Classifications { get; set; } = [];

    /// <summary>Model used for classification.</summary>
    public string Model { get; set; } = string.Empty;
}

/// <summary>Response model for pose endpoint.</summary>
public class PoseResponse
{
    /// <summary>List of pose estimation results.</summary>
    public IReadOnlyList<PoseResult> Poses { get; set; } = [];

    /// <summary>Model used for pose estimation.</summary>
    public string Model { get; set; } = string.Empty;
}
