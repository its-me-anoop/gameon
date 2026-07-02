#!/bin/bash
# Regenerates the Xcode project. XcodeGen doesn't emit the StoreKit
# configuration reference into the scheme's TestAction, so we patch it in —
# StoreKit unit tests need the config active during `xcodebuild test`.
set -euo pipefail
cd "$(dirname "$0")/.."

xcodegen generate

SCHEME="Gravitile.xcodeproj/xcshareddata/xcschemes/Gravitile.xcscheme"
python3 - "$SCHEME" << 'EOF'
import re, sys
path = sys.argv[1]
with open(path) as f:
    content = f.read()
ref = '''      <StoreKitConfigurationFileReference
         identifier = "../../Gravitile/Gravitile.storekit">
      </StoreKitConfigurationFileReference>
'''
# Insert into TestAction if not already present there.
test_action = re.search(r'<TestAction[^>]*>', content)
if test_action and 'StoreKitConfigurationFileReference' not in content[test_action.end():content.find('</TestAction>')]:
    insert_at = test_action.end()
    content = content[:insert_at] + '\n' + ref + content[insert_at:]
    with open(path, 'w') as f:
        f.write(content)
    print('Patched StoreKit configuration into TestAction')
else:
    print('TestAction already configured')
EOF
