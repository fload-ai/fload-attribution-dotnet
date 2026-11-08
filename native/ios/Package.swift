// swift-tools-version:5.9
import PackageDescription

// Packages the Swift SDK (fload-attribution-ios) as a dynamic framework so
// .NET for iOS can bind its Objective-C facade, FLDAttribution. No code of its own.
let package = Package(
    name: "FloadAttributionBridge",
    platforms: [.iOS(.v15)],
    products: [
        .library(name: "FloadAttributionBridge", type: .dynamic, targets: ["FloadAttributionBridge"]),
    ],
    dependencies: [
        .package(url: "https://github.com/fload-ai/fload-attribution-ios.git", exact: "1.1.0"),
    ],
    targets: [
        .target(
            name: "FloadAttributionBridge",
            dependencies: [.product(name: "FloadAttribution", package: "fload-attribution-ios")]
        ),
    ]
)
