using MauiClient.Converters;
using MauiClient.Services;
using MauiClient.ViewModels;
using MauiClient.Views;
using Microsoft.Extensions.Logging;

namespace MauiClient;

/// <summary>MAUI application builder and DI registration.</summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        // HTTP
        builder.Services.AddHttpClient();

        // API client (singleton — holds mutable BaseAddress / ApiKey settings)
        builder.Services.AddSingleton<VisionApiClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = new VisionApiClient(factory)
            {
                BaseAddress = Preferences.Default.Get("ServiceUrl", "http://localhost:5100"),
                ApiKey = Preferences.Default.Get("ApiKey", string.Empty)
            };
            return client;
        });

        // ViewModels
        builder.Services.AddTransient<HealthViewModel>();
        builder.Services.AddTransient<YoloViewModel>();
        builder.Services.AddTransient<QwenVlViewModel>();
        builder.Services.AddTransient<PipelineViewModel>();
        builder.Services.AddTransient<AdminViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Views
        builder.Services.AddTransient<HealthPage>();
        builder.Services.AddTransient<YoloPage>();
        builder.Services.AddTransient<QwenVlPage>();
        builder.Services.AddTransient<PipelinePage>();
        builder.Services.AddTransient<AdminPage>();
        builder.Services.AddTransient<SettingsPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
