# Little Lifeline 3.0 release

The candidate is the Unity game in `Unity/OrbitOrchard`, version **3.0 (14)**, bundle `com.flutterly.gravitile`. Its shipped scene is `Assets/LittleLifeline/Scenes/Lifeline.unity`. Earlier Orbit Orchard and native SwiftUI candidates are historical. The internal project, bridge and ZIP filenames retain their original names for compatibility; the product and scene checks identify Little Lifeline explicitly.

## Candidate content

The first release contains Willowbank, Copperhill and Seabrook; four carriage slots; consultation, diagnostics and recovery; and up to six crew members. Each town has two restoration projects. Current-stop community visits continue after projects finish. The same simulation calculates active progress and up to 24 hours of offline catchup, with bounded arrivals and no accumulating missed-day penalty. Save files pair full simulation state with accounted UTC time, use atomic replacement and retain a backup. Progress is device-local.

The Weekly Call gives everyone the same deterministic arrival pattern, starting crew and budget for a fixed four-minute scenario. Pausing and retries are free. Ranking uses completed visits first, then shorter waiting for those completed visits. Home progression, paid finishes and time spent idling do not add to the challenge score. This is a client-simulated leaderboard, not server-authoritative anti-cheat; there is no cash prize or claim of verified competition.

The existing Plus non-consumable (`com.flutterly.gravitile.plus`) becomes the **Founder’s Carriage Collection**. Sunrise, Coastal and Heritage finishes are cosmetic. Verified previous Plus purchases retain access. The three existing consumable tips grant no entitlement. Campaign and challenge content remain free. There are no ads, subscriptions, paid speed-ups, energy purchases or charity claims.

## Live preflight, 11 September 2026

These are read-only observations, not delivery evidence:

