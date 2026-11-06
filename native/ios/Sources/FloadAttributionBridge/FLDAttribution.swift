import Foundation
import FloadAttribution

/// The Objective-C face of `Fload`, for .NET for iOS (and any other caller
/// that can only see the Objective-C runtime). Each call forwards to the
/// Swift SDK unchanged.
@objc(FLDAttribution)
public final class FLDAttribution: NSObject {
    /// The native SDK's version.
    @objc public static var sdkVersion: String { Fload.sdkVersion }

    /// `Fload.configure(key:)` with the default endpoint and log level.
    @objc(configureWithKey:)
    public static func configure(key: String) {
        Fload.configure(key: key)
    }

    /// `Fload.configure(key:options:)`. A nil or unparsable endpoint keeps
    /// Fload's production endpoint. `logLevel` is 0 none, 1 error, 2 warning,
    /// 3 info, 4 debug; anything else is warning.
    @objc(configureWithKey:endpoint:logLevel:)
    public static func configure(key: String, endpoint: String?, logLevel: Int) {
        var options = FloadOptions()
        if let endpoint, let url = URL(string: endpoint), url.scheme != nil {
            options.endpoint = url
        }
        options.logLevel = FloadLogLevel(rawValue: logLevel) ?? .warning
        Fload.configure(key: key, options: options)
    }

    /// `Fload.setUserId(_:)`.
    @objc(setUserId:)
    public static func setUserId(_ userId: String) {
        Fload.setUserId(userId)
    }

    /// `Fload.installReference`: nil before configure.
    @objc public static var installReference: String? { Fload.installReference }
}
