namespace BasketballScout.App.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
        VersionLabel.Text = $"Version {AppInfo.Current.VersionString} (build {AppInfo.Current.BuildString})";
        LargeTextSwitch.IsToggled = TextSizeSettings.IsLarge;
    }

    private void OnLargeTextToggled(object sender, ToggledEventArgs e)
        => TextSizeSettings.IsLarge = e.Value;
}
