using MauiClient.ViewModels;

namespace MauiClient.Views;

/// <summary>Code-behind for the Settings page.</summary>
public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
