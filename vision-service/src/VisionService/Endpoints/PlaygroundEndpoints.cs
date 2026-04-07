using VisionService.Clients;
using VisionService.Services;

namespace VisionService.Endpoints;

/// <summary>Interactive playground endpoint for browser-based image testing.</summary>
public static class PlaygroundEndpoints
{
    /// <summary>Maps the playground endpoint.</summary>
    public static IEndpointRouteBuilder MapPlaygroundEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/playground", PlaygroundAsync)
            .WithName("Playground")
            .WithSummary("Upload an image to run detection + captioning in one request")
            .WithOpenApi()
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> PlaygroundAsync(
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

            byte[] bytes;
            await using (var ms = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var detectTask = yolo.DetectAsync(new MemoryStream(bytes), ct: ct);
            var captionTask = qwen.CaptionAsync(new MemoryStream(bytes), ct);

            await Task.WhenAll(detectTask, captionTask);

            return Results.Ok(new
            {
                Detections = detectTask.Result,
                Caption = captionTask.Result.Text,
                DetectionCount = detectTask.Result.Count
            });
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem("Backend unavailable: " + ex.Message, statusCode: 503);
        }
    }
}
