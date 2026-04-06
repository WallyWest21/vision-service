using Microsoft.UI.Xaml;

namespace MauiClient.WinUI;

/// <summary>
/// WinUI 3 application host — logical entry point for the Windows platform.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>Initializes the WinUI application singleton.</summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
