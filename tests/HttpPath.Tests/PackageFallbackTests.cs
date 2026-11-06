using Fload.Attribution;
using Xunit;

namespace HttpPath.Tests;

/// <summary>The package's plain net10.0 build (Windows, Mac Catalyst): every call does nothing.</summary>
public sealed class PackageFallbackTests
{
    [Fact]
    public void Calls_do_nothing_off_iOS_and_Android()
    {
        Assert.False(FloadAttribution.IsSupported);
        FloadAttribution.Configure("fload_pk_test_local", new FloadOptions { LogLevel = FloadLogLevel.Debug });
        FloadAttribution.SetUserId("user_123");
        FloadAttribution.SetUserId(null);
        Assert.Null(FloadAttribution.InstallReference);
        Assert.Null(FloadAttribution.NativeSdkVersion);
    }

    [Fact]
    public void A_null_key_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => FloadAttribution.Configure(null!));
    }
}
