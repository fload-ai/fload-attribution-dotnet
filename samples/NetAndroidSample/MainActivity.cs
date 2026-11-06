using Fload.Attribution;

namespace NetAndroidSample;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
#if FLOAD_LOCAL_STUB
        // Test hook: adb shell am start ... --es fload_sample_user_id <id>
        FloadAttribution.SetUserId(Intent?.GetStringExtra("fload_sample_user_id"));
#endif
        SetContentView(new TextView(this)
        {
            Text = $"Fload.Attribution {FloadAttribution.Version}, native SDK {FloadAttribution.NativeSdkVersion}",
        });
    }
}
