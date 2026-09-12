# Little Lifeline — verification record

Updated 12 September 2026. The verified Unity candidate is **3.0 (14)**, source **`0c82dbf4b13db31812662f2d2b396e416066027e`**. Its full regression suite, simulator workflows, final visual checks and signed archive have passed. **TestFlight 3.0 (14) is VALID, IN_BETA_TESTING and available to the existing Internal group.**

## Product and runtime evidence

The portrait diorama fills the play area. Carriage, crew, map and trophy icons replace the text-heavy management pages; compact focus controls leave care visible. The original Blender train, care equipment and towns are real 3D game assets. Concept images and Blender authoring renders are not gameplay evidence.

| Area | Verified result and scope |
| --- | --- |
| Unity EditMode | **148 passed, zero failed/skipped**, completed 11 September at 23:41:26 UTC. [Report](../../build/lifeline-tests-aisle.xml). Little Lifeline: 31 Core, 18 profile, 49 presentation/HUD cases. Retained Orchard: 50 cases. |
| Imported furniture regressions | Fourteen added cases check the scanner/recovery beds, consultation chair, working/idle staff and platform benches. Eleven exposed actual placement failures before correction. The three already-clear platform positions were retained. All pass after correction. |
| Standalone managed baseline | The source checker compiled 13 assemblies and passed 66 managed cases before the final actor-placement edits. [Summary](../../build/qa-lifeline/source-check/summary.txt). These overlap the Editor suite and do not establish renderer or Apple-service behavior. The final Unity suite and simulator build compiled the updated source. |
| Native bridge | Framework-qualified generated Swift header import reproduced and fixed the actual Xcode failure. The independent Swift/Objective-C++ check compiled and linked all **11 bridge exports**. The signed archive separately verifies those exports in the real UnityFramework. |
| Final iOS player | Exact `0c82dbf` built, installed and launched on **iPhone 17 Pro / iOS 26.5 simulator**, using local Xcode **27.0 (27A266a)**. Build completed in 34.8 seconds with no errors. [Build/log evidence](../../build/qa-lifeline/ios/runtime-build-evidence.json). |
| Building and assignments | Actual XCTest input opened the clinic, selected carriage slots, built Diagnostics and Recovery, assigned Ivo and Nell, and returned to Hospital. Real SHA-256-valid saved profiles independently confirm the room and crew relationships. |
| Final visual check | Three real XCTest cases captured all departments in active care on `0c82dbf`; 54 attachments were exported. Root inspected the actual frames: Consultation resident at the chair/desk, scanner patient on mattress, recovery patient on pillow/bed, clinicians clear in the aisle. |
| Relaunch and offline care | On prior candidate `3b4e901`, genuine app termination, 62 seconds absent, and relaunch preserved all three rooms and Ivo/Nell assignments. Return report showed **nine visits / 284 funds**; Continue restored the overview. The final changes affect only actor placement. |
| Weekly practice | On exact `0c82dbf`, a real four-minute shift ran **4:00 → 2:00 → 0:00**, completed **24 visits**, stored local score **23,986,901** for week `2026-09-07`, and returned to Willowbank. Saved campaign rooms and specialist assignments remained intact; the envelope checksum was valid. XCTest duration: 249.574 seconds. |
| Original assets | Fourteen original Blender FBX assets total 1,514,536 bytes, with bundled Newsreader and Atkinson Hyperlegible font licences and locally synthesized audio. Source: [Blender generator](../../Tools/create_lifeline_assets.py). |

[Detailed simulator record](../../build/qa-lifeline/ios-smoke/README.md) links the exact XCTest bundles, screenshots, saved envelopes and verification outputs. Inputs target only this app. Saves were read for verification, never seeded, patched, reset or time-adjusted. A successful tap or foreground assertion alone is not treated as functional proof.

Stable actual-player images: [overview](../../build/qa-lifeline/ios-smoke/artifacts/final-overview.png), [consultation](../../build/qa-lifeline/ios-smoke/artifacts/final-consultation.png), [diagnostics](../../build/qa-lifeline/ios-smoke/artifacts/final-diagnostics.png), [recovery](../../build/qa-lifeline/ios-smoke/artifacts/final-recovery.png), [weekly completion](../../build/qa-lifeline/ios-smoke/artifacts/final-weekly.png). These belong to the final source, not the earlier concept art.

