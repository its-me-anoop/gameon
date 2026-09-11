#!/usr/bin/env bash
set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "$0")/Native~" && pwd)"
CHECK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/orchard-unity-bridge.XXXXXX")"
SDK_PATH="$(xcrun --sdk iphonesimulator --show-sdk-path)"
TARGET=arm64-apple-ios18.0-simulator
FRAMEWORK_DIR="$CHECK_DIR/UnityFramework.framework"
mkdir -p "$FRAMEWORK_DIR/Headers"

xcrun swiftc -emit-module -emit-objc-header \
  -emit-objc-header-path "$FRAMEWORK_DIR/Headers/UnityFramework-Swift.h" \
  -emit-module-path "$CHECK_DIR/UnityFramework.swiftmodule" \
  -module-name UnityFramework -swift-version 6 -sdk "$SDK_PATH" -target "$TARGET" \
  "$SOURCE_DIR/OrchardStoreService.swift" "$SOURCE_DIR/OrchardGameCenterService.swift" "$SOURCE_DIR/OrchardAppleBridge.swift"
xcrun clang++ -fobjc-arc -fmodules -std=c++17 -isysroot "$SDK_PATH" -target "$TARGET" \
  -F "$CHECK_DIR" -c "$SOURCE_DIR/OrchardApplePlugin.mm" -o "$CHECK_DIR/OrchardApplePlugin.o"

# Test-only callback symbol. The real Unity player supplies this at app linkage.
cat > "$CHECK_DIR/UnityMessageStub.cpp" <<'CPP'
extern "C" void UnitySendMessage(const char *, const char *, const char *) {}
CPP
xcrun clang++ -isysroot "$SDK_PATH" -target "$TARGET" \
  -c "$CHECK_DIR/UnityMessageStub.cpp" -o "$CHECK_DIR/UnityMessageStub.o"
SDKROOT="$SDK_PATH" xcrun swiftc -emit-library -module-name UnityFramework -swift-version 6 \
  -sdk "$SDK_PATH" -target "$TARGET" -Xlinker -syslibroot -Xlinker "$SDK_PATH" -lc++ \
  "$SOURCE_DIR/OrchardStoreService.swift" "$SOURCE_DIR/OrchardGameCenterService.swift" "$SOURCE_DIR/OrchardAppleBridge.swift" \
  "$CHECK_DIR/OrchardApplePlugin.o" "$CHECK_DIR/UnityMessageStub.o" -o "$CHECK_DIR/OrchardAppleServices.dylib"
xcrun nm -gU "$CHECK_DIR/OrchardAppleServices.dylib" | awk '/_OO_/ { print $NF }'
echo "Native bridge compilation and linkage passed. Artifacts: $CHECK_DIR"
