using Fload.Attribution;

namespace NetiOSSample;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
#if FLOAD_LOCAL_STUB
        // The local ingest stub (tools/ingest-stub). Your app: FloadAttribution.Configure("fload_pk_…").
        FloadAttribution.Configure("fload_pk_test_local", new FloadOptions
        {
            Endpoint = new Uri("http://localhost:3901"),
            LogLevel = FloadLogLevel.Debug,
        });
        // Test hook: simctl launch with SIMCTL_CHILD_FLOAD_SAMPLE_USER_ID=<id>.
        FloadAttribution.SetUserId(Environment.GetEnvironmentVariable("FLOAD_SAMPLE_USER_ID"));
#else
        FloadAttribution.Configure("fload_pk_REPLACE_ME");
#endif

        Window = new UIWindow(UIScreen.MainScreen.Bounds);
        var controller = new UIViewController();
        controller.View!.BackgroundColor = UIColor.SystemBackground;
        controller.View.AddSubview(new UILabel(Window.Frame)
        {
            Text = $"Fload.Attribution {FloadAttribution.Version}, native SDK {FloadAttribution.NativeSdkVersion}",
            TextAlignment = UITextAlignment.Center,
            Lines = 0,
        });
        Window.RootViewController = controller;
        Window.MakeKeyAndVisible();
        return true;
    }
}
