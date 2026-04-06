namespace MauiClient.Services;

/// <summary>Represents a physical or virtual camera device available on the host system.</summary>
/// <param name="Id">Platform-specific device identifier.</param>
/// <param name="Name">Human-readable device name (e.g. "Integrated Webcam", "USB Camera").</param>
public sealed record CameraDeviceInfo(string Id, string Name);

/// <summary>
/// Provides access to local camera devices (USB / wired and wireless / Bluetooth)
/// by enumerating available capture devices and streaming JPEG frames.
/// </summary>
public interface ILocalCameraService
{
    /// <summary>Returns all video-capture devices currently visible to the OS.</summary>
    Task<IReadOnlyList<CameraDeviceInfo>> GetCamerasAsync();

    /// <summary>
    /// Streams JPEG-encoded frames from the device identified by <paramref name="deviceId"/>
    /// until <paramref name="ct"/> is cancelled.
    /// </summary>
    IAsyncEnumerable<byte[]> CaptureFramesAsync(string deviceId, CancellationToken ct = default);
}
