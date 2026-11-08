using Fload.Attribution.Native;

namespace Fload.Attribution;

public static partial class FloadAttribution
{
    private const bool PlatformIsSupported = true;

    private static string? PlatformNativeSdkVersion() => FLDAttribution.SdkVersion;

    private static void PlatformConfigure(string key, FloadOptions options) =>
        FLDAttribution.Configure(key, options.Endpoint?.AbsoluteUri.TrimEnd('/'), (nint)(int)options.LogLevel, WrapperName, Version);

    private static void PlatformSetUserId(string userId) => FLDAttribution.SetUserId(userId);

    private static string? PlatformInstallReference() => FLDAttribution.InstallReference;
}
