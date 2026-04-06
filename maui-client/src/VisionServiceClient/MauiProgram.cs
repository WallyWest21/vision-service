using Microsoft.Extensions.Logging;
using VisionServiceClient.Services;
using VisionServiceClient.ViewModels;
using VisionServiceClient.Views;

namespace VisionServiceClient;

/// <summary>Configures and builds the MAUI application.</summary>
public static class MauiProgram
{
    /// <summary>Creates and configures the MAUI application.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IVisionApiService, VisionApiService>();

        builder.Services.AddTransient<HealthViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<YoloViewModel>();
        builder.Services.AddTransient<QwenVlViewModel>();
        builder.Services.AddTransient<PipelineViewModel>();
        builder.Services.AddTransient<AdminViewModel>();

        builder.Services.AddTransient<HealthPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<YoloPage>();
        builder.Services.AddTransient<QwenVlPage>();
        builder.Services.AddTransient<PipelinePage>();
        builder.Services.AddTransient<AdminPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
