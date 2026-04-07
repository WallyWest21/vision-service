using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using VisionService.Clients;
using VisionService.Configuration;
using VisionService.Events;
using VisionService.Services;

namespace VisionService.Extensions;

/// <summary>Extension methods for registering services in the DI container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers all VisionService dependencies.</summary>
    public static IServiceCollection AddVisionServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<YoloOptions>()
            .Bind(configuration.GetSection(YoloOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<QwenVlOptions>()
            .Bind(configuration.GetSection(QwenVlOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName));

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PerformanceOptions>()
            .Bind(configuration.GetSection(PerformanceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName));

        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
        services.AddCors(options =>
        {
            options.AddPolicy("VisionCors", policy =>
            {
                var origins = corsOptions.AllowedOrigins;
                if (origins is null || origins.Length == 0)
                {
                    // No origins configured: deny all cross-origin requests
                    policy.WithOrigins(Array.Empty<string>());
                }
                else if (origins is ["*"])
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    // WithExposedHeaders is omitted for wildcard origins as the
                    // CORS spec does not permit it with AllowAnyOrigin()
                }
                else
                {
                    policy
                        .WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .WithExposedHeaders("X-Correlation-Id");
                }
            });
        });

        services.AddYoloClient(configuration);
        services.AddQwenVlClient(configuration);
        services.AddImageService();
        services.AddVisionEventBus();
        services.AddResponseCacheService();

        return services;
    }

    private static IServiceCollection AddYoloClient(this IServiceCollection services, IConfiguration configuration)
    {
        // Retry policy registered as singleton for efficiency; it holds no mutable cross-request state.
        services.AddKeyedSingleton<IAsyncPolicy<HttpResponseMessage>>("yolo-retry", (sp, _) =>
        {
            var opts = sp.GetRequiredService<IOptions<YoloOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<YoloClient>>();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: opts.MaxRetries,
                    sleepDurationProvider: retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                    onRetry: (outcome, delay, retryCount, _) =>
                        logger.LogWarning(
                            outcome.Exception,
                            "YOLO transient error on attempt {Retry}/{MaxRetries}, retrying after {Delay}ms. Reason: {Reason}",
                            retryCount, opts.MaxRetries, (int)delay.TotalMilliseconds,
                            outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()));
        });

        // Circuit breaker must be a singleton to maintain state across requests
        services.AddKeyedSingleton<IAsyncPolicy<HttpResponseMessage>>("yolo-circuit-breaker", (sp, _) =>
        {
            var opts = sp.GetRequiredService<IOptions<YoloOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<YoloClient>>();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: opts.CircuitBreakerThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(opts.CircuitBreakerDurationSeconds),
                    onBreak: (outcome, duration) =>
                        logger.LogError(
                            outcome.Exception,
                            "YOLO circuit breaker opened for {Duration:F1}s. Reason: {Reason}",
                            duration.TotalSeconds,
                            outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()),
                    onReset: () =>
                        logger.LogInformation("YOLO circuit breaker closed (reset)"),
                    onHalfOpen: () =>
                        logger.LogInformation("YOLO circuit breaker half-open (testing)"));
        });

        services.AddHttpClient<IYoloClient, YoloClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<YoloOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        })
        .AddPolicyHandler((sp, _) =>
            sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("yolo-retry"))
        .AddPolicyHandler((sp, _) =>
            sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("yolo-circuit-breaker"));

        return services;
    }

    private static IServiceCollection AddQwenVlClient(this IServiceCollection services, IConfiguration configuration)
    {
        // Retry policy registered as singleton for efficiency; it holds no mutable cross-request state.
        services.AddKeyedSingleton<IAsyncPolicy<HttpResponseMessage>>("qwenvl-retry", (sp, _) =>
        {
            var opts = sp.GetRequiredService<IOptions<QwenVlOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<QwenVlClient>>();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: opts.MaxRetries,
                    sleepDurationProvider: retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                    onRetry: (outcome, delay, retryCount, _) =>
                        logger.LogWarning(
                            outcome.Exception,
                            "Qwen-VL transient error on attempt {Retry}/{MaxRetries}, retrying after {Delay}ms. Reason: {Reason}",
                            retryCount, opts.MaxRetries, (int)delay.TotalMilliseconds,
                            outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()));
        });

        // Circuit breaker must be a singleton to maintain state across requests
        services.AddKeyedSingleton<IAsyncPolicy<HttpResponseMessage>>("qwenvl-circuit-breaker", (sp, _) =>
        {
            var opts = sp.GetRequiredService<IOptions<QwenVlOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<QwenVlClient>>();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: opts.CircuitBreakerThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(opts.CircuitBreakerDurationSeconds),
                    onBreak: (outcome, duration) =>
                        logger.LogError(
                            outcome.Exception,
                            "Qwen-VL circuit breaker opened for {Duration:F1}s. Reason: {Reason}",
                            duration.TotalSeconds,
                            outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()),
                    onReset: () =>
                        logger.LogInformation("Qwen-VL circuit breaker closed (reset)"),
                    onHalfOpen: () =>
                        logger.LogInformation("Qwen-VL circuit breaker half-open (testing)"));
        });

        services.AddHttpClient<IQwenVlClient, QwenVlClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<QwenVlOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        })
        .AddPolicyHandler((sp, _) =>
            sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("qwenvl-retry"))
        .AddPolicyHandler((sp, _) =>
            sp.GetRequiredKeyedService<IAsyncPolicy<HttpResponseMessage>>("qwenvl-circuit-breaker"));

        return services;
    }

    private static IServiceCollection AddImageService(this IServiceCollection services)
    {
        services.AddScoped<IImageService, ImageService>();
        return services;
    }

    private static IServiceCollection AddVisionEventBus(this IServiceCollection services)
    {
        services.AddSingleton<IVisionEventBus, InProcessEventBus>();
        return services;
    }

    private static IServiceCollection AddResponseCacheService(this IServiceCollection services)
    {
        services.AddMemoryCache();
        // Configure SizeLimit after options are registered so CacheOptions.MaxItems is respected
        services.AddOptions<MemoryCacheOptions>()
            .Configure<IOptions<CacheOptions>>((memOpts, cacheOpts) =>
            {
                memOpts.SizeLimit = cacheOpts.Value.MaxItems;
            });
        services.AddSingleton<IResponseCacheService, ResponseCacheService>();
        return services;
    }
}
