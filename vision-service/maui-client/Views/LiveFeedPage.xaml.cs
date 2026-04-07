using System.ComponentModel;
using MauiClient.ViewModels;

namespace MauiClient.Views;

/// <summary>Code-behind for the Live Feed page.</summary>
public partial class LiveFeedPage : ContentPage
{
    private readonly BoundingBoxDrawable _drawable = new();

    public LiveFeedPage(LiveFeedViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        BoundingBoxOverlay.Drawable = _drawable;
    }

    /// <inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is LiveFeedViewModel vm)
            vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <inheritdoc/>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is LiveFeedViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            if (vm.IsStreaming)
                vm.StopStreamCommand.Execute(null);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LiveFeedViewModel vm) return;
        switch (e.PropertyName)
        {
            case nameof(LiveFeedViewModel.CurrentDetections):
            case nameof(LiveFeedViewModel.CurrentSegments):
            case nameof(LiveFeedViewModel.CurrentPoses):
                _drawable.Detections = vm.CurrentDetections;
                _drawable.Segments = vm.CurrentSegments;
                _drawable.Poses = vm.CurrentPoses;
                BoundingBoxOverlay.Invalidate();
                break;
            case nameof(LiveFeedViewModel.ImageNaturalWidth):
                _drawable.ImageWidth = vm.ImageNaturalWidth;
                break;
            case nameof(LiveFeedViewModel.ImageNaturalHeight):
                _drawable.ImageHeight = vm.ImageNaturalHeight;
                break;
        }
    }
}
