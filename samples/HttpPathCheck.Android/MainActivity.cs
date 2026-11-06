using System;
using Android.App;
using Android.OS;
using Fload.HttpAttribution;

namespace HttpPathCheck.Android
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);
            // The local ingest stub only (tools/ingest-stub); 10.0.2.2 is the host. Never production.
            FloadAndroid.Start(this, "fload_pk_test_local", new Uri("http://10.0.2.2:3901"));
            var userId = Intent == null ? null : Intent.GetStringExtra("fload_sample_user_id");
            if (userId != null) { var sent = FloadAndroid.SetUserIdAsync(userId); }
        }

        protected override void OnResume()
        {
            base.OnResume();
            var flushed = FloadAndroid.Flush();
        }
    }
}
