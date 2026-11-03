using Microsoft.Extensions.DependencyInjection;
using BasketballScout.App.Diagnostics;

namespace BasketballScout.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		TextSizeSettings.Apply(); // US-30: apply the saved Normal/Large text-size setting
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// If startup (e.g. DB init) failed, show the error instead of crashing.
		if (MauiProgram.StartupError is { } startupError)
			return new Window(BuildErrorPage(startupError));

		try
		{
			var window = new Window(new AppShell());
			DiagnosticsLog.TrackLifecycle(window); // was a freeze preceded by the iPad locking?
			return window;
		}
		catch (Exception ex)
		{
			return new Window(BuildErrorPage(ex));
		}
	}

	static Page BuildErrorPage(Exception ex) => new ContentPage
	{
		Content = new ScrollView
		{
			Content = new Label
			{
				Text = "Startup error:\n\n" + ex,
				Margin = 20,
				FontSize = 12
			}
		}
	};
}