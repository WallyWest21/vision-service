using MauiClient.ViewModels;

namespace MauiClient.Views;

/// <summary>Code-behind for the Live Feed page.</summary>
public partial class LiveFeedPage : ContentPage
{
    public LiveFeedPage(LiveFeedViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    /// <inheritdoc/>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Stop the feed automatically when navigating away
        if (BindingContext is LiveFeedViewModel vm && vm.IsStreaming)
            vm.StopStreamCommand.Execute(null);
    }
}
