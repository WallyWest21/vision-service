using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiClient.Models;
using MauiClient.Services;

namespace MauiClient.ViewModels;

/// <summary>ViewModel for the Admin page — key listing and creation.</summary>
public partial class AdminViewModel : BaseViewModel
{
    private readonly VisionApiClient _api;

    [ObservableProperty] private string _newKeyName = string.Empty;
    [ObservableProperty] private string _newKeyScopes = "detect,analyze";
    [ObservableProperty] private int _newKeyRpm = 60;
    [ObservableProperty] private ObservableCollection<ApiKeyPreview> _keys = [];

    public AdminViewModel(VisionApiClient api)
    {
        _api = api;
        Title = "Admin";
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task ListKeysAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            var keys = await _api.ListKeysAsync(ct);
            Keys = new ObservableCollection<ApiKeyPreview>(keys);

            var sb = new StringBuilder();
            sb.AppendLine($"Keys found: {keys.Count}");
            foreach (var k in keys)
                sb.AppendLine($"  • {k.Name}  [{string.Join(", ", k.Scopes)}]  {k.KeyPreview}  {k.RequestsPerMinute} rpm");
            SetResult(sb.ToString());
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

    [RelayCommand(CanExecute = nameof(CanAddKey))]
    private async Task AddKeyAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        try
        {
            var scopes = NewKeyScopes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var request = new NewApiKeyRequest
            {
                Name = NewKeyName,
                Scopes = scopes,
                RequestsPerMinute = NewKeyRpm
            };

            var response = await _api.AddKeyAsync(request, ct);
            SetResult($"Created key for '{response.Name}':\n{response.Key}\nScopes: {string.Join(", ", response.Scopes)}");
            NewKeyName = string.Empty;

            // Refresh list
            await ListKeysAsync(ct);
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

    private bool CanAddKey() => !string.IsNullOrWhiteSpace(NewKeyName) && IsNotBusy;

    partial void OnNewKeyNameChanged(string value) =>
        AddKeyCommand.NotifyCanExecuteChanged();
}
