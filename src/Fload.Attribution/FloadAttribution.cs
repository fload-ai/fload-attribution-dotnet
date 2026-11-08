using System;

namespace Fload.Attribution;

/// <summary>
/// Fload install attribution for .NET for iOS and Android, with or without MAUI.
/// </summary>
/// <remarks>
/// Call <see cref="Configure(string, FloadOptions?)"/> once, early in launch: in your
/// <c>AppDelegate.FinishedLaunching</c> (iOS), <c>Application.OnCreate</c> or launcher
/// activity (Android), or <c>MauiProgram.CreateMauiApp</c> (MAUI). On the app's first launch the native SDK reports
/// the install to Fload: on iOS with Apple's AdServices attribution token, on
/// Android with the Google Play Install Referrer. It keeps the request queued on
/// disk until Fload has it. No IDFA, no advertising ID, no tracking prompt.
/// On any other platform (Windows, Mac Catalyst) every call does nothing.
/// <code>
/// FloadAttribution.Configure("fload_pk_…");
/// FloadAttribution.SetUserId(Purchases.AppUserId);   // optional
/// </code>
/// </remarks>
public static partial class FloadAttribution
{
    /// <summary>This package's version.</summary>
    public const string Version = "1.1.0";

    /// <summary>
    /// The name every native configure call gives this wrapper, with <see cref="Version"/>.
    /// The native SDKs send both as <c>sdk.wrapper</c>, so Fload knows the install came
    /// through this package.
    /// </summary>
    internal const string WrapperName = "dotnet";

    /// <summary>The write key prefix Fload issues. The key is public: it can only report installs for one app.</summary>
    public const string WriteKeyPrefix = "fload_pk_";

    /// <summary>True on iOS and Android, where the native SDK runs.</summary>
    public static bool IsSupported => PlatformIsSupported;

    /// <summary>The native SDK's version, or null where it does not run.</summary>
    public static string? NativeSdkVersion => PlatformNativeSdkVersion();

    /// <summary>
    /// Starts the SDK. Call it once, early in launch. It returns at once; the native
    /// SDK does its disk and network work on its own background thread. A second call
    /// is ignored by the native SDK (the first one stands).
    /// </summary>
    /// <param name="key">The app's public write key (<c>fload_pk_…</c>) from Fload.</param>
    /// <param name="options">Where to send and how much to log. The defaults are right for production.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public static void Configure(string key, FloadOptions? options = null)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        PlatformConfigure(key, options ?? new FloadOptions());
    }

    /// <summary>
    /// Links your app's user id (for example RevenueCat's <c>AppUserID</c>) to this install.
    /// Optional. Safe to call before <see cref="Configure(string, FloadOptions?)"/> and on every
    /// launch: the native SDK sends the same id once. A null or blank id is ignored.
    /// </summary>
    public static void SetUserId(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        PlatformSetUserId(userId!);
    }

    /// <summary>
    /// This install's random reference, or null before the SDK has loaded its state
    /// (just after <see cref="Configure(string, FloadOptions?)"/>) and on unsupported platforms.
    /// Useful when asking Fload support about one install.
    /// </summary>
    public static string? InstallReference => PlatformInstallReference();
}

/// <summary>How the SDK sends and logs.</summary>
public sealed class FloadOptions
{
    /// <summary>
    /// Fload's ingest endpoint. Leave it null for production; set it only to test
    /// against your own Fload API (for example <c>http://localhost:3001</c>).
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>What the native SDK logs (iOS unified log subsystem <c>com.fload.attribution</c>, Android Logcat tag <c>FloadAttribution</c>).</summary>
    public FloadLogLevel LogLevel { get; set; } = FloadLogLevel.Warning;
}

/// <summary>The SDK's log level. It never logs the write key, Apple's token, the referrer or a user id.</summary>
public enum FloadLogLevel
{
    /// <summary>Nothing.</summary>
    None = 0,
    /// <summary>Errors only.</summary>
    Error = 1,
    /// <summary>Errors and warnings (the default).</summary>
    Warning = 2,
    /// <summary>Also informational messages.</summary>
    Info = 3,
    /// <summary>Everything.</summary>
    Debug = 4,
}
