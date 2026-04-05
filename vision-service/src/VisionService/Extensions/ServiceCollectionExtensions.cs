using VisionService.Configuration;

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

        return services;
    }
}
