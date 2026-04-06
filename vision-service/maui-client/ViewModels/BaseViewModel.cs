using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiClient.ViewModels;

/// <summary>Base ViewModel with busy-state and result text.</summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public bool IsNotBusy => !IsBusy;

    protected void SetResult(string text, bool error = false)
    {
        ResultText = text;
        HasError = error;
    }

    protected void SetError(Exception ex) =>
        SetResult($"Error: {ex.Message}", error: true);
}
