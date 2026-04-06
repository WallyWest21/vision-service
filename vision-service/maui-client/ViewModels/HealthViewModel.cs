using CommunityToolkit.Mvvm.Input;
using MauiClient.Services;

namespace MauiClient.ViewModels;

/// <summary>ViewModel for the Health check page.</summary>
public partial class HealthViewModel : BaseViewModel
{
    private readonly VisionApiClient _api;

    public HealthViewModel(VisionApiClient api)
    {
        _api = api;
        Title = "Health";
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task CheckHealthAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            var result = await _api.HealthAsync(ct);
            SetResult(result);
        }
        catch (Exception ex)
        {
            SetError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
