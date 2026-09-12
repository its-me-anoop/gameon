# Little Lifeline 3.0 release

The candidate is the Unity game in `Unity/OrbitOrchard`, version **3.0 (14)**, bundle `com.flutterly.gravitile`. Its shipped scene is `Assets/LittleLifeline/Scenes/Lifeline.unity`. Earlier Orbit Orchard and native SwiftUI candidates are historical. The internal project, bridge and ZIP filenames retain their original names for compatibility; the product and scene checks identify Little Lifeline explicitly.

## Candidate content

The first release contains Willowbank, Copperhill and Seabrook; four carriage slots; consultation, diagnostics and recovery; and up to six crew members. Each town has two restoration projects. Current-stop community visits continue after projects finish. The same simulation calculates active progress and up to 24 hours of offline catchup, with bounded arrivals and no accumulating missed-day penalty. Save files pair full simulation state with accounted UTC time, use atomic replacement and retain a backup. Progress is device-local.

The Weekly Call gives everyone the same deterministic arrival pattern, starting crew and budget for a fixed four-minute scenario. Pausing and retries are free. Ranking uses completed visits first, then shorter waiting for those completed visits. Home progression, paid finishes and time spent idling do not add to the challenge score. This is a client-simulated leaderboard, not server-authoritative anti-cheat; there is no cash prize or claim of verified competition.

The existing Plus non-consumable (`com.flutterly.gravitile.plus`) becomes the **Founder’s Carriage Collection**. Sunrise, Coastal and Heritage finishes are cosmetic. Verified previous Plus purchases retain access. The three existing consumable tips grant no entitlement. Campaign and challenge content remain free. There are no ads, subscriptions, paid speed-ups, energy purchases or charity claims.

## Current release status — 12 September 2026

**Little Lifeline 3.0 (14) is available in TestFlight to the existing Internal group.** The shipped source is `0c82dbf4b13db31812662f2d2b396e416066027e`, on `codex/orbit-orchard`.

The final Unity suite passed **148/148** with zero failures or skips. The exact player built and ran on the iPhone 17 Pro / iOS 26.5 simulator. Actual input and checksummed saves established building, staff assignments, relaunch/offline progress, and a complete four-minute weekly practice round. Root inspected the final icon-led overview and all three care departments. [Verification and beta limitations](qa/little-lifeline.md).

