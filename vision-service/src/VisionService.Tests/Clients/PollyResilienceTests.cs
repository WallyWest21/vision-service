using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Xunit;

namespace VisionService.Tests.Clients;

/// <summary>Tests for Polly retry and circuit-breaker resilience policies applied to the AI backend HTTP clients.</summary>
public class PollyResilienceTests
{
    /// <summary>
    /// Verifies that the retry policy re-executes transient failures and ultimately returns a success
    /// response once the backend recovers within the allowed retry count.
    /// </summary>
    [Fact]
    public async Task RetryPolicy_SucceedsAfterTransientFailures()
    {
        const int maxRetries = 3;
        int callCount = 0;

        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(maxRetries, _ => TimeSpan.Zero);

        // Fail on first 3 calls, succeed on the 4th (1 original + 3 retries)
        var response = await policy.ExecuteAsync(() =>
        {
            callCount++;
            if (callCount <= maxRetries)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        response.IsSuccessStatusCode.Should().BeTrue();
        callCount.Should().Be(maxRetries + 1);
    }

    /// <summary>
    /// Verifies that the retry policy also handles 408 Request Timeout as a transient error.
    /// </summary>
    [Fact]
    public async Task RetryPolicy_RetriesOn408RequestTimeout()
    {
        const int maxRetries = 2;
        int callCount = 0;

        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(maxRetries, _ => TimeSpan.Zero);

        var response = await policy.ExecuteAsync(() =>
        {
            callCount++;
            if (callCount <= maxRetries)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestTimeout));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        response.IsSuccessStatusCode.Should().BeTrue();
        callCount.Should().Be(maxRetries + 1);
    }

    /// <summary>
    /// Verifies that after exhausting all retries the last failed response is returned to the caller.
    /// </summary>
    [Fact]
    public async Task RetryPolicy_ExhaustsRetries_ReturnsLastFailedResponse()
    {
        const int maxRetries = 3;
        int callCount = 0;

        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(maxRetries, _ => TimeSpan.Zero);

        // Always fail
        var response = await policy.ExecuteAsync(() =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        callCount.Should().Be(maxRetries + 1); // initial attempt + retries
    }

    /// <summary>
    /// Verifies that the circuit breaker opens after the configured number of consecutive failures
    /// and throws <see cref="BrokenCircuitException"/> for subsequent calls.
    /// </summary>
    [Fact]
    public async Task CircuitBreakerPolicy_OpensAfterConsecutiveFailures()
    {
        const int threshold = 3;

        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(threshold, TimeSpan.FromSeconds(30));

        // Drive consecutive failures to open the circuit
        for (int i = 0; i < threshold; i++)
        {
            await policy.ExecuteAsync(() =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        }

        // The circuit is now open — next execution must throw BrokenCircuitException
        Func<Task> act = () => policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        await act.Should().ThrowAsync<BrokenCircuitException>();
    }

    /// <summary>
    /// Verifies that the circuit breaker does NOT open before reaching the failure threshold.
    /// </summary>
    [Fact]
    public async Task CircuitBreakerPolicy_DoesNotOpenBeforeThreshold()
    {
        const int threshold = 5;

        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(threshold, TimeSpan.FromSeconds(30));

        // Drive failures up to (threshold - 1) without opening
        for (int i = 0; i < threshold - 1; i++)
        {
            await policy.ExecuteAsync(() =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        }

        // Circuit should still be closed — a healthy response should succeed
        var response = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the retry policy handles <see cref="HttpRequestException"/> (e.g. network failure).
    /// </summary>
    [Fact]
    public async Task RetryPolicy_RetriesOnHttpRequestException()
    {
        const int maxRetries = 2;
        int callCount = 0;

        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(maxRetries, _ => TimeSpan.Zero);

        var response = await policy.ExecuteAsync(() =>
        {
            callCount++;
            if (callCount <= maxRetries)
                throw new HttpRequestException("Transient network error");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        response.IsSuccessStatusCode.Should().BeTrue();
        callCount.Should().Be(maxRetries + 1);
    }
}
