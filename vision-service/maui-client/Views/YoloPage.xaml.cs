using MauiClient.ViewModels;

namespace MauiClient.Views;

/// <summary>Code-behind for the YOLO page.</summary>
public partial class YoloPage : ContentPage
{
    public YoloPage(YoloViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
