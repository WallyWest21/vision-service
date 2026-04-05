using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VisionService.Configuration;
using VisionService.Services;
using Xunit;

namespace VisionService.Tests.Services;

public class ResponseCacheServiceTests
{
    private static ResponseCacheService CreateService(bool enabled = true, int ttlSeconds = 300)
    {
        var options = Options.Create(new CacheOptions
        {
            Enabled = enabled,
            DefaultTtlSeconds = ttlSeconds,
            MaxItems = 100
        });
        var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        return new ResponseCacheService(memoryCache, options, NullLogger<ResponseCacheService>.Instance);
    }

    // ── ComputeKey ──────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeKey_SameInputs_ReturnsSameKey()
    {
        var svc = CreateService();
        var bytes = new byte[] { 1, 2, 3 };

        var key1 = svc.ComputeKey(bytes, "detect", "0.50");
        var key2 = svc.ComputeKey(bytes, "detect", "0.50");

        key1.Should().Be(key2);
    }

    [Fact]
    public void ComputeKey_DifferentImages_ReturnsDifferentKeys()
    {
        var svc = CreateService();

        var key1 = svc.ComputeKey(new byte[] { 1, 2, 3 }, "detect");
        var key2 = svc.ComputeKey(new byte[] { 4, 5, 6 }, "detect");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void ComputeKey_DifferentOperations_ReturnsDifferentKeys()
    {
        var svc = CreateService();
        var bytes = new byte[] { 1, 2, 3 };

        var key1 = svc.ComputeKey(bytes, "detect");
        var key2 = svc.ComputeKey(bytes, "caption");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void ComputeKey_DifferentParameters_ReturnsDifferentKeys()
    {
        var svc = CreateService();
        var bytes = new byte[] { 1, 2, 3 };

        var key1 = svc.ComputeKey(bytes, "detect", "0.50");
        var key2 = svc.ComputeKey(bytes, "detect", "0.80");

        key1.Should().NotBe(key2);
    }

    // ── IsEnabled ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsEnabled_WhenEnabled_ReturnsTrue()
    {
        var svc = CreateService(enabled: true);
        svc.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ReturnsFalse()
    {
        var svc = CreateService(enabled: false);
        svc.IsEnabled.Should().BeFalse();
    }

    // ── GetOrCreateAsync (cache hit / miss) ────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_FirstCall_InvokesFactory()
    {
        var svc = CreateService();
        var factoryCalls = 0;

        await svc.GetOrCreateAsync("key1", () =>
        {
            factoryCalls++;
            return Task.FromResult("result");
        });

        factoryCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_SecondCallWithSameKey_ReturnsCachedResult()
    {
        var svc = CreateService();
        var factoryCalls = 0;

        await svc.GetOrCreateAsync("key2", () =>
        {
            factoryCalls++;
            return Task.FromResult("cached");
        });

        var result = await svc.GetOrCreateAsync("key2", () =>
        {
            factoryCalls++;
            return Task.FromResult("should-not-be-returned");
        });

        factoryCalls.Should().Be(1);
        result.Should().Be("cached");
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentKeys_InvokesFactoryForEach()
    {
        var svc = CreateService();
        var factoryCalls = 0;

        await svc.GetOrCreateAsync("keyA", () => { factoryCalls++; return Task.FromResult(1); });
        await svc.GetOrCreateAsync("keyB", () => { factoryCalls++; return Task.FromResult(2); });

        factoryCalls.Should().Be(2);
    }

    // ── Disabled cache ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_CacheDisabled_AlwaysInvokesFactory()
    {
        var svc = CreateService(enabled: false);
        var factoryCalls = 0;

        await svc.GetOrCreateAsync("key3", () => { factoryCalls++; return Task.FromResult("r"); });
        await svc.GetOrCreateAsync("key3", () => { factoryCalls++; return Task.FromResult("r"); });

        factoryCalls.Should().Be(2);
    }

    // ── Request deduplication ───────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentIdenticalRequests_InvokesFactoryOnlyOnce()
    {
        var svc = CreateService();
        var factoryCalls = 0;
        const string key = "dedup-key";

        var delay = TimeSpan.FromMilliseconds(50);

        async Task<string> SlowFactory()
        {
            Interlocked.Increment(ref factoryCalls);
            await Task.Delay(delay);
            return "result";
        }

        // Fire 5 concurrent requests for the same key
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => svc.GetOrCreateAsync(key, SlowFactory))
            .ToList();

        var results = await Task.WhenAll(tasks);

        factoryCalls.Should().Be(1);
        results.Should().AllBe("result");
    }

    // ── TTL ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAsync_WithCustomTtl_UsesTtlForExpiration()
    {
        var svc = CreateService();
        var factoryCalls = 0;
        const string key = "ttl-key";

        // Cache with a very short TTL
        await svc.GetOrCreateAsync(key, () =>
        {
            factoryCalls++;
            return Task.FromResult("v");
        }, TimeSpan.FromMilliseconds(1));

        // Wait for expiry
        await Task.Delay(50);

        // Should invoke factory again after expiration
        await svc.GetOrCreateAsync(key, () =>
        {
            factoryCalls++;
            return Task.FromResult("v2");
        }, TimeSpan.FromSeconds(60));

        factoryCalls.Should().Be(2);
    }
}
