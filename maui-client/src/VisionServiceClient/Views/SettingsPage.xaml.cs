using VisionServiceClient.ViewModels;

namespace VisionServiceClient.Views;

/// <summary>Allows configuring the service base URL and API key.</summary>
public partial class SettingsPage : ContentPage
{
    /// <summary>Initializes the settings page with its view model.</summary>
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
