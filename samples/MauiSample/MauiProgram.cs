using Fload.Attribution;
using Microsoft.Extensions.Logging;

namespace MauiSample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// Fload: once, early in launch. Returns at once; the SDK works in the background.
		FloadAttribution.Configure(SampleConfig.WriteKey, new FloadOptions
		{
			Endpoint = SampleConfig.Endpoint,
			LogLevel = SampleConfig.LogLevel,
		});
#if FLOAD_LOCAL_STUB && IOS
		// Test hook: `simctl launch` with SIMCTL_CHILD_FLOAD_SAMPLE_USER_ID=<id> sets a user id
		// without tapping the button (Android: the intent extra in MainActivity).
		FloadAttribution.SetUserId(Environment.GetEnvironmentVariable("FLOAD_SAMPLE_USER_ID"));
#endif

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
