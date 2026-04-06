namespace VisionServiceClient.Models;

/// <summary>Health check response from the service.</summary>
public record HealthResponse(string Status, DateTime Timestamp, string Service, string Version);

/// <summary>Bounding box coordinates for a detected object.</summary>
public record BoundingBox(float X1, float Y1, float X2, float Y2, float Width, float Height);

/// <summary>A single object detection result.</summary>
public record Detection(string Label, float Confidence, BoundingBox BoundingBox, int ClassId);

/// <summary>Response from the detect endpoint.</summary>
public record DetectResponse(Detection[] Detections, long ProcessingTimeMs, string Model);

/// <summary>Batch detection result for one image file.</summary>
public record BatchDetectItem(string FileName, Detection[] Detections);

/// <summary>A single segmentation result.</summary>
public record Segmentation(string Label, float Confidence, BoundingBox BoundingBox, float[] Mask, int ClassId);

/// <summary>Response from the segment endpoint.</summary>
public record SegmentResponse(Segmentation[] Segmentations, string Model);

/// <summary>A single classification result.</summary>
public record ClassificationResult(string Label, float Confidence, int ClassId);

/// <summary>Response from the classify endpoint.</summary>
public record ClassifyResponse(ClassificationResult[] Classifications, string Model);

/// <summary>A single pose keypoint.</summary>
public record Keypoint(string Name, float X, float Y, float Confidence);

/// <summary>A single pose estimation result.</summary>
public record PoseResult(BoundingBox BoundingBox, float Confidence, Keypoint[] Keypoints);

/// <summary>Response from the pose endpoint.</summary>
public record PoseResponse(PoseResult[] Poses, string Model);

/// <summary>Response from vision-language endpoints.</summary>
public record VlResponse(string Text, string Model, int PromptTokens, int CompletionTokens);

/// <summary>Response from the detect-and-describe pipeline.</summary>
public record DetectAndDescribeResponse(Detection[] Detections, string Caption, int ObjectCount);

/// <summary>Response from the safety-check pipeline.</summary>
public record SafetyCheckResponse(bool IsSafe, string SafetyAnalysis, Detection[] Detections);

/// <summary>A single inventory item with count.</summary>
public record InventoryItem(string Item, int Count);

/// <summary>Response from the inventory pipeline.</summary>
public record InventoryResponse(InventoryItem[] ItemCounts, string VlInventory, int TotalDetections);

/// <summary>Response from the scene analysis pipeline.</summary>
public record SceneResponse(Detection[] Detections, string Caption, string ExtractedText, int DetectionCount);

/// <summary>API key metadata returned by the admin endpoint.</summary>
public record ApiKeyInfo(string Name, string[] Scopes, int RequestsPerMinute, string KeyPreview);

/// <summary>Request body for generating a new API key.</summary>
public record GenerateKeyRequest(string Name, string[]? Scopes, int? RequestsPerMinute);

/// <summary>Response when a new API key is generated.</summary>
public record GenerateKeyResponse(string Key, string Name, string[] Scopes);
