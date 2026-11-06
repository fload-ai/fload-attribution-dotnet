#!/usr/bin/env bash
# Installs the ios, android and maui workloads, with the .NET for iOS release
# that builds with Xcode. (Each .NET for iOS release accepts one Xcode; the
# newest one usually wants an Xcode newer than you have.)
#   scripts/install-workloads.sh                  match the selected Xcode
#   scripts/install-workloads.sh --select-xcode   CI: select the newest installed
#                                                 /Applications/Xcode_*.app that has a match
set -euo pipefail
cd "$(dirname "$0")/.."
band="$(dotnet --version | sed -E 's/^([0-9]+)\.([0-9]+)\..*/\1.\2.100/')"
xcode_of() { defaults read "$1/Contents/Info.plist" CFBundleShortVersionString | cut -d. -f1,2; }

if [ "${1:-}" = "--select-xcode" ]; then
  apps=()
  versions=()
  while read -r app; do
    apps+=("$app")
    versions+=("$(xcode_of "$app")")
  done < <(find /Applications -maxdepth 1 -name 'Xcode_[0-9]*.app' | sort -rV)
  read -r xcode ios < <(python3 -I scripts/pick-ios-workload.py "$band" "${versions[@]}") \
    || { echo "No installed Xcode (${versions[*]}) has a .NET for iOS release in the $band band." >&2; exit 1; }
  for i in "${!apps[@]}"; do
    if [ "${versions[$i]}" = "$xcode" ]; then sudo xcode-select -s "${apps[$i]}"; break; fi
  done
else
  xcode="$(xcodebuild -version | head -1 | awk '{print $2}' | cut -d. -f1,2)"
  read -r xcode ios < <(python3 -I scripts/pick-ios-workload.py "$band" "$xcode") \
    || { echo "No .NET for iOS release builds with Xcode $xcode in the $band band. Select another Xcode." >&2; exit 1; }
fi
# Mac Catalyst ships in lockstep with iOS under the same version.
echo "Xcode $xcode -> .NET for iOS and Mac Catalyst $ios ($band)"
rollback="$(mktemp)"
printf '{ "microsoft.net.sdk.ios": "%s/%s", "microsoft.net.sdk.maccatalyst": "%s/%s" }\n' "$ios" "$band" "$ios" "$band" > "$rollback"
dotnet workload config --update-mode manifests
dotnet workload install ios android maui >/dev/null
dotnet workload update --from-rollback-file "$rollback"
dotnet workload list
