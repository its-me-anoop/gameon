#!/usr/bin/env bash
set -euo pipefail

[[ "$(uname -m)" == arm64 ]]
XCODE_PATH="$(python3 - <<'PY'
import re
from pathlib import Path
candidates = [path for path in Path('/Applications').glob('Xcode_26*.app') if not re.search(r'beta|rc|release.candidate', path.name, re.I)]
if not candidates:
    raise SystemExit('A released Xcode 26 installation is required.')
print(max(candidates, key=lambda path: tuple(map(int, re.findall(r'\d+', path.name)))))
PY
)"
sudo xcode-select -s "$XCODE_PATH"
if [[ -n "${GITHUB_ENV:-}" ]]; then
    echo "DEVELOPER_DIR=$XCODE_PATH/Contents/Developer" >> "$GITHUB_ENV"
fi
xcodebuild -version
sw_vers
OS_BUILD="$(sw_vers -buildVersion)"
XCODE_BUILD="$(xcodebuild -version | awk '/Build version/ { print $3 }')"
if [[ "$OS_BUILD" =~ [a-z]$ || "$XCODE_BUILD" =~ [a-z]$ ]]; then
    echo 'Prerelease build stamp; this pipeline deliberately requires released Xcode 26 and macOS 26.'
    exit 1
fi
