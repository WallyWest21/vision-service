using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VisionService.Configuration;

namespace VisionService.Services;

/// <summary>
/// In-memory response cache that deduplicates concurrent requests for identical image + operation combinations.
/// </summary>
public sealed class ResponseCacheService : IResponseCacheService
{
    private readonly IMemoryCache _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<ResponseCacheService> _logger;

    // Per-key semaphores ensure only one backend call runs at a time for the same cache key.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    /// <summary>Initializes a new instance of <see cref="ResponseCacheService"/>.</summary>
    public ResponseCacheService(IMemoryCache cache, IOptions<CacheOptions> options, ILogger<ResponseCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc/>
    public string ComputeKey(byte[] imageBytes, string operation, string parameters = "")
    {
        // SHA-256 of image bytes; truncated to first 16 hex chars (8 bytes) for brevity
        var hash = SHA256.HashData(imageBytes);
        var imageHash = Convert.ToHexString(hash)[..16];
        var paramHash = string.IsNullOrEmpty(parameters)
            ? string.Empty
            : ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(parameters)))[..8];
        return $"{operation}:{imageHash}{paramHash}";
    }

    /// <inheritdoc/>
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        if (!_options.Enabled)
            return await factory();

        // Fast path — already cached
        if (_cache.TryGetValue(key, out T? cached))
        {
            _logger.LogDebug("Cache hit for key {CacheKey}", key);
            return cached!;
        }

        // Slow path — acquire a per-key lock to deduplicate concurrent requests
        var gate = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            // Double-check after acquiring the lock
            if (_cache.TryGetValue(key, out cached))
            {
                _logger.LogDebug("Cache hit (after lock) for key {CacheKey}", key);
                return cached!;
            }

            _logger.LogDebug("Cache miss for key {CacheKey} — calling factory", key);
            var result = await factory();

            var entryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(ttl ?? TimeSpan.FromSeconds(_options.DefaultTtlSeconds))
                .SetSize(1);

            _cache.Set(key, result, entryOptions);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }
}
