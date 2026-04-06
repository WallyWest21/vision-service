using VisionServiceClient.ViewModels;

namespace VisionServiceClient.Views;

/// <summary>Provides vision-language analysis via the Qwen-VL model.</summary>
public partial class QwenVlPage : ContentPage
{
    /// <summary>Initializes the Qwen-VL page with its view model.</summary>
    public QwenVlPage(QwenVlViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
