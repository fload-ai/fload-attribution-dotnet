# Contributing

Written from Apple's, Google's and Microsoft's public documentation and
Fload's own native SDKs. Do not paste code from other attribution SDKs.

## Layout

```
native/ios/       Swift package: FLDAttribution, an Objective-C facade over
                  fload-attribution-ios, built into FloadAttributionBridge.xcframework
native/android/   build-aar.sh: builds the AAR from a fload-attribution-android tag
src/Fload.Attribution/
                  the NuGet package: the public C# API, the iOS binding
                  (Platforms/iOS/ApiDefinition.cs), the Android binding
                  (Platforms/Android/Metadata.xml) and the do-nothing fallback
samples/MauiSample/        .NET MAUI sample (iOS, Android, Mac Catalyst)
samples/HttpPathCheck.*/   run the classic-Xamarin HTTP path on a simulator and an emulator
snippets/http-path/        the HTTP path for classic Xamarin (docs/xamarin-http.md)
tests/HttpPath.Tests/      unit and contract tests (HTTP path, fallback)
tools/ingest-stub/         a local stand-in for Fload's ingest API, for tests only
```

## Build

Needs Xcode 26, the .NET SDK in `global.json` with the `ios`, `android` and
`maui` workloads, JDK 17 and an Android SDK in `ANDROID_HOME`.

```sh
./build.sh
```

That builds the xcframework (`native/ios/build-xcframework.sh`) and the AAR
(`native/android/build-aar.sh`, from the tag in `native/android/VERSION`),
packs `artifacts/Fload.Attribution.<version>.nupkg` and runs the tests.

Each .NET for iOS release builds with one Xcode version, and the newest
release usually wants an Xcode newer than yours ("This version of .NET for
iOS requires Xcode X"). `scripts/install-workloads.sh` installs the
workloads with the release that matches your selected Xcode. It reads each
release's required Xcode from its pack on nuget.org
(`scripts/pick-ios-workload.py`).

## Run the sample against the local stub

Never against production. Debug builds of the sample send to the stub.

```sh
python3 tools/ingest-stub/stub.py --port 3901 --key fload_pk_test_local
```

iOS simulator:

```sh
cd samples/MauiSample
dotnet build -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
xcrun simctl install booted bin/Debug/net10.0-ios/iossimulator-arm64/MauiSample.app
SIMCTL_CHILD_FLOAD_SAMPLE_USER_ID=user_1 xcrun simctl launch booted com.fload.samples.maui
curl -s localhost:3901/__stub__/requests
```

Android emulator (it reaches the host at `10.0.2.2`):

```sh
dotnet build -f net10.0-android -p:EmbedAssembliesIntoApk=true
adb install -r bin/Debug/net10.0-android/com.fload.samples.maui-Signed.apk
adb shell am start -n "$(adb shell cmd package resolve-activity --brief com.fload.samples.maui | tail -1)" --es fload_sample_user_id user_1
curl -s localhost:3901/__stub__/requests
```

Expect one `installs` request and one `identify` request, both `202`. On the
simulator, `appleAds.status` is `unsupported_os` (the simulator has no
AdServices token). On an emulator with Google Play, `playReferrer.status` is
`ok` with the organic referrer. A Release build can send to the stub too, with
`-p:FloadLocalStub=true`.

## Releases

One version for the package, in `src/Fload.Attribution/Fload.Attribution.csproj`
and `FloadAttribution.Version`. Tag it (`1.0.0`). The release workflow builds
the natives from source, packs and attaches the `.nupkg` to the GitHub
release. It pushes to nuget.org only when the repository variable
`NUGET_ENABLED` is `true`, from the protected `nuget` environment.
