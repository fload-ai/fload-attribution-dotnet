#!/usr/bin/env bash
# Installs the ios, android and maui workloads, with the .NET for iOS release
# that matches Xcode. (Each .NET for iOS release needs one Xcode major.minor;
# the newest one usually needs an Xcode newer than you have.)
#   scripts/install-workloads.sh                  match the selected Xcode
#   scripts/install-workloads.sh --select-xcode   CI: select the newest installed
#                                                 /Applications/Xcode_*.app that has a match
set -euo pipefail
cd "$(dirname "$0")/.."
band="$(dotnet --version | sed -E 's/^([0-9]+)\.([0-9]+)\..*/\1.\2.100/')"
manifest() {
  curl -fsSL "https://api.nuget.org/v3-flatcontainer/microsoft.net.sdk.$1.manifest-$band/index.json"
}
ios_versions="$(manifest ios)"
maccatalyst_versions="$(manifest maccatalyst)"
pick() {  # pick <versions json> <xcode major.minor>
  python3 -c 'import json,sys; v=[x for x in json.loads(sys.argv[1])["versions"] if x.startswith(sys.argv[2]+".") and "-" not in x]; print(v[-1] if v else "")' "$1" "$2"
}

if [ "${1:-}" = "--select-xcode" ]; then
  selected=""
  while read -r app; do
    version="$(basename "$app" .app | sed -E 's/^Xcode_([0-9]+\.[0-9]+).*/\1/')"
    if [ -n "$(pick "$ios_versions" "$version")" ]; then selected="$app"; break; fi
  done < <(find /Applications -maxdepth 1 -name 'Xcode_[0-9]*.app' | sort -rV)
  [ -n "$selected" ] || { echo "No installed Xcode has a .NET for iOS release in the $band band." >&2; exit 1; }
  sudo xcode-select -s "$selected"
fi

xcode="$(xcodebuild -version | head -1 | awk '{print $2}' | cut -d. -f1,2)"
ios="$(pick "$ios_versions" "$xcode")"
maccatalyst="$(pick "$maccatalyst_versions" "$xcode")"
if [ -z "$ios" ] || [ -z "$maccatalyst" ]; then
  echo "No .NET for iOS release for Xcode $xcode in the $band band. Select another Xcode (or pass --select-xcode)." >&2
  exit 1
fi
echo "Xcode $xcode -> .NET for iOS $ios, Mac Catalyst $maccatalyst ($band)"
rollback="$(mktemp)"
printf '{ "microsoft.net.sdk.ios": "%s/%s", "microsoft.net.sdk.maccatalyst": "%s/%s" }\n' "$ios" "$band" "$maccatalyst" "$band" > "$rollback"
dotnet workload config --update-mode manifests
dotnet workload install ios android maui >/dev/null
dotnet workload update --from-rollback-file "$rollback"
dotnet workload list
