# Little Lifeline — current verification record

Updated 12 September 2026. Little Lifeline is the selected Unity hospital-on-rails concept. The game has passed the baseline checks below and runs on an iPhone simulator. **The patient and stationary staff placement corrections passed all 148 Unity tests; the updated iOS build and visual check are pending. Nothing has been uploaded to TestFlight.**

The evidence belongs to the builds named below. Passing tests do not establish physical-device behavior. Generated concept art and Blender authoring previews are not gameplay screenshots.

## Current evidence

| Area | Verified result and scope |
| --- | --- |
| Unity EditMode suite | **148 passed, 0 failed, 0 skipped** in [lifeline-tests-aisle.xml](../../build/lifeline-tests-aisle.xml), completed 11 September at 23:41:26 UTC. Little Lifeline: 31 Core, 18 profile, 49 presentation/HUD cases. Retained Orchard: 24 Core, 15 profile, 11 presentation cases. Fourteen new cases check imported beds, the consultation chair, working/idle crew and platform benches. Eleven exposed real placement failures before correction; the three already-clear platform positions were preserved. All now pass. |
| Standalone source and managed rules | [Source checker](../../Tools/check_unity_sources.py) compiled **13 assemblies** and passed **66 managed tests**: 24 Orchard Core, 31 Lifeline Core, six Orchard profile rules, five Lifeline weekly-date rules. [Summary](../../build/qa-lifeline/source-check/summary.txt). These overlap the Editor suite; they do not exercise Unity native JSON, rendering, or touch input. |
| Native Apple bridge | The framework-qualified Swift header import fixed the simulator build failure. The [native validator](../../Unity/OrbitOrchard/Assets/OrbitOrchard/Plugins/iOS/validate-native.sh) now reproduces Xcode's framework header layout; native Swift/Objective-C++ compilation and linking passed with **11 bridge exports**. Its independent link check uses a test-only Unity message callback, so it does not establish live Apple-service behavior. |
| iOS simulator build | Xcode build, installation, launch, and initial rendering succeeded for source commit `241304b8082602745e5dc36df29a84344e5538e0`, on **iPhone 17 Pro / iOS 26.5**, using Xcode **27.0 (27A266a)**. [Build evidence and log location](../../build/qa-lifeline/ios/runtime-build-evidence.json). |
| Simulator interaction and saving | Clinic opening, second-carriage selection, Diagnostics construction, Ivo assignment, and return to Hospital were exercised through XCTest input and checked against screenshots plus the real saved profile. [Smoke record](../../build/qa-lifeline/ios-smoke/README.md). |
| Relaunch, background recovery, and offline collection | **Verified on simulator source `3b4e901`.** Actual XCTest termination, 62 seconds absent, and relaunch retained all three rooms and Ivo/Nell assignments. The return report showed nine residents and 284 funds; Continue returned to the overview. After collection the valid save was revision 272, Tick 10522, with 101 completed residents. See the smoke record. |
| Scanner care pose | **Patient poses observed corrected on simulator `3b4e901`.** FBX import reverses the authored X axis. The scanner patient matches the mattress and both recovery patients face their headboards. The follow-up found staff within equipment and the consultation resident at the desk rather than chair; their corrected stationary anchors now pass the actual-geometry tests and await rebuilt visual inspection. |
| Physical iPhone performance and accessibility | **NOT VERIFIED.** No sustained device CPU/GPU, frame-time, memory, thermal, touch-latency, or VoiceOver assessment. The simulator accessibility snapshot exposed generic application/window/views, without individual Unity controls. |
| StoreKit and Game Center | Real purchase, restore, entitlement delivery, authentication, leaderboard submission/display, and weekly competition are **NOT VERIFIED**. No account or purchase operation was performed in the smoke run. |
| Release | A device export/transfer package is recorded in the build evidence. A signed archive and the forthcoming scanner-fix candidate still need release verification. **TestFlight upload, processing, and tester availability have not occurred.** |

The build JSON records the later smoke outcome separately from compilation and rendering; only the workflow described below has been exercised.

## Observed simulator workflow

The smoke harness targeted only `com.flutterly.gravitile`, using public XCTest UI APIs. It read the real simulator container; it did not seed, patch, or reset the profile. Four workflow XCTest executions passed, but their foreground assertions alone are not treated as functional proof. Screenshots show the visible result, and the profile verifier checks the corresponding saved state and SHA-256 envelope checksum.

| Checkpoint | Saved-state evidence |
| --- | --- |
| Open clinic | The unopened baseline had Tick 0. [Open verification](../../build/qa-lifeline/ios-smoke/artifacts/verification-open.json) confirms Tick 460, four completed residents, 180 funds, and a valid checksum. |
| Build Diagnostics | The second carriage was selected visually, then the Diagnostics/120 action was tapped. [Build verification](../../build/qa-lifeline/ios-smoke/artifacts/verification-built.json) confirms carriage `Id=1`, `Slot=1`, `Kind=Diagnostics`; Tick 760, six completed residents, and a valid checksum. |
| Assign Ivo and return | [Assignment verification](../../build/qa-lifeline/ios-smoke/artifacts/verification-assigned.json) confirms Ivo's `PrimaryRoomId=1`, `CurrentRoomId=1`, and active `TaskPatientId=10`. Final revision 96 records Tick 1161, nine completed residents, 160 funds, and a valid checksum. |

The [smoke record](../../build/qa-lifeline/ios-smoke/README.md) links each screenshot, XCTest result bundle, and accessibility snapshot. The initial calibration attempt made no change while the app was on Route; it is retained but excluded from successful clinic-open evidence. Coordinate input was necessary because individual Unity controls were absent from the accessibility tree.

## Editor and authoring observations

Earlier direct interaction in the portrait Unity Editor showed the train under the compact HUD, carriage focus above the lower dock, Copperhill scenery after travel, a resident on the recovery bed, and the Sunrise finish applied to the train. Those observations establish only the exercised Editor behavior; the simulator smoke did not exercise travel, weekly play, or cosmetic purchasing.

Original Blender models are authored by [create_lifeline_assets.py](../../Tools/create_lifeline_assets.py). The world includes the train, modular care equipment, residents and crew, plus distinct Willowbank, Copperhill, and Seabrook settings. Tests cover projection/picking, separate restoration landmarks, actor-pool reuse, and lost render-texture recovery. Source review is not a performance measurement, and the scanner finding limits any claim that all treatment poses are visually correct.

## Concise chronology and next verification

The initial 97-case Editor run and the later 131-case run with two failures are historical. The caption assertion and station-facing issues were corrected; the current baseline is the **134/134 b14 run**. The standalone checker then verified current assembly references and managed rules. The first Xcode simulator build exposed an app-style quoted Swift header import; changing it to the framework-qualified import produced a successful simulator build and launch. The subsequent clinic/Diagnostics/Ivo smoke established input and saved-state behavior and revealed the scanner alignment issue.

The complete furniture correction now passes all 148 Unity cases. Actual termination/relaunch and offline collection have passed on the preceding `3b4e901` candidate; these changes affect only actor placement. Next: build the updated candidate and inspect the three departments again. Physical-device performance, accessibility, Apple-service transactions, signed release validation, and TestFlight delivery remain separate outstanding evidence. The root release task will update this record after those actions complete.
