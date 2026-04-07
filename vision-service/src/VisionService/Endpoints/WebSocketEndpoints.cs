using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VisionService.Clients;
using VisionService.Configuration;

namespace VisionService.Endpoints;

/// <summary>WebSocket endpoints for real-time frame processing.</summary>
public static class WebSocketEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Maps WebSocket streaming endpoint.</summary>
    public static IEndpointRouteBuilder MapWebSocketEndpoints(this IEndpointRouteBuilder app)
    {
        app.Map("/ws/stream", HandleStreamAsync);
        return app;
    }

    private static async Task HandleStreamAsync(
        HttpContext context,
        IYoloClient yolo,
        IQwenVlClient qwen,
        IOptionsMonitor<PerformanceOptions> perfOptions)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        var mode = context.Request.Query["mode"].ToString() switch
        {
            "caption" => "caption",
            "detect" => "detect",
            _ => "detect"
        };

        var buffer = new byte[perfOptions.CurrentValue.MaxWebSocketFrameBytes];

        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            using var ms = new MemoryStream();

            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close) break;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", context.RequestAborted);
                break;
            }

            if (result.MessageType != WebSocketMessageType.Binary) continue;

            ms.Position = 0;
            object? response = null;

            try
            {
                if (mode == "caption")
                {
                    var vlResponse = await qwen.CaptionAsync(ms, context.RequestAborted);
                    response = new { Mode = "caption", Result = vlResponse.Text };
                }
                else
                {
                    var detections = await yolo.DetectAsync(ms, ct: context.RequestAborted);
                    response = new { Mode = "detect", Detections = detections };
                }
            }
            catch (Exception ex)
            {
                response = new { Error = ex.Message };
            }

            var json = JsonSerializer.Serialize(response, JsonOpts);
            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, context.RequestAborted);
        }
    }
}
