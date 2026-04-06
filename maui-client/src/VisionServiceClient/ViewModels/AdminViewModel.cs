using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionServiceClient.Models;
using VisionServiceClient.Services;

namespace VisionServiceClient.ViewModels;

/// <summary>View model for the Admin page, supporting listing and generating API keys.</summary>
public partial class AdminViewModel : BaseViewModel
{
    private readonly IVisionApiService _api;

    [ObservableProperty] private string _keysText = string.Empty;
    [ObservableProperty] private string _newKeyName = string.Empty;
    [ObservableProperty] private string _newKeyScopes = "detect,analyze";
    [ObservableProperty] private int _newKeyRpm = 60;
    [ObservableProperty] private string _generatedKey = string.Empty;

    /// <summary>Initializes the admin view model.</summary>
    public AdminViewModel(IVisionApiService api) => _api = api;

    /// <summary>Fetches and displays all configured API keys.</summary>
    [RelayCommand]
    private async Task ListKeysAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearMessages();
        try
        {
            var keys = await _api.GetApiKeysAsync();
            KeysText = keys.Length == 0
                ? "No API keys configured."
                : string.Join("\n\n", keys.Select(k =>
                    $"Name: {k.Name}\nPreview: {k.KeyPreview}\nScopes: {string.Join(", ", k.Scopes)}\nRate limit: {k.RequestsPerMinute} req/min"));
            SetStatus($"Loaded {keys.Length} API key(s).");
        }
        catch (Exception ex) { SetError($"Error: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    /// <summary>Generates a new API key and displays the result.</summary>
    [RelayCommand]
    private async Task GenerateKeyAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(NewKeyName)) { SetError("Please enter a key name."); return; }
        IsBusy = true;
        ClearMessages();
        try
        {
            var scopes = NewKeyScopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var req = new GenerateKeyRequest(NewKeyName, scopes, NewKeyRpm > 0 ? NewKeyRpm : null);
            var r = await _api.GenerateApiKeyAsync(req);
            GeneratedKey = r.Key;
            SetStatus($"Key '{r.Name}' generated. Copy it now — it won't be shown again.");
        }
        catch (Exception ex) { SetError($"Error: {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
