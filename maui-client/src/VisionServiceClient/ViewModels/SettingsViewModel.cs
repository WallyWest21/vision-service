using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionServiceClient.Services;

namespace VisionServiceClient.ViewModels;

/// <summary>View model for the Settings page.</summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly IVisionApiService _api;

    [ObservableProperty]
    private string _baseUrl = Preferences.Get("BaseUrl", "http://localhost:5100");

    [ObservableProperty]
    private string _apiKey = Preferences.Get("ApiKey", string.Empty);

    /// <summary>Initializes settings and applies stored configuration to the API service.</summary>
    public SettingsViewModel(IVisionApiService api)
    {
        _api = api;
        _api.Configure(BaseUrl, ApiKey);
    }

    /// <summary>Persists settings to Preferences and reconfigures the API service.</summary>
    [RelayCommand]
    private void Save()
    {
        Preferences.Set("BaseUrl", BaseUrl);
        Preferences.Set("ApiKey", ApiKey);
        _api.Configure(BaseUrl, ApiKey);
        SetStatus("Settings saved successfully.");
    }
}
