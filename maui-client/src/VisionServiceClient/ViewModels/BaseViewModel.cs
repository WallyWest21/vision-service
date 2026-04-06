using CommunityToolkit.Mvvm.ComponentModel;

namespace VisionServiceClient.ViewModels;

/// <summary>Base view model providing busy state and status/error messaging.</summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>True when the view model is not busy executing an operation.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>Sets an error message and clears the status message.</summary>
    protected void SetError(string message)
    {
        ErrorMessage = message;
        StatusMessage = string.Empty;
    }

    /// <summary>Sets a status message and clears the error message.</summary>
    protected void SetStatus(string message)
    {
        StatusMessage = message;
        ErrorMessage = string.Empty;
    }

    /// <summary>Clears both status and error messages.</summary>
    protected void ClearMessages()
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
    }
}
