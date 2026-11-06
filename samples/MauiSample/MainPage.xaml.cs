namespace MauiSample;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		Refresh();
		// The install reference appears once the native SDK has loaded its state.
		Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
		{
			Refresh();
			return FloadAttribution.InstallReference is null;
		});
	}

	private void Refresh()
	{
		StatusLabel.Text = FloadAttribution.IsSupported
			? $"Fload.Attribution {FloadAttribution.Version}, native SDK {FloadAttribution.NativeSdkVersion}"
			: "Not supported on this platform: calls do nothing.";
		ReferenceLabel.Text = $"Install reference: {FloadAttribution.InstallReference ?? "(loading)"}";
	}

	private void OnSetUserClicked(object? sender, EventArgs e)
	{
		FloadAttribution.SetUserId(UserIdEntry.Text);
		UserLabel.Text = $"User id set: {UserIdEntry.Text}";
	}

	private void OnRefreshClicked(object? sender, EventArgs e) => Refresh();
}
