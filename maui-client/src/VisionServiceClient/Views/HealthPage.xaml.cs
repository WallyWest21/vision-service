using VisionServiceClient.ViewModels;

namespace VisionServiceClient.Views;

/// <summary>Displays service health status.</summary>
public partial class HealthPage : ContentPage
{
    /// <summary>Initializes the health page with its view model.</summary>
    public HealthPage(HealthViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
