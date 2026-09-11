# Little Lifeline 3.0 release

The candidate is the Unity game in `Unity/OrbitOrchard`, version **3.0 (14)**, bundle `com.flutterly.gravitile`. Its shipped scene is `Assets/LittleLifeline/Scenes/Lifeline.unity`. Earlier Orbit Orchard and native SwiftUI candidates are historical. The internal project, bridge and ZIP filenames retain their original names for compatibility; the product and scene checks identify Little Lifeline explicitly.

## Candidate content

The first release contains Willowbank, Copperhill and Seabrook; four carriage slots; consultation, diagnostics and recovery; and up to six crew members. Each town has two restoration projects. Current-stop community visits continue after projects finish. The same simulation calculates active progress and up to 24 hours of offline catchup, with bounded arrivals and no accumulating missed-day penalty. Save files pair full simulation state with accounted UTC time, use atomic replacement and retain a backup. Progress is device-local.

The Weekly Call gives everyone the same deterministic arrival pattern, starting crew and budget for a fixed four-minute scenario. Pausing and retries are free. Ranking uses completed visits first, then shorter waiting for those completed visits. Home progression, paid finishes and time spent idling do not add to the challenge score. This is a client-simulated leaderboard, not server-authoritative anti-cheat; there is no cash prize or claim of verified competition.

The existing Plus non-consumable (`com.flutterly.gravitile.plus`) becomes the **Founder’s Carriage Collection**. Sunrise, Coastal and Heritage finishes are cosmetic. Verified previous Plus purchases retain access. The three existing consumable tips grant no entitlement. Campaign and challenge content remain free. There are no ads, subscriptions, paid speed-ups, energy purchases or charity claims.

## Current release status — 12 September 2026

The Unity player has built, installed and rendered on the iPhone 17 Pro / iOS 26.5 simulator. App-scoped XCTest actions and the actual checksummed save confirm opening the clinic, building Diagnostics, and assigning Ivo. The compact icon controls operate over the full-height 3D world. Scanner and recovery patient poses were observed corrected on simulator. Follow-up placement checks found staff intersecting equipment and the consultation resident at the wrong furniture; their bounded correction now passes the full suite and awaits a rebuilt visual check. Physical-device performance, real StoreKit transactions and live Game Center behavior remain unverified. See [the evidence record](qa/little-lifeline.md).

The latest complete Unity EditMode run passed **148/148** (`build/lifeline-tests-aisle.xml`), including imported-furniture checks for patients and stationary crew. The generated Swift-header import was subsequently corrected to the framework-qualified form and the actual simulator Xcode build passed. All eleven native bridge exports also compile and link in the independent bridge check. The renderer correction has a fresh passing test report; its simulator check follows the rebuild.

**No Little Lifeline TestFlight build has been uploaded yet.** Source `241304b8082602745e5dc36df29a84344e5538e0` has a sealed Device SDK export and archive-only CI run [34657640594](https://github.com/its-me-anoop/gameon/actions/runs/34657640594). That archive passed all signing, native-symbol, product, privacy and entitlement gates, but was superseded and must not be promoted. The subsequent `3b4e901` run [34658491720](https://github.com/its-me-anoop/gameon/actions/runs/34658491720) was cancelled before upload for the stationary actor correction. The final corrected export is pending. The previous run [34657229754](https://github.com/its-me-anoop/gameon/actions/runs/34657229754) was cancelled after the native header problem was identified. Neither is delivery evidence.

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

Remaining order:

1. Verify the final stationary actor correction in actual simulator play. Imported-geometry tests and prior-candidate relaunch/offline collection already pass.
2. Commit the reviewed changes, run final Unity tests, export Device SDK from that clean source and package with the new passing report.
3. Transfer through an unpublished draft, create the signed archive, and pass all identity, native export, privacy, signing and entitlement checks.
4. Recheck build-number availability, promote that exact archive and wait for App Store Connect processing to become VALID and internally available. Attach the prepared per-build testing notes.
5. Record physical iPhone, StoreKit and Game Center gaps explicitly. Do not claim live weekly score submission before September 14.

The release authorization includes TestFlight upload. No app-review submission, external tester invitations, messages or agreement acceptance is included. An uncertain upload must be resolved by provider readback before retrying. A rejected binary needs a new build number and candidate. Retain exact run IDs, checksums and processing evidence in the QA record.
