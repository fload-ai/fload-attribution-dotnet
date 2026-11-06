// swift-tools-version:5.9
import PackageDescription

// An Objective-C facade over the Swift SDK, built as a dynamic framework so
// .NET for iOS can bind it. All logic stays in fload-attribution-ios.
let package = Package(
    name: "FloadAttributionBridge",
    platforms: [.iOS(.v15)],
    products: [
        .library(name: "FloadAttributionBridge", type: .dynamic, targets: ["FloadAttributionBridge"]),
    ],
    dependencies: [
        .package(url: "https://github.com/fload-ai/fload-attribution-ios.git", exact: "1.0.0"),
    ],
    targets: [
        .target(
            name: "FloadAttributionBridge",
            dependencies: [.product(name: "FloadAttribution", package: "fload-attribution-ios")]
        ),
    ]
)
