# Classic Xamarin: the HTTP path

`Fload.Attribution` supports .NET MAUI and .NET for iOS and Android
(`net9.0-ios`, `net10.0-ios`, `net9.0-android`, `net10.0-android`). It does
not support classic Xamarin.iOS or Xamarin.Android projects (the old,
non-SDK-style `.csproj` with `Xamarin.iOS10` / `MonoAndroid` targets).

## Why there is no classic Xamarin package

- Microsoft ended support for all Xamarin SDKs on 1 May 2024. The last
  Xamarin SDKs target Xcode 15 (iOS 17) and Android API 34. No newer SDKs
  will come
  ([Xamarin support policy](https://dotnet.microsoft.com/platform/support/policy/xamarin)).
- Apple requires apps uploaded to App Store Connect since 28 April 2026 to be
  built with Xcode 26 and the iOS 26 SDK
  ([Apple](https://developer.apple.com/news/upcoming-requirements/)).
  Xamarin.iOS cannot build with Xcode 26, so a classic Xamarin.iOS app can't
  ship an update to the App Store.
- Google Play requires new apps and updates to target Android 16 (API 36)
  since 31 August 2026
  ([Google](https://developer.android.com/google/play/requirements/target-sdk)).
  Xamarin.Android stops at API 34.
- So no supported toolchain builds classic Xamarin projects. Our build
  machine has Xcode 26.4 on macOS 26, and Xamarin.iOS supports neither.
  Binding the native SDKs for Xamarin would mean building with tools that
  are out of support, for apps that can't reach the stores.

The way forward is the one Microsoft gives: move the app to .NET for iOS
and Android (Xamarin.Forms apps to .NET MAUI), then add `Fload.Attribution`.

## Until then: report installs over HTTP

[`snippets/http-path/`](../snippets/http-path) has three files to copy into
the app. They compile as C# 7.3 for netstandard2.0, like a classic Xamarin
project:

| File | What it does |
| --- | --- |
| `FloadHttpAttribution.cs` | The queue and the two requests of Fload's ingest API (`POST /attribution/v1/installs`, `POST /attribution/v1/identify`). Each request waits in a file until Fload accepts it. A retry repeats the same `requestId`, so Fload counts it once. |
| `FloadIosSource.cs` | iOS: Apple's AdServices token (`AAAttribution.GetAttributionToken`, iOS 14.3+, no IDFA, no prompt) and the app's facts. State lives in Application Support, excluded from backup. |
| `FloadAndroidSource.cs` | Android: Google Play's Install Referrer (needs the `Xamarin.Google.Android.InstallReferrer` NuGet package) and the app's facts. State lives in `NoBackupFilesDir`. |

iOS, in `AppDelegate.FinishedLaunching`:

```csharp
using Fload.HttpAttribution;

FloadIos.Start("fload_pk_…");
// After RevenueCat is configured, and again after a login:
FloadIos.SetUserIdAsync(Purchases.SharedPurchases.AppUserId);
```

Android, in `Application.OnCreate` or the launcher activity's `OnCreate`:

```csharp
using Fload.HttpAttribution;

FloadAndroid.Start(this, "fload_pk_…");
FloadAndroid.SetUserIdAsync(appUserId);

// In the activity's OnResume:
FloadAndroid.Flush();
```

How it behaves:

- The first launch reports the install once, with Apple's token or Play's
  referrer. Later launches only send what is still queued.
- `202`: done. `400` or `413`: dropped, since it can never succeed. `401`:
  kept, and retried on the next flush. `410` (key revoked): it stops for
  good. `429`, `5xx` or no network: kept, and retried on the next flush
  (each launch, and each return to the foreground).
- Apple's token lives 24 hours. An install still queued after 23 hours goes
  without it, as `token_expired`, so Fload still counts it. Start it on the
  first launch.
- It never logs the key, the token, the referrer or the user id.

This path is simpler than the native SDKs. It retries on each flush rather
than with backoff in the background, and it asks once more for a token that
fails with a network error, not for days. The wire contract is the same, and
installs from it show in Fload as SDK `fload-attribution-dotnet-http`.

## How it was tested

- `tests/HttpPath.Tests` compiles `FloadHttpAttribution.cs` as C# 7.3 for
  netstandard2.0. It checks the payloads, the user id race, retries with
  the same `requestId`, the `401` / `410` / `400` handling and the 23-hour
  cut-off. It also sends real HTTP to `tools/ingest-stub/stub.py`, which
  checks every request against Fload's wire contract.
- `samples/HttpPathCheck.iOS` and `samples/HttpPathCheck.Android` compile
  all three files as C# 7.3. They ran on the iOS 26.4 simulator and an
  Android 15 emulator against the local stub: install `202`, identify `202`.

They were compiled under .NET for iOS and Android, not under Xamarin
itself, which no longer builds on a current Mac. .NET for iOS grew out of
Xamarin.iOS and keeps its binding names, so
`AdServices.AAAttribution.GetAttributionToken`, UIKit and Foundation should
read the same in a Xamarin.iOS 14.3-or-later SDK. We could not confirm that
with a Xamarin build. The Android half uses `Xamarin.Android.InstallReferrer.Api`
from the 2.2.x Install Referrer package. If the package version you use
names its namespace differently, change the `using` line.
