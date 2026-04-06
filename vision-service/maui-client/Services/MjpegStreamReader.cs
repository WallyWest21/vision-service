using System.Runtime.CompilerServices;

namespace MauiClient.Services;

/// <summary>
/// Reads video frames from an MJPEG HTTP stream or polls a snapshot URL at regular intervals.
/// Frames are returned as raw JPEG byte arrays via <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
public class MjpegStreamReader
{
    private readonly IHttpClientFactory _factory;

    /// <summary>Initialises a new <see cref="MjpegStreamReader"/>.</summary>
    public MjpegStreamReader(IHttpClientFactory factory) => _factory = factory;

    /// <summary>
    /// Yields raw JPEG byte arrays from <paramref name="url"/>.
    /// When the server returns a <c>multipart/x-mixed-replace</c> response the MJPEG
    /// boundary stream is parsed by locating SOI (0xFF 0xD8) and EOI (0xFF 0xD9) markers.
    /// For any other content-type the URL is polled as a snapshot endpoint every
    /// <paramref name="pollIntervalMs"/> milliseconds.
    /// </summary>
    public async IAsyncEnumerable<byte[]> ReadFramesAsync(
        string url,
        int pollIntervalMs = 500,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var client = _factory.CreateClient();
        client.Timeout = Timeout.InfiniteTimeSpan;

        HttpResponseMessage? response = null;
        bool gotResponse = false;

        try
        {
            response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            gotResponse = true;
        }
        catch { response?.Dispose(); }

        if (!gotResponse) yield break;

        var contentType = response!.Content.Headers.ContentType?.MediaType ?? string.Empty;

        if (contentType.Contains("multipart", StringComparison.OrdinalIgnoreCase))
        {
            await foreach (var frame in ReadMjpegStreamAsync(response, ct))
                yield return frame;
        }
        else
        {
            byte[]? first = null;
            bool firstOk = false;

            try
            {
                first = await response.Content.ReadAsByteArrayAsync(ct);
                firstOk = true;
            }
            catch { }
            finally { response.Dispose(); }

            if (!firstOk) yield break;
            if (first is { Length: > 0 }) yield return first;

            while (!ct.IsCancellationRequested)
            {
                byte[]? bytes = null;
                bool pollOk = false;

                try
                {
                    await Task.Delay(pollIntervalMs, ct);
                    bytes = await client.GetByteArrayAsync(url, ct);
                    pollOk = true;
                }
                catch (OperationCanceledException) { }
                catch { }

                if (ct.IsCancellationRequested) yield break;
                if (pollOk && bytes is { Length: > 0 }) yield return bytes;
            }
        }
    }

    /// <summary>Parses an MJPEG multipart HTTP response stream, yielding one JPEG per frame.</summary>
    private static async IAsyncEnumerable<byte[]> ReadMjpegStreamAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var _r = response;

        Stream? stream = null;
        bool streamOk = false;

        try
        {
            stream = await response.Content.ReadAsStreamAsync(ct);
            streamOk = true;
        }
        catch { }

        if (!streamOk || stream is null) yield break;

        using var _s = stream;

        var readBuf = new byte[65536];
        var accumulated = new List<byte>(1024 * 1024);
        bool done = false;

        while (!done && !ct.IsCancellationRequested)
        {
            int read = 0;

            try
            {
                read = await stream.ReadAsync(readBuf, ct);
                if (read == 0) done = true;
            }
            catch (OperationCanceledException) { done = true; }
            catch { done = true; }

            if (!done)
                accumulated.AddRange(readBuf.AsSpan(0, read).ToArray());

            // Extract every complete JPEG frame present in the accumulated buffer
            while (!done)
            {
                int soi = IndexOf(accumulated, 0xFF, 0xD8);
                if (soi < 0) { TrimTo(accumulated, 1); break; }

                int eoi = IndexOf(accumulated, 0xFF, 0xD9, soi + 2);
                if (eoi < 0)
                {
                    if (soi > 0) accumulated.RemoveRange(0, soi);
                    break;
                }

                int end = eoi + 2;
                yield return accumulated.GetRange(soi, end - soi).ToArray();
                accumulated.RemoveRange(0, end);
            }
        }
    }

    private static int IndexOf(List<byte> buf, byte b0, byte b1, int from = 0)
    {
        for (int i = from; i < buf.Count - 1; i++)
            if (buf[i] == b0 && buf[i + 1] == b1) return i;
        return -1;
    }

    private static void TrimTo(List<byte> buf, int keepLast)
    {
        if (buf.Count > keepLast)
            buf.RemoveRange(0, buf.Count - keepLast);
    }
}
