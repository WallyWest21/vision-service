using MauiClient.ViewModels;

namespace MauiClient.Views;

/// <summary>Code-behind for the Health check page.</summary>
public partial class HealthPage : ContentPage
{
    public HealthPage(HealthViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
