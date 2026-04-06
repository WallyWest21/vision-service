namespace VisionServiceClient;

/// <summary>Application entry point.</summary>
public partial class App : Application
{
    /// <summary>Initializes the application.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell()) { Title = "Vision Service Client" };
    }
}