| Artifact | Exact identity |
| --- | --- |
| Device SDK export | `build/unity-transfer-aisle/orbit-orchard-ios.zip`, 206,881,027 bytes |
| Export SHA-256 | `5caaa943d34847cf481f1eebc4ad51675ed2a685c6d342422ec28cea940f5aba` |
| Archive run | [34659231780](https://github.com/its-me-anoop/gameon/actions/runs/34659231780), **SUCCESS** |
| Archive SHA-256 | `22aed3213233d293f97775dc9131650a76358e39007729a91869acad48fb6e89` |
| Released archive toolchain | Xcode **26.6 (17F113)**; build-machine OS **25G83** |
| Upload run | [34659787504](https://github.com/its-me-anoop/gameon/actions/runs/34659787504), **SUCCESS**; same archive, no rebuild |
| Apple build | `2ac69fe0-4b4b-47b3-8939-c35aeeb47d4d`; VALID, unexpired, IN_BETA_TESTING |
| TestFlight readback | [Verified receipt](../build/lifeline-testflight-readback.json), 12 September 00:02:48 UTC; existing Internal group membership confirmed |
| What to Test source | Committed `docs/testflight-lifeline-what-to-test.txt`, SHA-256 `e94f82ba0ad00154a5aae0b955429743c0781ffc0c32721e628df5d0abdd4999` |

The archive verified the product/scene, bundle, version/build, all eleven native bridge exports, signature, Game Center entitlement and privacy reason. Credential cleanup passed in archive and upload jobs. Superseded runs `34657229754`, `34657640594` and `34658491720` were never promoted.

## Verified App Store Connect configuration

Seven authorized changes were applied and read back on 11 September at 23:11 UTC (12 September in London). Evidence: `build/lifeline-provider-apply-20260912.log` and `build/lifeline-provider-readback-20260912.json`. The helper reports an empty remaining-actions list.

| Record | Verified value |
| --- | --- |
| App | `6786840477`, bundle `com.flutterly.gravitile`, name **Little Lifeline** |
| Editable subtitle | **A little care. A moving world.** |
| Beta description and review instructions | Match the prepared Lifeline files; existing contact details retained |
| Weekly board | `grv3.lifeline.weekly.v1`, resource `1945e115-3481-4d76-91f7-b73c2c51d8a5` |
| Weekly schedule | First start **14 September 2026, 00:00 UTC**, seven-day duration and recurrence |
| Scoring | BEST_SCORE, descending integer, range 0–2147483647 |
| Existing Plus | `com.flutterly.gravitile.plus`, NON_CONSUMABLE, READY_TO_SUBMIT |
| Plus name | **Founder’s Carriage Collection**; Sunrise, Coastal and Heritage cosmetic finishes |
| Plus pricing / sharing | Existing USA **$2.99** price and family sharing retained |
| Tips and historical boards | Existing records preserved |
| Latest uploaded build at preflight | **12**, VALID and IN_BETA_TESTING; historical native app |
| Internal group | Existing tester and access to all builds; no invitation needed |

The current week is practice only. A new Apple recurring leaderboard cannot start in the past, so the first live week opens on September 14; earlier results never enter that occurrence. The client checks the loaded occurrence against the scenario's UTC Monday and seven-day duration. [Apple recurring leaderboard setup](https://developer.apple.com/videos/play/wwdc2021/10067/), [leaderboard properties](https://developer.apple.com/help/app-store-connect/reference/game-center/leaderboards).

`Tools/configure_lifeline_services.py --weekly-start 2026-09-14T00:00:00Z` is read-only unless passed `--apply`. Do not recreate the configured board or entitlement. Product READY_TO_SUBMIT status does not establish agreement, banking, tax or purchase readiness. The prepared [What to Test](testflight-lifeline-what-to-test.txt) must be attached only to the exact processed Unity build.

## Purchase and leaderboard acceptance

A product being READY_TO_SUBMIT is not a successful purchase test. Before treating monetization as verified, test the four actual IDs through the signed Unity player: product loading and localized prices, collection success/cancellation/pending state, restore with and without ownership, prior Plus ownership, entitlement revocation and each tip. Check that only verified transactions grant finishes and that home/challenge outcomes are unchanged by ownership. Do not use the previous native app’s StoreKit results as Unity evidence.

TestFlight purchases use Apple’s sandbox and do not charge testers. Product metadata may take up to one hour to propagate to sandbox. A dedicated Sandbox Apple Account is useful for interrupted transactions, purchase-history resets and family-sharing tests. Changing account settings is a manual tester action; the release helper does not sign anyone out. [Apple sandbox testing](https://developer.apple.com/documentation/storekit/testing-in-app-purchases-with-sandbox).

If StoreKit returns no products, verify the explicit bundle ID, signed provisioning and IAP capability, matching product identifiers, storefront availability, pricing/localization and current developer/paid-app agreements plus required financial information. The Account Holder resolves any agreement action; the helper never accepts terms. A local `.storekit` configuration can test code paths without Apple servers but cannot prove App Store sandbox availability. [Apple TN3186](https://developer.apple.com/documentation/technotes/tn3186-troubleshooting-in-app-purchases-availability-in-the-sandbox), [testing environments](https://developer.apple.com/help/app-store-connect/test-in-app-purchases/overview-of-testing-in-sandbox).

For Game Center, inspect player-initiated authentication and the real dashboard, then a completed four-minute score on the active occurrence. Test offline retry, account switching and a week boundary. Before the first occurrence opens, record authentication separately and mark live score submission NOT RUN. Expired scores must remain local and must not enter a later occurrence. Empty/unavailable rankings must never be populated with invented players.

## Export, archive and upload

Use licensed Unity **6000.3.24f1** with iOS Build Support. Complete the runtime/visual checks, prepare the project, then commit the full intended Unity source and release tooling. Run the final candidate’s Unity EditMode tests and retain their NUnit XML. `Tools/package_unity_export.py` requires passing Little Lifeline Core, profile and world test fixtures; an old Orbit Orchard test report is insufficient.

From the repository root, with no other Unity process using the project:

```bash
LIFELINE_EDITOR='/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity'
"$LIFELINE_EDITOR" -batchmode -nographics -projectPath "$PWD/Unity/OrbitOrchard" \
  -buildTarget iOS -runTests -testPlatform EditMode \
  -testResults "$PWD/build/lifeline-tests.xml" -logFile "$PWD/build/lifeline-tests.log"
"$LIFELINE_EDITOR" -batchmode -nographics -quit -projectPath "$PWD/Unity/OrbitOrchard" \
  -buildTarget iOS -executeMethod OrbitOrchard.Editor.OrchardBuild.BuildIOS \
  -logFile "$PWD/build/lifeline-export.log"
python3 Tools/package_unity_export.py
python3 Tools/package_unity_export.py --write --test-results build/lifeline-tests.xml
```

`BuildIOS` selects Device SDK explicitly. Its success-only provenance records `product=little-lifeline`, the Lifeline scene, exact commit, clean-source status and Unity version. Simulator exports remain separate and cannot provide production export provenance. The packager checks display name, product, scene, source/tree, native files, privacy manifest and tests; rejects credentials, unsafe paths and symlinks; and measures free space. It keeps `orbit-orchard-ios.zip` as an internal transport filename. Do not overwrite an existing candidate ZIP.

Upload that ZIP as an **unpublished draft GitHub release asset**, with `targetCommitish` equal to the full committed and pushed source SHA. Never publish the transport draft. Keep its tag in the required `unity-export-...` format. The workflow uses the hash from `build/unity-transfer/orbit-orchard-ios.zip.sha256`. This transfers the generated project without adding it to git. The 2 GiB asset ceiling and disk-space checks remain enforced.

```bash
SOURCE_SHA="$(git rev-parse HEAD)"
EXPORT_SHA256="$(cut -d ' ' -f 1 build/unity-transfer/orbit-orchard-ios.zip.sha256)"
EXPORT_TAG="unity-export-lifeline-${SOURCE_SHA:0:12}-b14"
printf '%s\n' 'Internal Little Lifeline Unity export transfer. Keep this draft unpublished.' > build/unity-transfer/release-notes.md
gh release create "$EXPORT_TAG" build/unity-transfer/orbit-orchard-ios.zip \
  --draft --target "$SOURCE_SHA" --title 'Little Lifeline Unity export · build 14' \
  --notes-file build/unity-transfer/release-notes.md
gh workflow run release.yml --ref codex/orbit-orchard \
  -f source_sha="$SOURCE_SHA" -f export_release_tag="$EXPORT_TAG" \
  -f export_sha256="$EXPORT_SHA256" -F upload_to_testflight=false
```

The registered `release.yml` archives the Unity-iPhone target once on the hosted **macos-26 arm64** runner. Current primary runner inventory lists released Xcode **26.6 (17F113)** and **26.5 (17F42)**. Selection deliberately requires released Xcode 26 and a released macOS build. The archive gate verifies Little Lifeline’s display name/product/scene, exact version/build, UnityFramework, **all eleven native bridge exports**, signing, Game Center entitlement and the CA92.1 app-local UserDefaults privacy declaration. Signing credentials are owner-readable and removed even after failure. [GitHub runner inventory](https://github.com/actions/runner-images/blob/main/images/macos/macos-26-arm64-Readme.md).

This stable-runner preference is project policy, not a claim that Apple forbids RC uploads. Apple explicitly opened submissions using **Xcode 27 RC on 9 September 2026**. The current minimum remains Xcode 26 / iOS 26 SDK or later. Local prerelease macOS previously produced unacceptable archive metadata, so this pipeline avoids rebuilding the release on that host. [Apple RC announcement](https://developer.apple.com/news/?id=k1mtkt1k), [current SDK requirements](https://developer.apple.com/news/upcoming-requirements/).

After the archive passes, promote the same retained signed archive, without rebuilding:

```bash
ARCHIVE_RUN_ID='REPLACE_WITH_SUCCESSFUL_ARCHIVE_RUN_ID'
gh workflow run release.yml --ref codex/orbit-orchard \
  -f source_sha="$SOURCE_SHA" -f archive_run_id="$ARCHIVE_RUN_ID" \
  -F upload_to_testflight=true
```

Archive artifacts and logs last seven days; the intermediate export artifact lasts one day. The archive stage can consume up to 90 minutes of hosted macOS time; promotion permits 30 minutes and does not compile again. No Unity license is needed on CI. Concurrency serializes release runs, shell pipefail preserves Xcode failures, and automatic build-number changes are disabled.

Recheck the latest App Store Connect build before upload. After upload, verify build **14**, version **3.0**, `processingState=VALID` and internal TestFlight availability, then attach the exact candidate’s testing notes. Upload-command success is not availability. The workflow does not submit app review, invite external testers or send tester messages.

## Candidate preservation and remaining gates

The isolated branch is `codex/orbit-orchard` at the public [gameon repository](https://github.com/its-me-anoop/gameon). Unity source and scoped release tools are committed; the user's separate native Swift/Xcode work remains in place. Stage only reviewed paths. Generated exports, caches, simulator data, transfer ZIPs and `.blend1` backups are excluded from source. A source change requires a new commit, export and distinct sealed package; do not overwrite or force-push a candidate.

Local Xcode **27.0 (27A266a)** is a beta and is used only for simulator QA. Release CI selected **Xcode 26.6 (17F113)** on released macOS **25G83**. Successful local simulator compilation cannot substitute for the CI signed-device archive or TestFlight processing.

The upload and Apple processing are complete. The exact build is **VALID**, **IN_BETA_TESTING** and present in the existing Internal group. The en-US testing notes exactly match the committed copy, verified 12 September at 00:02:48 UTC. [Release receipt](../build/lifeline-release-receipt.json). Physical-device performance, StoreKit transactions and live Game Center behavior remain beta verification gaps, not claims established by the simulator.

The release authorization includes TestFlight upload. No app-review submission, external tester invitations, messages or agreement acceptance is included. An uncertain upload must be resolved by provider readback before retrying. A rejected binary needs a new build number and candidate. Retain exact run IDs, checksums and processing evidence in the QA record.
