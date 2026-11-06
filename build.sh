#!/usr/bin/env bash
# Builds Fload.Attribution from the native SDKs and packs it.
#   ./build.sh          native iOS + Android, pack, tests
#   ./build.sh pack     pack only (natives already built)
# Needs: Xcode, the .NET SDK in global.json with the ios, android and maui
# workloads, JDK 17 and an Android SDK (ANDROID_HOME).
set -euo pipefail
cd "$(dirname "$0")"
if [ "${1:-all}" = "all" ]; then
  native/ios/build-xcframework.sh
  native/android/build-aar.sh
fi
dotnet pack src/Fload.Attribution -c Release -o artifacts
dotnet test tests/HttpPath.Tests -c Release
echo "packed: $(ls artifacts/*.nupkg)"
