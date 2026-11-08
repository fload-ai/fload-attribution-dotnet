using Foundation;
using ObjCRuntime;

namespace Fload.Attribution.Native
{
    // FLDAttribution: fload-attribution-ios's own Objective-C facade, packaged by native/ios.
    // The wrapper selector is 1.1.0+.
    [Internal]
    [BaseType(typeof(NSObject), Name = "FLDAttribution")]
    interface FLDAttribution
    {
        [Static]
        [Export("sdkVersion")]
        string SdkVersion { get; }

        // A nil or blank wrapper name or version sends no sdk.wrapper.
        [Static]
        [Export("configureWithKey:endpoint:logLevel:wrapperName:wrapperVersion:")]
        void Configure(string key, [NullAllowed] string endpoint, nint logLevel, [NullAllowed] string wrapperName, [NullAllowed] string wrapperVersion);

        [Static]
        [Export("setUserId:")]
        void SetUserId(string userId);

        [Static]
        [NullAllowed]
        [Export("installReference")]
        string InstallReference { get; }
    }
}
