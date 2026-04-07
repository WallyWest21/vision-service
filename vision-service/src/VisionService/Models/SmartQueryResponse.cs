namespace VisionService.Models;

/// <summary>Response from the <c>/api/v1/pipeline/smart-query</c> endpoint.</summary>
public class SmartQueryResponse
{
    /// <summary>The original user query echoed back for reference.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>YOLO object detections found in the frame.</summary>
    public List<Detection> Detections { get; set; } = [];

    /// <summary>Qwen-VL natural-language analysis of the queried objects and their locations.</summary>
    public string VlAnalysis { get; set; } = string.Empty;

    /// <summary>Total number of objects detected by YOLO.</summary>
    public int TotalDetections { get; set; }

    /// <summary>Combined processing time (YOLO + Qwen-VL) in milliseconds.</summary>
    public double ProcessingTimeMs { get; set; }

    /// <summary>Name of the YOLO model used for detection.</summary>
    public string YoloModel { get; set; } = string.Empty;
}
