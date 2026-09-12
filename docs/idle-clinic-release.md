# Little Lifeline 3.1 — idle clinic release

This candidate replaces the train game with a fixed 3D clinic. The public display name stays **Little Lifeline**, bundle `com.flutterly.gravitile`, app `6786840477`, team `K6623R3GP5`. Planned version/build: **3.1 (15)**. New internal product marker: `idle-clinic`; launch scene: `Assets/IdleClinic/Scenes/Clinic.unity`.

No clinic App Store metadata changes or uploads have been performed during implementation. A dedicated development profile was created for the existing certificate and registered QA phone; see the device recipe. The preceding release, 3.0 (14), was verified VALID and internally available on 12 September 2026; its source was `0c82dbf4b13db31812662f2d2b396e416066027e`. Archive run `34659231780` and promotion run `34659787504` demonstrate the pipeline, not validation of this new clinic candidate.

## Save and Apple service behavior

`ClinicProfileStore` writes `idle-clinic-profile-v1.json` in the application container. The checksummed envelope contains the complete core state, tutorial step, wallet, tills, staff, patients, construction, preferences, and accounted UTC time. A flushed temporary file replaces the primary atomically; a previous good backup remains recoverable, and unreadable originals are preserved. Checksums detect corruption; this is a local save, not a server-trusted economy.

Offline earnings use the core's eight-hour window. Construction uses the full absence. Earnings remain at reception until collected. Offline state and the full accounted interval are committed together before return results are exposed. A failed offline write blocks active simulation and spending until it can be retried. Backward clocks cannot rewind the watermark, and a stale pre-resume profile reference cannot overwrite committed progress.

Migration reads only sound, haptics and reduced-motion preferences from the previous checksummed Lifeline save or backup. It never advances or rewrites the previous campaign, copies its money/tutorial/results, or grants an entitlement from a local flag. Existing verified StoreKit ownership remains available through the shared Apple bridge. New purchases, the cosmetic shop and leaderboards are deferred for this clinic beta; do not advertise them as new clinic features.

Native Game Center is lazy. Clinic startup and foreground restoration do not read, prune, retry, or discard previous weekly score data. An explicit legacy leaderboard feature must activate that service. The thirteen bridge exports and product IDs remain compatible. No legacy leaderboards or IAP prices are changed.

