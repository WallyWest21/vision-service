using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiClient.Models;
using MauiClient.Services;

namespace MauiClient.ViewModels;

/// <summary>ViewModel for the Settings page — service URL, API key, and server performance tuning.</summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly VisionApiClient _api;

    [ObservableProperty] private string _serviceUrl = "http://100.108.155.28:5100";
    [ObservableProperty] private string _apiKey = string.Empty;

    // Rate limiting
    [ObservableProperty] private int _requestsPerMinute = 3000;
    [ObservableProperty] private int _burstSize = 100;

    // Cache
    [ObservableProperty] private bool _cacheEnabled = true;
    [ObservableProperty] private int _cacheTtlSeconds = 300;
    [ObservableProperty] private int _cacheMaxItems = 1000;

    // Performance
    [ObservableProperty] private int _minAiIntervalMs = 500;
    [ObservableProperty] private int _maxWebSocketFrameMb = 5;
    [ObservableProperty] private int _healthCheckIntervalSeconds = 30;
    [ObservableProperty] private int _imageCleanupIntervalHours = 6;
    [ObservableProperty] private int _maxConcurrentAiRequests;

    // YOLO
    [ObservableProperty] private int _yoloTimeoutSeconds = 30;
    [ObservableProperty] private int _yoloMaxRetries = 3;

    // QwenVl
    [ObservableProperty] private int _qwenVlMaxTokens = 1024;
    [ObservableProperty] private double _qwenVlTemperature = 0.7;
    [ObservableProperty] private int _qwenVlTimeoutSeconds = 120;

    // Storage
    [ObservableProperty] private int _retentionDays = 7;
    [ObservableProperty] private int _maxFileSizeMb = 20;

    /// <summary><c>true</c> while loading or saving server settings.</summary>
    [ObservableProperty] private bool _isLoadingServerSettings;

    public SettingsViewModel(VisionApiClient api)
    {
        _api = api;
        Title = "Settings";
        _serviceUrl = api.BaseAddress;
        _apiKey = api.ApiKey;
    }

    [RelayCommand]
    private void Save()
    {
        _api.BaseAddress = ServiceUrl.TrimEnd('/');
        _api.ApiKey = ApiKey;
        Preferences.Default.Set("ServiceUrl", _api.BaseAddress);
        Preferences.Default.Set("ApiKey", _api.ApiKey);
        SetResult("Settings saved.");
    }

    [RelayCommand]
    private void Load()
    {
        ServiceUrl = Preferences.Default.Get("ServiceUrl", "http://100.108.155.28:5100");
        ApiKey = Preferences.Default.Get("ApiKey", string.Empty);
        _api.BaseAddress = ServiceUrl;
        _api.ApiKey = ApiKey;
        SetResult("Settings loaded.");
    }

    /// <summary>Fetches the current runtime settings from the server.</summary>
    [RelayCommand]
    private async Task LoadServerSettingsAsync()
    {
        IsLoadingServerSettings = true;
        try
        {
            var s = await _api.GetSettingsAsync();

            if (s.RateLimit is { } rl)
            {
                if (rl.RequestsPerMinute.HasValue) RequestsPerMinute = rl.RequestsPerMinute.Value;
                if (rl.BurstSize.HasValue) BurstSize = rl.BurstSize.Value;
            }
            if (s.Cache is { } c)
            {
                if (c.Enabled.HasValue) CacheEnabled = c.Enabled.Value;
                if (c.DefaultTtlSeconds.HasValue) CacheTtlSeconds = c.DefaultTtlSeconds.Value;
                if (c.MaxItems.HasValue) CacheMaxItems = c.MaxItems.Value;
            }
            if (s.Performance is { } p)
            {
                if (p.MinAiIntervalMs.HasValue) MinAiIntervalMs = p.MinAiIntervalMs.Value;
                if (p.MaxWebSocketFrameBytes.HasValue) MaxWebSocketFrameMb = p.MaxWebSocketFrameBytes.Value / (1024 * 1024);
                if (p.HealthCheckIntervalSeconds.HasValue) HealthCheckIntervalSeconds = p.HealthCheckIntervalSeconds.Value;
                if (p.ImageCleanupIntervalHours.HasValue) ImageCleanupIntervalHours = p.ImageCleanupIntervalHours.Value;
                if (p.MaxConcurrentAiRequests.HasValue) MaxConcurrentAiRequests = p.MaxConcurrentAiRequests.Value;
            }
            if (s.Yolo is { } y)
            {
                if (y.TimeoutSeconds.HasValue) YoloTimeoutSeconds = y.TimeoutSeconds.Value;
                if (y.MaxRetries.HasValue) YoloMaxRetries = y.MaxRetries.Value;
            }
            if (s.QwenVl is { } q)
            {
                if (q.MaxTokens.HasValue) QwenVlMaxTokens = q.MaxTokens.Value;
                if (q.Temperature.HasValue) QwenVlTemperature = q.Temperature.Value;
                if (q.TimeoutSeconds.HasValue) QwenVlTimeoutSeconds = q.TimeoutSeconds.Value;
            }
            if (s.Storage is { } st)
            {
                if (st.RetentionDays.HasValue) RetentionDays = st.RetentionDays.Value;
                if (st.MaxFileSizeMb.HasValue) MaxFileSizeMb = st.MaxFileSizeMb.Value;
            }

            SetResult("Server settings loaded.");
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            IsLoadingServerSettings = false;
        }
    }

    /// <summary>Pushes the current values to the server as runtime overrides.</summary>
    [RelayCommand]
    private async Task SaveServerSettingsAsync()
    {
        IsLoadingServerSettings = true;
        try
        {
            var settings = new RuntimeSettings
            {
                RateLimit = new RateLimitSettings
                {
                    RequestsPerMinute = RequestsPerMinute,
                    BurstSize = BurstSize
                },
                Cache = new CacheSettings
                {
                    Enabled = CacheEnabled,
                    DefaultTtlSeconds = CacheTtlSeconds,
                    MaxItems = CacheMaxItems
                },
                Performance = new PerformanceSettings
                {
                    MinAiIntervalMs = MinAiIntervalMs,
                    MaxWebSocketFrameBytes = MaxWebSocketFrameMb * 1024 * 1024,
                    HealthCheckIntervalSeconds = HealthCheckIntervalSeconds,
                    ImageCleanupIntervalHours = ImageCleanupIntervalHours,
                    MaxConcurrentAiRequests = MaxConcurrentAiRequests
                },
                Yolo = new YoloSettings
                {
                    TimeoutSeconds = YoloTimeoutSeconds,
                    MaxRetries = YoloMaxRetries
                },
                QwenVl = new QwenVlSettings
                {
                    MaxTokens = QwenVlMaxTokens,
                    Temperature = QwenVlTemperature,
                    TimeoutSeconds = QwenVlTimeoutSeconds
                },
                Storage = new StorageSettings
                {
                    RetentionDays = RetentionDays,
                    MaxFileSizeMb = MaxFileSizeMb
                }
            };

            await _api.UpdateSettingsAsync(settings);
            SetResult("Server settings updated. Changes take effect immediately.");
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            IsLoadingServerSettings = false;
        }
    }
}
