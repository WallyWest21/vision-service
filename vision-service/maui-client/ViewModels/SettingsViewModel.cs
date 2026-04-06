using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiClient.Services;

namespace MauiClient.ViewModels;

/// <summary>ViewModel for the Settings page — service URL and API key.</summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly VisionApiClient _api;

    [ObservableProperty] private string _serviceUrl = "http://100.108.155.28:5100";
    [ObservableProperty] private string _apiKey = string.Empty;

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
}
