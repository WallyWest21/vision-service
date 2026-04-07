using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
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
        services.AddHttpClient<IYoloClient, YoloClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<YoloOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });
        return services;
    }

    private static IServiceCollection AddQwenVlClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IQwenVlClient, QwenVlClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<QwenVlOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });
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
