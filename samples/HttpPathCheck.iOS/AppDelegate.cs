using System;
using Foundation;
using UIKit;
using Fload.HttpAttribution;

namespace HttpPathCheck.iOS
{
    [Register("AppDelegate")]
    public class AppDelegate : UIApplicationDelegate
    {
        public override UIWindow Window { get; set; }

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            // The local ingest stub only (tools/ingest-stub). Never production.
            FloadIos.Start("fload_pk_test_local", new Uri("http://localhost:3901"));
            var userId = Environment.GetEnvironmentVariable("FLOAD_SAMPLE_USER_ID");
            if (userId != null) { var sent = FloadIos.SetUserIdAsync(userId); }

            Window = new UIWindow(UIScreen.MainScreen.Bounds);
            var controller = new UIViewController();
            controller.View.BackgroundColor = UIColor.SystemBackground;
            controller.View.AddSubview(new UILabel(Window.Frame) { Text = "Fload HTTP path check", TextAlignment = UITextAlignment.Center });
            Window.RootViewController = controller;
            Window.MakeKeyAndVisible();
            return true;
        }
    }
}
