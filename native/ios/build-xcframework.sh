#!/usr/bin/env bash
# Builds FloadAttributionBridge.xcframework (device + simulator) from the
# Swift package in this folder, for the .NET for iOS binding.
set -euo pipefail
cd "$(dirname "$0")"
OUT="${1:-../../src/Fload.Attribution/Platforms/iOS/FloadAttributionBridge.xcframework}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
PRIVACY=".build/checkouts/fload-attribution-ios/Sources/FloadAttribution/Resources/PrivacyInfo.xcprivacy"

frameworks=()
for platform in "iOS" "iOS Simulator"; do
  slug="${platform// /_}"
  xcodebuild archive \
    -scheme FloadAttributionBridge \
    -destination "generic/platform=$platform" \
    -archivePath "$WORK/$slug.xcarchive" \
    -derivedDataPath "$WORK/dd-$slug" \
    -clonedSourcePackagesDirPath .build \
    SKIP_INSTALL=NO \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    DEBUG_INFORMATION_FORMAT=dwarf-with-dsym \
    -quiet
  fw="$WORK/$slug.xcarchive/Products/usr/local/lib/FloadAttributionBridge.framework"
  [ -d "$fw" ] || { echo "no framework for $platform" >&2; exit 1; }
  # Apple wants a framework's privacy manifest inside the framework.
  cp "$PRIVACY" "$fw/PrivacyInfo.xcprivacy"
  frameworks+=(-framework "$fw")
done

rm -rf "$OUT"
xcodebuild -create-xcframework "${frameworks[@]}" -output "$OUT"
echo "built $OUT"
