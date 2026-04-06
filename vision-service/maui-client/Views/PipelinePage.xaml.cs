using MauiClient.ViewModels;

namespace MauiClient.Views;

/// <summary>Code-behind for the Pipeline page.</summary>
public partial class PipelinePage : ContentPage
{
    public PipelinePage(PipelineViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
