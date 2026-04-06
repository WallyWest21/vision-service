using MauiClient.ViewModels;

namespace MauiClient.Views;

/// <summary>Code-behind for the Qwen-VL page.</summary>
public partial class QwenVlPage : ContentPage
{
    public QwenVlPage(QwenVlViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
