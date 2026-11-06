using Foundation;
using ObjCRuntime;

namespace Fload.Attribution.Native
{
    // FLDAttribution in native/ios/Sources/FloadAttributionBridge/FLDAttribution.swift.
    [Internal]
    [BaseType(typeof(NSObject), Name = "FLDAttribution")]
    interface FLDAttribution
    {
        [Static]
        [Export("sdkVersion")]
        string SdkVersion { get; }

        [Static]
        [Export("configureWithKey:endpoint:logLevel:")]
        void Configure(string key, [NullAllowed] string endpoint, nint logLevel);

        [Static]
        [Export("setUserId:")]
        void SetUserId(string userId);

        [Static]
        [NullAllowed]
        [Export("installReference")]
        string InstallReference { get; }
    }
}
