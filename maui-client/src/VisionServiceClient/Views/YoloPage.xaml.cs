using VisionServiceClient.ViewModels;

namespace VisionServiceClient.Views;

/// <summary>Provides YOLO object detection, segmentation, classification, pose estimation, and batch detection.</summary>
public partial class YoloPage : ContentPage
{
    /// <summary>Initializes the YOLO page with its view model.</summary>
    public YoloPage(YoloViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
