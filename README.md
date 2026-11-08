# Fload Attribution for .NET

For **.NET for iOS and Android, with or without MAUI.** Counts your app's
installs in Fload and attributes them: on iOS to Apple Ads through Apple's
AdServices API, on Android with Google Play's Install Referrer. No IDFA, no
advertising ID, no App Tracking Transparency prompt.

The package has no MAUI dependency. It works in a plain .NET for iOS or
.NET for Android app (where Xamarin apps moved after Microsoft ended Xamarin
support) and in .NET MAUI. It wraps Fload's native SDKs,
[fload-attribution-ios](https://github.com/fload-ai/fload-attribution-ios)
and [fload-attribution-android](https://github.com/fload-ai/fload-attribution-android),
so the behaviour is theirs: the same queue, retries and privacy rules.

| Target | Minimum | Native SDK |
| --- | --- | --- |
| `net10.0-ios`, `net9.0-ios` | iOS 15 | fload-attribution-ios 1.1.0 |
| `net10.0-android`, `net9.0-android` | Android 7.0 (API 24) | fload-attribution-android 1.1.0 |
| `net10.0`, `net9.0` (Windows, Mac Catalyst, tests) | | none: every call does nothing |

Classic Xamarin.iOS and Xamarin.Android are not supported (see
[Classic Xamarin](#classic-xamarin)).

## Install

```sh
dotnet add package Fload.Attribution
```

## Use

### .NET for iOS

Call `Configure` once, early in `FinishedLaunching`:

```csharp
using Fload.Attribution;
using Foundation;
using UIKit;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        FloadAttribution.Configure("fload_pk_…");

        // Optional: link your user id (e.g. RevenueCat's AppUserID) to the install.
        // Call it again after a login.
        FloadAttribution.SetUserId(appUserId);

        // ... your window and root view controller
        return true;
    }
}
```

### .NET for Android

Call `Configure` once in your `Application.OnCreate` (or your launcher
activity's `OnCreate`):

```csharp
using Android.App;
using Android.Runtime;
using Fload.Attribution;

[Application]
public class App : Application
{
    public App(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

    public override void OnCreate()
    {
        base.OnCreate();
        FloadAttribution.Configure(this, "fload_pk_…");
        FloadAttribution.SetUserId(appUserId);   // optional
    }
}
```

### .NET MAUI

Once, in `MauiProgram.CreateMauiApp`. It is the same call; on Android it uses
`Android.App.Application.Context`:

```csharp
FloadAttribution.Configure("fload_pk_…");
```

The key is your app's public write key from Fload. It can only report
installs for that one app, so it is safe to ship in the binary.

| Call | What it does |
| --- | --- |
| `FloadAttribution.Configure(key, options?)` | Starts the SDK. Returns at once; the native SDK works on its own background thread. The first call stands. On Android, `Configure(context, key, options?)` takes an explicit context. |
| `FloadAttribution.SetUserId(id)` | Links your user id to this install. Safe before `Configure` and on every launch; the same id is sent once. Null or blank is ignored. |
| `FloadAttribution.InstallReference` | This install's random reference, for Fload support. Null until the SDK has loaded its state. |
| `FloadAttribution.IsSupported` | True on iOS and Android. |
| `FloadAttribution.NativeSdkVersion` | The native SDK's version. |

Samples: [`samples/NetiOSSample`](samples/NetiOSSample) and
[`samples/NetAndroidSample`](samples/NetAndroidSample) (no MAUI), and
[`samples/MauiSample`](samples/MauiSample).

## What it does

- On the first launch with the SDK, it makes a random install reference and
  reports the install once. On iOS: bundle id, app version and build, OS
  version, device model, locale, first-launch time and Apple's AdServices
  attribution token, which Fload swaps with Apple server side. On Android:
  package name, version, Android version, device model, locale and what
  Play's Install Referrer API returns.
- `SetUserId` sends your user id with the install, or afterwards in a
  separate request. Each id is sent once.
- Everything waits in a small file queue, excluded from backups, until Fload
  accepts it. It survives relaunches and network loss, retries with
  exponential backoff, and follows `Retry-After`.
- A key Fload does not know (HTTP 401) keeps the queue and retries daily. A
  revoked key (HTTP 410) stops the SDK until the app ships a new key.
- It never logs the key, the token, the referrer or the user id.

The native READMEs have the details.

## Privacy

iOS: the native SDK's `PrivacyInfo.xcprivacy` ships inside the bound
framework: no tracking, no tracking domains, no required-reason APIs.
Collected, linked to the user, not used for tracking, for analytics: a device
ID (the random install reference), advertising data (the AdServices token)
and a user ID (only when you call `SetUserId`). Declare the same in your App
Store privacy answers.

Android: see "Google Play Data safety" in the
[Android README](https://github.com/fload-ai/fload-attribution-android#google-play-data-safety).
The SDK adds the `INTERNET` and `ACCESS_NETWORK_STATE` permissions.

## Testing against your own API

```csharp
FloadAttribution.Configure(key, new FloadOptions
{
    Endpoint = new Uri("http://localhost:3901"),   // Android emulator: http://10.0.2.2:3901
    LogLevel = FloadLogLevel.Debug,
});
```

A plain `http` endpoint also needs `NSAllowsLocalNetworking` (iOS) or a
network security config that allows cleartext to that host (Android). The
sample does both in Debug builds only.

`tools/ingest-stub/stub.py` is a local stand-in for Fload's ingest API: it
checks every request against the wire contract and logs it. See
[CONTRIBUTING.md](CONTRIBUTING.md) for running the sample on a simulator
and an emulator against it.

## Classic Xamarin

Classic Xamarin.iOS and Xamarin.Android projects can't use this package:
Microsoft ended Xamarin support on 1 May 2024, and no supported toolchain
builds them any more. They can't ship to the stores either: Xamarin.iOS
can't build with the Xcode the App Store requires now, and Xamarin.Android
can't target the Android API level Google Play requires. Move the app to
.NET for iOS and Android (Microsoft's upgrade assistant does most of it) and
use this package.

Until then, [`docs/xamarin-http.md`](docs/xamarin-http.md) shows how a
classic Xamarin app reports installs itself over Fload's HTTP ingest API.

## License

MIT. See [LICENSE](LICENSE).
