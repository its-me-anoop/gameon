#!/usr/bin/env bash
set -euo pipefail
PLUGIN_DIR="$(cd "$(dirname "$0")" && pwd)"
WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/clinic-macos-plugin.XXXXXX")"
trap 'rm -rf "$WORK_DIR"' EXIT
SDK_PATH="$(xcrun --sdk macosx --show-sdk-path)"
BUNDLE_PATH="$PLUGIN_DIR/ClinicPlatformInput.bundle"
for architecture in arm64 x86_64; do
  xcrun clang++ -bundle -fobjc-arc -fblocks -std=c++17 -fvisibility=hidden \
    -arch "$architecture" -mmacosx-version-min=11.0 -isysroot "$SDK_PATH" \
    -framework AppKit "$PLUGIN_DIR/Source~/ClinicPlatformInput.mm" \
    -o "$WORK_DIR/ClinicPlatformInput-$architecture"
done
mkdir -p "$BUNDLE_PATH/Contents/MacOS"
xcrun lipo -create "$WORK_DIR/ClinicPlatformInput-arm64" "$WORK_DIR/ClinicPlatformInput-x86_64" \
  -output "$BUNDLE_PATH/Contents/MacOS/ClinicPlatformInput"
cat > "$BUNDLE_PATH/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>ClinicPlatformInput</string>
<key>CFBundleIdentifier</key><string>com.flutterly.clinic.platform-input</string>
<key>CFBundleName</key><string>ClinicPlatformInput</string>
<key>CFBundlePackageType</key><string>BNDL</string>
<key>CFBundleVersion</key><string>1</string>
<key>CFBundleShortVersionString</key><string>1.0</string>
<key>LSMinimumSystemVersion</key><string>11.0</string>
</dict></plist>
PLIST
codesign --force --sign - --timestamp=none "$BUNDLE_PATH"
codesign --verify --strict "$BUNDLE_PATH"
xcrun lipo -archs "$BUNDLE_PATH/Contents/MacOS/ClinicPlatformInput"
xcrun nm -gU "$BUNDLE_PATH/Contents/MacOS/ClinicPlatformInput" | awk '/_Clinic_/ {print $NF}'
printf '%s\n' "macOS input bundle built and verified. Restart Unity if replacing an already-loaded native plugin."
