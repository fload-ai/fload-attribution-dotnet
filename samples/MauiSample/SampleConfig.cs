namespace MauiSample;

/// <summary>Where the sample sends. Debug builds (and -p:FloadLocalStub=true) talk to the local ingest stub (tools/ingest-stub).</summary>
public static class SampleConfig
{
#if FLOAD_LOCAL_STUB
	public const string WriteKey = "fload_pk_test_local";
#if ANDROID
	// The Android emulator reaches the host's localhost at 10.0.2.2.
	public static readonly Uri? Endpoint = new("http://10.0.2.2:3901");
#else
	public static readonly Uri? Endpoint = new("http://localhost:3901");
#endif
	public const FloadLogLevel LogLevel = FloadLogLevel.Debug;
#else
	// Your app's write key from Fload (Settings > App > Attribution SDK).
	public const string WriteKey = "fload_pk_REPLACE_ME";
	public static readonly Uri? Endpoint = null;   // production
	public const FloadLogLevel LogLevel = FloadLogLevel.Warning;
#endif
}
