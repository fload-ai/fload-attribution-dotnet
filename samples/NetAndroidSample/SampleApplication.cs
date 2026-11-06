using Android.Runtime;
using Fload.Attribution;

namespace NetAndroidSample;

[Application]
public class SampleApplication : Application
{
    public SampleApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

    public override void OnCreate()
    {
        base.OnCreate();
#if FLOAD_LOCAL_STUB
        // The local ingest stub on the host (10.0.2.2). Your app: FloadAttribution.Configure(this, "fload_pk_…").
        FloadAttribution.Configure(this, "fload_pk_test_local", new FloadOptions
        {
            Endpoint = new Uri("http://10.0.2.2:3901"),
            LogLevel = FloadLogLevel.Debug,
        });
#else
        FloadAttribution.Configure(this, "fload_pk_REPLACE_ME");
#endif
    }
}
