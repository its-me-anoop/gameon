#!/bin/bash
# Build, install, launch and screenshot on an iOS 26 simulator.
#
#   ./Tools/run.sh                 # build, run, one screenshot
#   ./Tools/run.sh --shots 4       # screenshot every 1.5s, four times
#   ./Tools/run.sh -- -arg -arg2   # pass launch arguments to the app
#
# The live simulator panel cannot load from the Xcode 27 beta (its
# SimulatorKit.framework is absent), so verification here is headless:
# screenshots come straight from simctl.
set -euo pipefail

DEVICE="${GRAVITILE_SIM:-iPhone 17 Pro (26.5)}"
UDID=$(xcrun simctl list devices available \
  | grep -F "$DEVICE" | head -1 | sed -E 's/.*\(([0-9A-F-]{36})\).*/\1/')
if [ -z "$UDID" ]; then
  echo "No simulator matching '$DEVICE'. Set GRAVITILE_SIM to one of:" >&2
  xcrun simctl list devices available | grep -E "iPhone" >&2
  exit 1
fi

SHOTS=1
LAUNCH_ARGS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --shots) SHOTS="$2"; shift 2 ;;
    --) shift; LAUNCH_ARGS=("$@"); break ;;
    *) echo "unknown option $1" >&2; exit 1 ;;
  esac
done

OUT="${GRAVITILE_SHOTS:-build/shots}"
BUNDLE=$(grep -m1 'PRODUCT_BUNDLE_IDENTIFIER' project.yml | sed -E 's/.*: *//')
SCHEME=$(grep -m1 '^name:' project.yml | sed -E 's/name: *//')
mkdir -p "$OUT"

echo "▸ booting $DEVICE"
xcrun simctl bootstatus "$UDID" -b >/dev/null 2>&1 || xcrun simctl boot "$UDID" >/dev/null 2>&1 || true

echo "▸ building"
xcodebuild build \
  -project "$SCHEME.xcodeproj" -scheme "$SCHEME" \
  -destination "id=$UDID" -derivedDataPath build/dd \
  2>&1 | grep -E "error:|warning: .*(unused|deprecat)|BUILD" | tail -20

APP="build/dd/Build/Products/Debug-iphonesimulator/$SCHEME.app"
[ -d "$APP" ] || { echo "no app built at $APP" >&2; exit 1; }

echo "▸ launching $BUNDLE"
xcrun simctl terminate "$UDID" "$BUNDLE" >/dev/null 2>&1 || true
xcrun simctl install "$UDID" "$APP"
xcrun simctl launch "$UDID" "$BUNDLE" ${LAUNCH_ARGS[@]+"${LAUNCH_ARGS[@]}"} >/dev/null

for i in $(seq 1 "$SHOTS"); do
  sleep 1.5
  xcrun simctl io "$UDID" screenshot "$OUT/shot-$i.png" >/dev/null 2>&1
  echo "▸ $OUT/shot-$i.png"
done
