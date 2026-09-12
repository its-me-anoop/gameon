#!/usr/bin/env bash
set -euo pipefail
PLUGIN_DIR="$(cd "$(dirname "$0")" && pwd)"
WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/clinic-macos-input-test.XXXXXX")"
trap 'rm -rf "$WORK_DIR"' EXIT
xcrun clang++ -fobjc-arc -std=c++17 -framework AppKit -framework CoreGraphics \
  "$PLUGIN_DIR/Source~/ClinicPlatformInputTests.mm" -o "$WORK_DIR/ClinicPlatformInputTests"
"$WORK_DIR/ClinicPlatformInputTests" "$PLUGIN_DIR/ClinicPlatformInput.bundle/Contents/MacOS/ClinicPlatformInput"
