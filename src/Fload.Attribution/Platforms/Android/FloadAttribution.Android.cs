using System;
using Android.Content;
using NativeFload = Com.Fload.Attribution.Fload;
using NativeOptions = Com.Fload.Attribution.FloadOptions;
using NativeLogLevel = Com.Fload.Attribution.FloadLogLevel;
using NativeWrapper = Com.Fload.Attribution.FloadWrapper;

namespace Fload.Attribution;

public static partial class FloadAttribution
{
    private const bool PlatformIsSupported = true;

    /// <summary>
    /// Starts the SDK with an explicit context. <see cref="Configure(string, FloadOptions?)"/>
    /// uses <c>Android.App.Application.Context</c>, which is right for .NET MAUI.
    /// </summary>
    /// <param name="context">Any context; the native SDK keeps only the application context.</param>
    /// <param name="key">The app's public write key (<c>fload_pk_…</c>) from Fload.</param>
    /// <param name="options">Where to send and how much to log.</param>
    public static void Configure(Context context, string key, FloadOptions? options = null)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (key is null) throw new ArgumentNullException(nameof(key));
        NativeFload.Configure(context, key, ToNative(options ?? new FloadOptions()));
    }

    private static string? PlatformNativeSdkVersion() => NativeFload.SdkVersion;

    private static void PlatformConfigure(string key, FloadOptions options) =>
        NativeFload.Configure(Android.App.Application.Context, key, ToNative(options));

    private static void PlatformSetUserId(string userId) => NativeFload.SetUserId(userId);

    private static string? PlatformInstallReference() => NativeFload.InstallReference;

    private static NativeOptions ToNative(FloadOptions options)
    {
        var level = options.LogLevel switch
        {
            FloadLogLevel.None => NativeLogLevel.None!,
            FloadLogLevel.Error => NativeLogLevel.Error!,
            FloadLogLevel.Info => NativeLogLevel.Info!,
            FloadLogLevel.Debug => NativeLogLevel.Debug!,
            _ => NativeLogLevel.Warning!,
        };
        var endpoint = options.Endpoint is null ? "https://ingest.fload.com" : options.Endpoint.AbsoluteUri.TrimEnd('/');
        // FloadOptions(String, FloadLogLevel, FloadWrapper): the native SDK sends it as sdk.wrapper.
        return new NativeOptions(endpoint, level, new NativeWrapper(WrapperName, Version));
    }
}
