#!/usr/bin/env bash
# Builds the Android SDK's release AAR from a tag of fload-attribution-android
# and copies it next to the .NET binding.
# Usage: ./build-aar.sh [tag]   (needs JDK 17 and an Android SDK in ANDROID_HOME)
set -euo pipefail
cd "$(dirname "$0")"
TAG="${1:-$(cat VERSION)}"
OUT="../../src/Fload.Attribution/Platforms/Android/fload-attribution-android.aar"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
git clone -q --depth 1 --branch "$TAG" https://github.com/fload-ai/fload-attribution-android.git "$WORK/src"
cd "$WORK/src"
[ -n "${ANDROID_HOME:-}" ] && echo "sdk.dir=$ANDROID_HOME" > local.properties
./gradlew -q :attribution:assembleRelease -Pversion="$TAG"
cd - >/dev/null
cp "$WORK/src/attribution/build/outputs/aar/attribution-release.aar" "$OUT"
echo "built $OUT from fload-attribution-android $TAG"
