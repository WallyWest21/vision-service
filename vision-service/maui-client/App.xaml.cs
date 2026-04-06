namespace MauiClient;

/// <summary>Application entry — creates the main window around the Shell.</summary>
public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_shell);
}
