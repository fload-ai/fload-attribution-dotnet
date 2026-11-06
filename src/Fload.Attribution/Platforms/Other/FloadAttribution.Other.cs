namespace Fload.Attribution;

public static partial class FloadAttribution
{
    private const bool PlatformIsSupported = false;

    private static string? PlatformNativeSdkVersion() => null;

    private static void PlatformConfigure(string key, FloadOptions options) { }

    private static void PlatformSetUserId(string userId) { }

    private static string? PlatformInstallReference() => null;
}
