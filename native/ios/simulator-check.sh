#!/usr/bin/env bash
# The iOS binding's configure call against the real native SDK, without .NET:
#   1. builds FloadAttributionBridge.xcframework (build-xcframework.sh, from the
#      fload-attribution-ios version pinned in Package.swift);
#   2. links simulator-check/main.m against its simulator slice. main.m calls
#      FLDAttribution with the configure selector that
#      src/Fload.Attribution/Platforms/iOS/ApiDefinition.cs binds, passing the
#      wrapper name (FloadAttribution.WrapperName) and version
#      (FloadAttribution.Version) the package passes;
#   3. runs it on a simulator against tools/ingest-stub/stub.py and checks that
#      the install arrived with sdk.wrapper {"dotnet", <the csproj Version>}.
# Needs Xcode with an iOS simulator runtime, python3 and curl.
# Usage: native/ios/simulator-check.sh   (FLOAD_SIM_UDID=… to pick a simulator)
set -euo pipefail
cd "$(dirname "$0")"
root="$(cd ../.. && pwd)"
lib="$root/src/Fload.Attribution"
work="$(mktemp -d)"
stub_pid=""
booted_here=0
udid=""
finish() {
  [ -n "$stub_pid" ] && { kill "$stub_pid" 2>/dev/null || true; wait "$stub_pid" 2>/dev/null || true; }
  [ "$booted_here" = 1 ] && { xcrun simctl shutdown "$udid" 2>/dev/null || true; }
  rm -rf "$work"
}
trap finish EXIT
fail() { echo "FAIL $*"; exit 1; }

# What the package passes, read from its sources.
selector="$(sed -n 's/.*Export("\(configureWithKey:[^"]*wrapperName:[^"]*\)").*/\1/p' "$lib/Platforms/iOS/ApiDefinition.cs")"
[ -n "$selector" ] || fail "ApiDefinition.cs binds no configure selector that takes a wrapper"
name="$(sed -n 's/.*const string WrapperName = "\([^"]*\)".*/\1/p' "$lib/FloadAttribution.cs")"
[ -n "$name" ] || fail "FloadAttribution.cs has no WrapperName"
const_version="$(sed -n 's/.*const string Version = "\([^"]*\)".*/\1/p' "$lib/FloadAttribution.cs")"
package_version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$lib/Fload.Attribution.csproj")"
echo "selector +$selector, wrapper $name $const_version (csproj Version $package_version)"

echo "== xcframework"
./build-xcframework.sh "$work/FloadAttributionBridge.xcframework" > "$work/xcframework.log" 2>&1 \
  || { tail -40 "$work/xcframework.log"; fail "build-xcframework.sh"; }
slice="$(ls -d "$work"/FloadAttributionBridge.xcframework/*simulator* | head -1)"
xcrun -sdk iphonesimulator clang -target arm64-apple-ios15.0-simulator -fobjc-arc -Werror -Wall \
  -F "$slice" -framework FloadAttributionBridge -framework Foundation -Wl,-rpath,"$slice" \
  simulator-check/main.m -o "$work/check"

echo "== simulator"
if [ -n "${FLOAD_SIM_UDID:-}" ]; then
  udid="$FLOAD_SIM_UDID"
else
  udid="$(xcrun simctl list devices available -j | python3 -I -c '
import json, sys
d = json.load(sys.stdin)["devices"]
for k in sorted((k for k in d if "iOS" in k), reverse=True):
    p = [x for x in d[k] if x["name"].startswith("iPhone")]
    if p:
        print(p[0]["udid"]); break')"
fi
[ -n "$udid" ] || fail "no iPhone simulator available"
if ! xcrun simctl list devices | grep -q "$udid) (Booted)"; then
  xcrun simctl boot "$udid"
  booted_here=1
fi
xcrun simctl bootstatus "$udid" -b >/dev/null

port=$((39000 + $$ % 1000))
python3 -I "$root/tools/ingest-stub/stub.py" --port "$port" --key fload_pk_test_local > "$work/stub.log" 2>&1 &
stub_pid=$!
for _ in $(seq 20); do curl -sf "http://127.0.0.1:$port/__stub__/requests" >/dev/null && break; sleep 0.5; done

mkdir -p "$work/home"  # a throwaway home, so every run is a first launch
SIMCTL_CHILD_CFFIXED_USER_HOME="$work/home" \
  xcrun simctl spawn "$udid" "$work/check" "http://127.0.0.1:$port" "$selector" "$name" "$const_version" dotnet-check-user

curl -sf "http://127.0.0.1:$port/__stub__/requests" > "$work/requests.json"
python3 -I - "$work/requests.json" "$package_version" <<'PY'
import json, sys
requests = json.load(open(sys.argv[1]))
version = sys.argv[2]
installs = [r for r in requests if r["path"] == "/attribution/v1/installs"]
identifies = [r for r in requests if r["path"] == "/attribution/v1/identify"]
install = installs[0] if installs else {}
sdk = install.get("body", {}).get("sdk", {})
failed = 0
def check(ok, name):
    global failed
    print(("ok   " if ok else "FAIL ") + name)
    failed += 0 if ok else 1
check(len(installs) == 1, f"one install sent (got {len(installs)})")
check(install.get("status") == 202, "the stub accepted it (contract valid)")
check(sdk.get("name") == "fload-attribution-ios", "sent by the Swift SDK")
check(sdk.get("wrapper") == {"name": "dotnet", "version": version}, f'sdk.wrapper is {{"dotnet", "{version}"}}')
check(any(r["status"] == 202 and r["body"].get("appUserId") == "dotnet-check-user" for r in identifies + installs), "the user id arrived")
print("sdk: " + json.dumps(sdk))
sys.exit(1 if failed else 0)
PY
