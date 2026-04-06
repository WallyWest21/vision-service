using VisionServiceClient.ViewModels;

namespace VisionServiceClient.Views;

/// <summary>Provides API key listing and generation.</summary>
public partial class AdminPage : ContentPage
{
    /// <summary>Initializes the admin page with its view model.</summary>
    public AdminPage(AdminViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
