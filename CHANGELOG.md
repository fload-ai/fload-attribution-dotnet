# Changelog

## 1.0.1

- First release of `Fload.Attribution`, for .NET for iOS and Android with or
  without MAUI (net9.0 and net10.0), over fload-attribution-ios 1.0.2 and
  fload-attribution-android 1.0.2. `Configure`, `SetUserId`,
  `InstallReference`. Every other platform builds and does nothing.
- Installs report the native SDKs' own version, `1.0.2`.
- iOS binds the Swift SDK's own Objective-C facade, `FLDAttribution`. The
  package no longer carries an Objective-C bridge of its own.
- The HTTP path for classic Xamarin apps (`snippets/http-path`,
  `docs/xamarin-http.md`) reports `fload-attribution-dotnet-http` `1.0.1`.
- Samples for plain .NET for iOS and Android (`samples/NetiOSSample`,
  `samples/NetAndroidSample`) next to the MAUI one.

1.0.0 was tagged by mistake on native SDKs that still reported `0.1.0`. It
was withdrawn before it reached nuget.org and never shipped.
