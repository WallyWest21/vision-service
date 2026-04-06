using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionServiceClient.Services;

namespace VisionServiceClient.ViewModels;

/// <summary>View model for the Health page.</summary>
public partial class HealthViewModel : BaseViewModel
{
    private readonly IVisionApiService _api;

    [ObservableProperty]
    private string _healthStatus = "—";

    [ObservableProperty]
    private string _serviceVersion = "—";

    [ObservableProperty]
    private string _lastChecked = "—";

    /// <summary>Initializes the health view model.</summary>
    public HealthViewModel(IVisionApiService api)
    {
        _api = api;
    }

    /// <summary>Calls the health endpoint and updates the displayed status.</summary>
    [RelayCommand]
    private async Task CheckHealthAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearMessages();
        try
        {
            var h = await _api.GetHealthAsync();
            HealthStatus = h.Status;
            ServiceVersion = $"{h.Service} v{h.Version}";
            LastChecked = h.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            SetStatus("Health check successful.");
        }
        catch (Exception ex)
        {
            SetError($"Error: {ex.Message}");
            HealthStatus = "Unavailable";
        }
        finally { IsBusy = false; }
    }
}
