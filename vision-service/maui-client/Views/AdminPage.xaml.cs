using MauiClient.ViewModels;

namespace MauiClient.Views;

/// <summary>Code-behind for the Admin page.</summary>
public partial class AdminPage : ContentPage
{
    public AdminPage(AdminViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