Earlier direct Unity Editor interaction additionally covered Copperhill travel, Recovery building/Nell assignment, weekly planning and the Sunrise finish preview. That is Editor evidence; simulator travel and real purchasing are not inferred from it.

## Apple services and release

The authorized App Store Connect changes were applied and read back: Little Lifeline app/beta text, the recurring `grv3.lifeline.weekly.v1` board, and the existing Plus cosmetic collection. Existing USA $2.99 price, family sharing and optional tips were preserved. [Readback](../../build/lifeline-provider-readback-20260912.json). The new board opens **14 September 2026 at 00:00 UTC**; the completed current-week shift is local practice and was not submitted.

The final Device SDK package is `build/unity-transfer-aisle/orbit-orchard-ios.zip`, **206,881,027 bytes**, SHA-256 **`5caaa943d34847cf481f1eebc4ad51675ed2a685c6d342422ec28cea940f5aba`**. It records the clean source, correct product/scene and passing 148-case report.

Signed archive [34659231780](https://github.com/its-me-anoop/gameon/actions/runs/34659231780) **passed** on released **Xcode 26.6 (17F113)**. Its checks verified product, scene, bundle, version/build, all 11 native symbols, signature, Game Center entitlement and app-local UserDefaults privacy reason. Signing credential cleanup passed. Upload run [34659787504](https://github.com/its-me-anoop/gameon/actions/runs/34659787504) succeeded using that exact archive without rebuilding. Apple processing completed without errors or warnings. Exact build `2ac69fe0-4b4b-47b3-8939-c35aeeb47d4d` is **VALID**, unexpired, **IN_BETA_TESTING**, and explicitly listed in the existing Internal group. The en-US testing notes exactly match the committed copy. [Final provider readback](../../build/lifeline-testflight-readback.json), verified 12 September at 00:02:48 UTC.

## Known beta limitations and unverified areas

- **Visual staffing edge case:** rested or replacement crew retaining the same room can share an aisle position. The normal one-specialist-per-department configuration is clear. This source-reviewed issue affects visual separation, not care, assignment or saving; normal-UI reproduction was not performed.
- **Accessibility:** XCTest exposed generic application/window/views without individual Unity controls. Coordinate input was required. VoiceOver behavior and a complete accessibility pass are **NOT VERIFIED**.
- **Physical device and iPad:** sustained frame time, memory, thermals, touch latency, physical-device background graphics recovery and iPad layout are **NOT MEASURED**.
- **Apple transactions/services:** actual StoreKit purchase, cancellation, pending state, restore, refund/revocation and prior ownership are **NOT VERIFIED** in this Unity player. Real Game Center authentication, dashboard, score posting/retry and account switching are **NOT VERIFIED**. No account or purchase operation occurred during the simulator tests.
- **Competition:** scores are client-simulated casual results; no server anti-cheat or cash-prize claim. Live weekly submission cannot be established before the September 14 opening.

## Evidence corrections and superseded candidates

The 97-case and 131-case runs are historical. The latter exposed a caption-test issue and station orientation; both were corrected before the 134-case baseline. Actual simulator play then exposed mirrored FBX patient anchors, followed by stationary staff/furniture overlap. Geometry regressions reproduced each before its correction. The final baseline is **148/148**, not an earlier passing report.

One initial clinic tap occurred on Route and made no progress; it is excluded from successful input proof. A later harness selection reported Xcode success with **zero executed tests**; it is excluded from visual proof. The successful final visual run executed three cases with actual attachments. The detailed smoke record also identifies a refreshed assignment snapshot instead of mislabeling it as the earlier revision.

Runs `34657229754` and `34658491720` were cancelled before upload. Run `34657640594` passed the release pipeline but contains superseded actor placement and was never promoted. Only source `0c82dbf` and archive `34659231780` are approved for this release. Generated local QA evidence remains under `build/`; signed CI artifacts have seven-day retention.
