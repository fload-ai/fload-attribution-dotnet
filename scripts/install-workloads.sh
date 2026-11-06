#!/usr/bin/env bash
# Installs the ios, android and maui workloads, with the .NET for iOS release
# that matches the selected Xcode. (Each .NET for iOS release needs one Xcode
# major.minor; the newest one usually needs an Xcode newer than you have.)
set -euo pipefail
cd "$(dirname "$0")/.."
band="$(dotnet --version | sed -E 's/^([0-9]+)\.([0-9]+)\..*/\1.\2.100/')"
xcode="$(xcodebuild -version | head -1 | awk '{print $2}' | cut -d. -f1,2)"
pick() {
  curl -fsSL "https://api.nuget.org/v3-flatcontainer/microsoft.net.sdk.$1.manifest-$band/index.json" \
    | python3 -c 'import json,sys; v=[x for x in json.load(sys.stdin)["versions"] if x.startswith(sys.argv[1]+".") and "-" not in x]; print(v[-1] if v else "")' "$xcode"
}
ios="$(pick ios)"
maccatalyst="$(pick maccatalyst)"
if [ -z "$ios" ] || [ -z "$maccatalyst" ]; then
  echo "No .NET for iOS release for Xcode $xcode in the $band band. Select another Xcode." >&2
  exit 1
fi
echo "Xcode $xcode -> .NET for iOS $ios, Mac Catalyst $maccatalyst ($band)"
rollback="$(mktemp)"
printf '{ "microsoft.net.sdk.ios": "%s/%s", "microsoft.net.sdk.maccatalyst": "%s/%s" }\n' "$ios" "$band" "$maccatalyst" "$band" > "$rollback"
dotnet workload config --update-mode manifests
dotnet workload install ios android maui >/dev/null
dotnet workload update --from-rollback-file "$rollback"
dotnet workload list
