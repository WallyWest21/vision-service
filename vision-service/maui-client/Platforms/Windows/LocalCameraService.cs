using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MauiClient.Services;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace MauiClient.WinUI;

/// <summary>
/// Windows implementation of <see cref="ILocalCameraService"/>.
/// Uses <c>Windows.Media.Capture.MediaCapture</c> and <c>MediaFrameReader</c>
/// to stream JPEG frames from any locally attached camera (USB or wireless).
/// </summary>
public sealed class LocalCameraService : ILocalCameraService
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<CameraDeviceInfo>> GetCamerasAsync()
    {
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        return devices
            .Select(d => new CameraDeviceInfo(d.Id, d.Name))
            .ToList();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<byte[]> CaptureFramesAsync(
        string deviceId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Bounded channel — drop oldest when the consumer falls behind
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(4)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });

        var capture = new MediaCapture();
        MediaFrameReader? frameReader = null;

        try
        {
            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = deviceId,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };

            await capture.InitializeAsync(settings);

            // Pick the first colour source
            var colorSource = capture.FrameSources.Values
                .FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color);

            if (colorSource is null)
            {
                channel.Writer.Complete();
                yield break;
            }

            frameReader = await capture.CreateFrameReaderAsync(
                colorSource, MediaEncodingSubtypes.Bgra8);

            frameReader.FrameArrived += async (reader, _) =>
            {
                if (ct.IsCancellationRequested) return;

                // Copy the bitmap out of the pooled MediaFrameReference immediately
                // so the frame can be returned to the reader pool before we encode.
                SoftwareBitmap? bitmap;
                using (var frame = reader.TryAcquireLatestFrame())
                {
                    var raw = frame?.VideoMediaFrame?.SoftwareBitmap;
                    if (raw is null) return;
                    bitmap = SoftwareBitmap.Copy(raw);
                }

                using (bitmap)
                {
                    var jpegBytes = await EncodeToJpegAsync(bitmap);
                    if (jpegBytes is { Length: > 0 } && !ct.IsCancellationRequested)
                        channel.Writer.TryWrite(jpegBytes);
                }
            };

            await frameReader.StartAsync();

            ct.Register(() => channel.Writer.TryComplete());

            await foreach (var jpegFrame in channel.Reader.ReadAllAsync(ct))
                yield return jpegFrame;
        }
        finally
        {
            if (frameReader is not null)
            {
                await frameReader.StopAsync();
                frameReader.Dispose();
            }

            capture.Dispose();
        }
    }

    /// <summary>Encodes a <see cref="SoftwareBitmap"/> to a JPEG byte array.</summary>
    private static async Task<byte[]> EncodeToJpegAsync(SoftwareBitmap sourceBitmap)
    {
        using var stream = new InMemoryRandomAccessStream();

        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

        // BitmapEncoder requires Bgra8 / Premultiplied; convert when necessary.
        // 'converted' must remain alive until after FlushAsync completes —
        // SetSoftwareBitmap only stores a reference; pixel data is read during flush.
        SoftwareBitmap? converted = null;
        try
        {
            if (sourceBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8
                || sourceBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                converted = SoftwareBitmap.Convert(
                    sourceBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                encoder.SetSoftwareBitmap(converted);
            }
            else
            {
                encoder.SetSoftwareBitmap(sourceBitmap);
            }

            await encoder.FlushAsync();
        }
        finally
        {
            converted?.Dispose();
        }

        stream.Seek(0);
        using var ms = stream.AsStream();
        var bytes = new byte[ms.Length];
        _ = await ms.ReadAsync(bytes);
        return bytes;
    }
}