| Record | Readback |
|---|---|
| App Store Connect app | `6786840477`; bundle `com.flutterly.gravitile` |
| Current app name | Gravitile: Orbit Orchard |
| Editable app-info state | REJECTED; one editable en-US localization |
| Latest uploaded build | 12, VALID, uploaded 8 July 2026 |
| Existing Plus | `6786847881`, NON_CONSUMABLE, READY_TO_SUBMIT; family sharing enabled |
| Plus localization | Orchard Pass; WAITING_FOR_REVIEW |
| Existing Plus base price | USA, $2.99; read back and preserved |
| Three tips | Existing small, medium and large products; all READY_TO_SUBMIT |
| Little Lifeline weekly board | Not created |
| Prior leaderboard records | Preserved; the new client does not read or submit their queues |
| GitHub secrets | `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_P8` names present; values not printed |
| Last release workflow run | [28969361839](https://github.com/its-me-anoop/gameon/actions/runs/28969361839), successful historical native release |

The current beta description and review notes still describe Orbit Orchard. No Little Lifeline provider write, app-review submission, external invitation or tester message was performed during this preparation. Agreements, banking and tax readiness are not verified by product `READY_TO_SUBMIT` status.

## Reviewable provider changes

`python3 Tools/configure_lifeline_services.py --weekly-start 2026-09-14T00:00:00Z` performs only reads and prints the exact plan. Its live plan contains seven changes:

1. Create `grv3.lifeline.weekly.v1`, reference/display name **Little Lifeline · Weekly Call**.
2. Add its en-US localization.
3. Change the editable app title to **Little Lifeline**, subtitle **A little care. A moving world.**
4. Replace the beta description with [the prepared description](testflight-lifeline-description.txt).
5. Replace beta reviewer instructions with [the prepared review notes](testflight-lifeline-review-notes.txt), retaining existing contact details and requiring no demo account.
6. Update the existing Plus reference name and review instructions.
7. Set the Plus localized name to **Founder’s Carriage Collection** and description to **Sunrise, Coastal and Heritage. Cosmetic train finishes.**

The weekly leaderboard uses integer scores, BEST_SCORE, descending order and range 0–2147483647. Its first start is **Monday 14 September 2026, 00:00 UTC**, duration **168 hours**, recurring every **seven days**. The API representation is `PT168H` and `FREQ=DAILY;INTERVAL=7`; this matches the recurrence format already present on an unrelated existing weekly board. The native client checks that the loaded occurrence starts at the exact challenge Monday and lasts seven days before submitting.

Apple states that a new recurring leaderboard cannot start in the past. Therefore a September 7 start cannot activate this new board immediately. Before September 14 the game explains the opening date and lets players practise locally; those earlier results never enter the first live week. The script rejects a past start before any mutation. If setup occurs after the proposed start, choose the next future Monday and update the native availability date and copy before exporting. [Apple recurring leaderboard setup](https://developer.apple.com/videos/play/wwdc2021/10067/), [UTC leaderboard properties](https://developer.apple.com/help/app-store-connect/reference/game-center/leaderboards).

After the runtime candidate is ready, the root agent can execute the already-authorized scoped changes with the same command plus `--apply`. No further user authorization is needed for this approved release scope. The helper preserves prices, product IDs, family sharing, tips, legacy boards, contacts, agreements and tester settings. It stops on an unexpected existing leaderboard configuration and re-reads all requested metadata after applying. A second read-only run should have an empty `actions` list. If an API call fails partway through, inspect that read-only plan before retrying; do not reset the board or replace the entitlement product.

The per-build [What to Test text](testflight-lifeline-what-to-test.txt) is deliberately separate. Attach it only to the exact processed Little Lifeline build, after upload, through its en-US beta build localization. Beta app localizations have no independent app-title field; the app-info localization supplies the TestFlight identity.

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

## Verification and recovery

| Check at this release-preparation update | Evidence |
|---|---|
| Read-only provider plan | Seven scoped actions; `build/lifeline-provider-plan.json` |
| Provider helper logic | 6 pure unit tests passed; no network writes |
| Export/identity rejection tests | 14 synthetic fixture tests passed |
| Workflow and shell validation | actionlint and shellcheck passed |
| Native Swift 6/ObjC++ compile and link | Passed; 11 exports |
| Native recurring boundary/account-model checks | 16 passed |
| Actual Unity import, tests and runtime | Root owns current evidence; use the latest QA log rather than this static checklist |
| Root’s initial runtime observation | Actual 3D app launch and new-carriage action observed; full scenario QA remains separate |
| Little Lifeline provider mutations | NOT RUN |
| Signed Unity archive/upload and processed TestFlight build | NOT RUN |
| Real signed Unity purchases and active weekly score | NOT RUN |

If export/archive fails, fix the cause and create a new committed candidate. If upload is uncertain, inspect App Store Connect before retrying the retained archive. A rejected binary requires a new build number and export. Do not force-push or overwrite a sealed candidate. An unwanted TestFlight candidate can later be expired by its exact build ID; no automatic deletion or expiration is performed here. Local save rollback uses the last valid snapshot with its matching accounted time, so offline progress cannot be credited independently of the state.


## Independent readiness audit, 12 September 2026

**Ready to continue release preparation; not ready to claim TestFlight delivery.** Runtime QA and the final source freeze are still in progress. This audit did not start Unity, upload a build, change provider records or touch the interface.

Fresh readback still shows build 12 as VALID and IN_BETA_TESTING, the Internal group with access to all builds, the Orbit Orchard app/Plus text, and no `grv3.lifeline.weekly.v1` leaderboard. The exact registered `com.flutterly.gravitile` bundle has both GAME_CENTER and IN_APP_PURCHASE capabilities. Existing certificate metadata includes unexpired development certificates, while the local keychain reports two valid signing identities. These observations do not prove hosted distribution signing: the new Unity CI archive remains the decisive check. The same three GitHub signing-secret names are present.

The iOS module is installed under `/Applications/Unity/Hub/Editor/6000.3.24f1/PlaybackEngines/iOSSupport` and registered in the actual Unity preparation log. The local Editor is 6000.3.24f1 and licensed preparation has succeeded. Local Xcode is still **27A266a**, a beta build; it is not the Xcode 27 RC permitted by Apple. Use hosted Xcode 26.6 for this release. Fresh [runner inventory](https://github.com/actions/runner-images/blob/main/images/macos/macos-26-arm64-Readme.md) shows macOS **26.6.2 (25G83)**, image **20260907.0351.1**, with released Xcode **26.6 (17F113)**. No Unity Device SDK export currently exists. Approximately **9.7 GiB** was free locally; measure export and ZIP requirements again after the build.

`build/lifeline-tests.xml` contains **97 passed, zero failed/skipped**, completed at **2026-09-11 22:45 UTC**. The package helper accepts its test-fixture structure, but subsequent HUD/CSS edits make it historical evidence. Run the complete tests again after the final interface changes. Root reports a newer full-suite run at **129/131 with two failures under repair**; that incomplete run is not release evidence either. The older `build/unity-tests.xml` contains failures and must not be packaged. Current release-helper checks pass: 14 packaging tests, six configuration-plan tests, actionlint, shellcheck and diff whitespace checks.

### Preserve the shared checkout

The current branch is `codex/orbit-orchard`, based on `2557a03fa20a49cdc13c692f30dd9f4a5f9af387`. Fresh remote inspection finds no remote branch of that name. Remote main is `78b08ab35526d97fbd9920edfc758660662f9af8`; current HEAD is five commits ahead with no divergence. Those existing commits remain part of this branch’s history; do not reset or rewrite them during the release. All Unity project files are still untracked. Native Swift/Xcode files, earlier concept files and other pre-existing work also remain modified/untracked. None needs to be removed, reset, stashed or swept into the Unity release commit.

Freeze edits with the other agents, then stage only reviewed release paths: the complete `Unity/OrbitOrchard` source tree including `.meta` files and licenses; `Tools/package_unity_export.py`, `Tools/select_released_xcode.sh` and their packaging tests; `.github/workflows/release.yml`; the Lifeline provider helper/test, release notes and necessary documentation. Review `.gitignore` and README hunks separately if including them. Do not use a blanket `git add -A`. Inspect `git diff --cached --name-status` before committing, and leave unrelated/native work in place.

Unity’s own ignore file excludes Library, Temp, Logs, UserSettings and Builds. The committed Assets/Packages/ProjectSettings source is approximately 8.6 MB before git compression; generated exports and caches are not source. The packager intentionally checks only the Unity source and required release tooling for dirtiness, so preserved changes outside that scope do not block a reproducible Unity export. It now refuses an existing ZIP, checksum, manifest sidecar or dangling output symlink rather than overwriting earlier candidate evidence.

### Exact remaining order

1. Finish interactive UI/runtime QA and fixes; retain device/window size, screenshots and actual observations. Verify the compact controls still expose restore, preferences, crew/route management and weekly planning.
2. Prepare the final Unity project, review generated scene/settings and metadata, freeze agent edits, then create the scoped source commit above. Push the isolated branch without force. Use its full SHA throughout; changes after this point require another commit and export.
3. Run final full Unity EditMode tests from that candidate. Export a **Device SDK** iOS project via `OrchardBuild.BuildIOS`, check success provenance and inspect the actual native postprocessor output. Measure space and package with the new passing report. The helper must accept the clean candidate; do not bypass its gates.
4. After runtime QA is accepted, apply the seven already-authorized provider changes and verify empty remaining actions. Confirm the September 14 opening, retained Plus price/family sharing and unchanged tips. Update the prepared reviewer/testing copy if final navigation differs from its current wording.
5. Transfer the checked ZIP through an unpublished draft release, dispatch the registered Unity workflow for an archive, and inspect the signing/Unity/product/bridge/privacy gates. This is the first verification of generated IL2CPP, the real Unity native linkage and hosted signing together.
6. Promote that same signed archive to TestFlight. Recheck Apple’s latest build before upload; use a higher build if 14 is no longer available. Wait for processing VALID and internal TestFlight availability, then attach testing notes to the exact build. Do not report delivery on upload-command success alone.
7. Verify the installed TestFlight candidate’s real purchase/restore/cancellation/pending behavior and Game Center authentication/dashboard. Live recurring score submission remains NOT RUN until the first Monday occurrence actually opens; the pre-opening UI must explicitly say practice is available now. No external invitations, tester messages or app-review submission are included.

The source repository is public. Transfer releases must remain drafts; signed archive artifacts remain subject to repository Actions access and seven-day retention. Secrets stay out of source, release assets and logs. There is no reason to publish the generated Xcode project or transport draft publicly.


The root’s latest runtime observations include building Recovery, assigning Nell, travel to Copperhill with distinct scenery, weekly start/crew planning and an actual Sunrise preview. These observations do not replace the still-needed final suite, player export, native signing or sandbox checks. The reviewer and testing text now use **Open clinic**, the top-right **gear** for the depot, **palette / Finishes**, the bottom **people** icon for crew, **map** for Route and **trophy** for The Weekly Call. No crew-outfit content is advertised.

An explicit proposed staging list is saved in `build/lifeline-stage-paths.txt`. It includes the Unity source, release/provider/asset-generation helpers and their tests, Lifeline-only docs/QA/concept files, and the authored `assets/little-lifeline-game.blend`. It excludes `.blend1` backups, previous concept files, native Swift/Xcode work and caches. Nothing has been staged or committed by the release audit. After the root confirms the source freeze, inspect the list and use `git add --pathspec-from-file=build/lifeline-stage-paths.txt`; review README and `.gitignore` separately. Review the staged diff and scan the paths for secrets before committing. A dirty file outside the release paths can remain untouched throughout packaging.

### Latest local candidate checks

The complete repaired Unity suite passed 134/134 at 23:06:53 UTC on 11 September (12 September in London), including 30 renderer cases and five compact management UI cases. The standalone compiler passed 13 assemblies and 66 managed tests. Final source-freeze tests and device export remain tracked in the QA record.
