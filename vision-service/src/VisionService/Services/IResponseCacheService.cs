namespace VisionService.Services;

/// <summary>Service for caching vision API responses keyed by image content hash and request parameters.</summary>
public interface IResponseCacheService
{
    /// <summary>Computes a stable cache key from image bytes, operation name and extra parameters.</summary>
    /// <param name="imageBytes">Raw image bytes used as the primary hash input.</param>
    /// <param name="operation">Operation name (e.g. "detect", "caption").</param>
    /// <param name="parameters">Additional key discriminators such as confidence or question text.</param>
    /// <returns>A deterministic string key.</returns>
    string ComputeKey(byte[] imageBytes, string operation, string parameters = "");

    /// <summary>
    /// Returns a cached value if one exists for <paramref name="key"/>, otherwise calls
    /// <paramref name="factory"/> exactly once (even under concurrent requests) and caches the result.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="key">Cache key produced by <see cref="ComputeKey"/>.</param>
    /// <param name="factory">Async factory invoked on a cache miss.</param>
    /// <param name="ttl">Optional TTL override; uses <c>CacheOptions.DefaultTtlSeconds</c> when null.</param>
    /// <returns>Cached or freshly computed result.</returns>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null);

    /// <summary>Returns true when the cache is enabled.</summary>
    bool IsEnabled { get; }
}
