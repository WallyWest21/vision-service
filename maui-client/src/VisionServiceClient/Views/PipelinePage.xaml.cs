using VisionServiceClient.ViewModels;

namespace VisionServiceClient.Views;

/// <summary>Provides combined AI pipeline operations combining YOLO and Qwen-VL.</summary>
public partial class PipelinePage : ContentPage
{
    /// <summary>Initializes the pipeline page with its view model.</summary>
    public PipelinePage(PipelineViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