The application privacy manifest merges app-local UserDefaults reason `CA92.1` and app-container file metadata reason `C617.1`, preserving Unity's bundled declarations. [Apple's required-reason definitions](https://developer.apple.com/documentation/bundleresources/app-privacy-configuration/nsprivacyaccessedapitypes/nsprivacyaccessedapitype) cover these uses.

## Preparation and validation

Coordinate with the active Editor owner. Never run two Unity processes against this project. Run commands from the repository root; the Editor path below is the installed 6000.3.24f1 version.

```sh
/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD/Unity/OrbitOrchard" \
  -executeMethod OrbitOrchard.Editor.OrchardBuild.Prepare \
  -logFile "$PWD/build/clinic-prepare.log"

/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "$PWD/Unity/OrbitOrchard" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/build/clinic-tests.xml" -logFile "$PWD/build/clinic-tests.log"
```

`Prepare` creates `ClinicPanel` and the clinic scene if missing, selects only the clinic scene for builds, and sets 3.1 (15). `ClinicModelImporter` configures scoped clinic character FBXs for Legacy animation and keeps their material subassets embedded. Animation and accessibility modules are included. Existing game scenes and model metadata remain preserved.

Require actual Unity import/tests plus Editor and iOS simulator acceptance before release. Pure C# source compilation, synthetic package tests, and Swift linkage are separate evidence. Use `python3 Tools/check_unity_sources.py --output build/clinic-source-check` for the source-only check once all modules exist. Use `bash Unity/OrbitOrchard/Assets/OrbitOrchard/Plugins/iOS/validate-native.sh` for native bridge compilation/linkage. Neither substitutes for an actual Unity export or signed-device test.

After preparation and QA, root freezes the source, stages only the intended Unity/release changes, reviews the diff, commits, and pushes `codex/orbit-orchard`. Preserve unrelated pre-existing Swift/native work; do not use blanket staging, reset, stash, or cleanup. Re-run the final full Unity tests for the frozen source.

The macOS camera plugin is a scoped universal bundle; rebuild and validate it with the scripts in `Assets/IdleClinic/Plugins/macOS`. Its importer excludes iOS. Native synthetic event tests passed; physical trackpad/mouse behavior and 44-point controls still require runtime acceptance. The new `ScreenWidthPoints` bridge uses UIKit window points to size the iOS panel without estimating DPI.

## Device export and packaging

```sh
ORCHARD_IOS_EXPORT="$PWD/Unity/OrbitOrchard/Builds/iOSClinic" \
/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD/Unity/OrbitOrchard" \
  -executeMethod OrbitOrchard.Editor.OrchardBuild.BuildIOS \
  -logFile "$PWD/build/clinic-export.log"

python3 Tools/package_unity_export.py Unity/OrbitOrchard/Builds/iOSClinic \
  --output build/unity-transfer-clinic/orbit-orchard-ios.zip \
  --test-results build/clinic-tests.xml --write
```

The export must be Device SDK and come from the exact committed source, with `developmentBuild=false`, `iosSdk=device` and `buildSucceeded=true`. Every export attempt invalidates any previous success marker before building; only a completed export writes success. Diagnostic exports and missing markers are rejected. The generated Xcode project must contain only `SDKROOT=iphoneos` and no simulator IL2CPP flag.

The package requires the `idle-clinic` marker, clinic scene, and final full Unity report containing all five fixtures: `IdleClinic.Tests.ClinicSimulationTests`, `IdleClinic.Tests.ClinicProfileTests`, `IdleClinic.Tests.ClinicWorldTests`, `IdleClinic.Tests.ClinicHUDTests` and `IdleClinic.Tests.ClinicPerformanceTests`. Every reported case must pass, with zero skipped or inconclusive tests. Use the full source-freeze report; do not filter the run. It checks source tree identity, native critical-file hashes, bundle identity, host architecture, output uniqueness and free space. Production extraction still requires the export size plus 4 GiB free. Synthetic tests mock disk capacity and separately exercise this guard; they never bypass it for real exports.

For simulator validation, use `OrbitOrchard.Editor.OrchardBuild.BuildIOSSimulator` with a separate `ORCHARD_IOS_EXPORT` directory. It writes `developmentBuild=true` and `iosSdk=simulator`, uses Development mode for opt-in performance and accessibility QA, then restores Device SDK afterward. Simulator frame times are host diagnostics, not physical-device performance. Its thermal getter reports unsupported. A simulator export is not a release package.

For physical-device development signing, opt-in performance recording and report extraction, use [the iPhone QA recipe](qa/clinic-iphone-performance.md). That development export is never a TestFlight candidate.

## Draft transfer, archive and promotion

Replace the uppercase placeholders with the final verified values. Use a new tag/output for every candidate; never overwrite a previous draft or artifact. The repository is public, so the transfer release must remain **unpublished/draft**. The existing workflow is `.github/workflows/release.yml`; dispatch it on `codex/orbit-orchard` without a main-branch merge.

```sh
gh release create unity-export-clinic-SHORT_SHA-b15 \
  build/unity-transfer-clinic/orbit-orchard-ios.zip \
  --repo its-me-anoop/gameon --target FULL_SOURCE_SHA --draft \
  --title 'Little Lifeline 3.1 (15) — clinic export' \
  --notes-file build/clinic-transfer-notes.txt

gh workflow run release.yml --repo its-me-anoop/gameon --ref codex/orbit-orchard \
  -f source_sha=FULL_SOURCE_SHA \
  -f export_release_tag=unity-export-clinic-SHORT_SHA-b15 \
  -f export_sha256=EXACT_EXPORT_SHA256 -f upload_to_testflight=false
```

Read back draft status, exact target, uploaded size and GitHub's asset digest before dispatch. Require archive success and inspect its sealed manifest. The workflow selects released arm64 Xcode, validates the clinic marker/scene, app identity/version, thirteen bridge exports, both privacy reasons, signature and Game Center capability, then stores a signed archive. Signing keys come only from existing `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_P8` secrets and are removed on every outcome. Successful previous execution used Xcode 26.6 (17F113), macOS 26.6.2 (25G83); the new run records its actual toolchain.

After root accepts the exact runtime candidate and authorizes promotion, reuse the successful archive:

```sh
gh workflow run release.yml --repo its-me-anoop/gameon --ref codex/orbit-orchard \
  -f source_sha=FULL_SOURCE_SHA -f archive_run_id=SUCCESSFUL_ARCHIVE_RUN_ID \
  -f upload_to_testflight=true
```

This skips export transfer and archive, verifies the stored archive checksum and source/product/scene identity, and uploads it without rebuilding. Do not promote a superseded candidate. Recovery means fix and test a new source/export/archive; never modify a sealed archive in place. A successful archive used around seven to twelve hosted macOS minutes in the previous release; promotion used around two. Exact duration and billable minutes vary. Artifacts have bounded retention; transport drafts remain until separately authorized for cleanup.

## Provider handoff and completion

The [metadata handoff](clinic-metadata-handoff.md) contains exact, read-only-by-default commands for the two clinic beta text updates and the per-build testing localization. It does not reuse the train configuration helper.

Before upload, read current builds and ensure build 15 has not already been used. Do not alter agreements, prices, previous boards, testers, notification preferences or app-review state. Before the clinic beta is exposed, root should update only the beta description and reviewer instructions from `docs/testflight-clinic-description.txt` and `docs/testflight-clinic-review-notes.txt`; the public app name remains Little Lifeline. The previous product metadata should remain unchanged until a new shop design is approved.

The existing internal group is `a1098f67-cfde-4eaa-8626-e250f881596a`, with access to all builds as verified during 3.0 release. Recheck that condition for this release. After upload, inspect `/v1/apps/6786840477/buildUploads` for exact 3.1 (15); this endpoint shows processing/errors before the ordinary build resource appears. Then require exact 3.1/build15, `processingState=VALID`, `expired=false`, `internalBuildState=IN_BETA_TESTING`, and membership in the existing Internal group's build list. Upload-command success alone is insufficient.

Attach the exact final committed `docs/testflight-clinic-what-to-test.txt` to that build's en-US `betaBuildLocalizations.whatsNew` using PATCH if the locale exists or POST if absent; verify exact text equality on readback. No external beta review, invitations, public links, or explicit tester notifications are part of this handoff. Record source SHA, export/archive checksums, archive/upload run URLs, Apple build ID, internal availability and note-localization ID. StoreKit restore and VoiceOver should be tested on a signed iOS build, separately from source/simulator evidence.

Apple references: [build upload status](https://developer.apple.com/help/app-store-connect/reference/app-uploads/build-upload-statuses), [internal build access](https://developer.apple.com/help/app-store-connect/test-a-beta-version/add-testers-to-builds), [per-build testing copy](https://developer.apple.com/documentation/appstoreconnectapi/post-v1-betabuildlocalizations).
