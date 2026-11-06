using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MauiSample;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
#if FLOAD_LOCAL_STUB
		// Test hook: `adb shell am start -n com.fload.samples.maui/crc….MainActivity
		// --es fload_sample_user_id <id>` sets a user id without tapping the button.
		FloadAttribution.SetUserId(Intent?.GetStringExtra("fload_sample_user_id"));
#endif
	}
}
