using Microsoft.Extensions.DependencyInjection;

namespace BasketballScout.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// If startup (e.g. DB init) failed, show the error instead of crashing.
		if (MauiProgram.StartupError is { } startupError)
			return new Window(BuildErrorPage(startupError));

		try
		{
			return new Window(new AppShell());
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