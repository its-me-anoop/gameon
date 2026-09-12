# Little Lifeline 3.2 (16) QA

**Release held:** native video review of `200b466` verified driving and articulated walking, but found opposing patients intersecting in the main corridor. A presentation correction and replacement builds are in progress. The source and artifacts below are the tested intermediate snapshot and must not be uploaded as the final fix.

Current source **`200b4668897d9537009b436aabcb2d2fbcd85f72`** passed **490 Unity EditMode tests**, **47 managed Core tests**, **26 packaging tests**, static verification of both built apps, and native simulator run **07b: one test in 242.096 seconds**. Promotion/App Store Connect availability and actual animation/video review remain **PENDING**. The current-candidate physical check is **NOT RUN**, blocked by the locked phone. This record does not claim TestFlight delivery.

## Source, build and test evidence

| Check | Result and evidence |
| --- | --- |
| Frozen source | Unity tree `ee7899f4cb446517746f20326fe655e85e2a2b35`; 442-file inventory digest `0b92db265285e918ad1287e6b64251602bebe5893cdb642d834a3e174926a7e6`. [Candidate identity](../../build/qa-clinic-v2/traffic-release/current-candidate.json) |
| Full Unity run | **490/490 passed**, zero failed/skipped/inconclusive, 12 September 2026, 10:56:10–10:56:45 UTC. [NUnit XML](../../build/clinic-v2-traffic-final-tests.xml), SHA-256 `ff159414a8cebc75c2cc2e161844a8b87c8b820afac2f1a735cfabbdc616c9b3`; [fixture counts](../../build/qa-clinic-v2/traffic-release/final-test-summary.json) |
| Managed Core validation | **47/47 passed**, zero failed/skipped. [Results](../../build/qa-clinic-v2/capped-traffic-clock/results.xml), [log](../../build/qa-clinic-v2/capped-traffic-clock/run.log). No Unity rendering, native JSON or Apple APIs run by this check |
| Packaging | **26/26 synthetic tests passed**, including rejection when any newly required parking/walking/architecture suite is missing. [Archive CI log](../../build/qa-clinic-v2/traffic-release/archive-run-34689982012/archive-job.log). Synthetic archive fixtures do not establish gameplay |
| Signed Device app | [Run 34689982012](https://github.com/its-me-anoop/gameon/actions/runs/34689982012) succeeded with upload skipped. [Verification](../../build/qa-clinic-v2/traffic-release/archive-run-34689982012/verification.json) checks source/tree/version/scene, sealed archive, embedded 490-test digest, strict/deep signing and intended iPhone eligibility |
| Simulator app | [Run 34689923416](https://github.com/its-me-anoop/gameon/actions/runs/34689923416) succeeded. [Verification](../../build/qa-clinic-v2/traffic-release/simulator-run-34689923416/verification.json) binds source/tree and binary digests; its receipt does not embed the Unity test digest |

The full run contains **342 clinic cases across all eleven required suites**; the remaining cases cover preserved earlier game modules. Coverage includes parking bay ownership through departure, serialized vehicle maneuvers, paid-patient reservations, atomic currency, individual upgrades, room limits, migration/recovery, gesture cancellation, camera bounds, 0.30m route clearances, joined architecture and normal/reduced-motion walking. Native JSON cases verify old saves with no `PausedTrafficTicks` field and persistence without applying the pause twice.

Two reproduced scheduling regressions were corrected before this source freeze: parking eligibility boundaries are explicit simulation events, so frame-sized and coarse/offline updates produce identical saved fields and event sequences; time beyond the eight-hour earnings cap pauses the shared traffic clock while construction retains absolute elapsed time. The active-departure capped-offline regression also checks reload and subsequent crossing eligibility.

Both built apps use released Xcode 26.6 (17F113), macOS 26.6.2 (25G83), arm64, all thirteen Apple bridge exports and the required UserDefaults/FileTimestamp privacy reasons. The Device Unity build has development disabled and a valid development provisioning profile; the unsigned simulator build intentionally enables diagnostics. Static signing/provenance is separate from native runtime evidence.

## Current-source native simulator run 07b

Device: dedicated **iPhone SE (3rd generation), iOS 26.5 (23F77), 375 × 667 points**. The [installation receipt](../../build/qa-clinic-v2/traffic-release/simulator-install.json) binds source `200b466`, version 3.2 (16), installed executable SHA-256 `15b45dd68a3b2e463f50a6c16b1e0256827767a9cb3b468f5710b76d17778bc4` and UnityFramework SHA-256 `5842dbc07c3cc78e98b18a1ca362502cd4794f15f64f1e8ba3c2a55430ca92ce`. Primary save, backup and preserved 3.1 migration file hashes are unchanged by installation.

**Exactly one XCTest passed in 242.096 seconds**, zero failures/skips/runtime warnings: [summary](../../build/qa-clinic-v2/ios-smoke/07b-summary.json), [raw log](../../build/qa-clinic-v2/ios-smoke/07b-parking-walls-walking.log), [result bundle](../../build/qa-clinic-v2/ios-smoke/07b-parking-walls-walking.xcresult), [50 screenshot/accessibility attachments](../../build/qa-clinic-v2/ios-smoke/07b-attachments/manifest.json). The local XCTest harness host is separate from the released toolchain that built the app.

Actual input checked:

- Panning, anchored pinch, Home restoration, room selection preserving camera position, and cash remaining usable after gestures.
- Distinct, nonoverlapping cash targets at least 44 points wide/high. Each desk credited **60 coins**. First-attempt repeat taps preserved wallet **65,750 → 65,750** and **65,810 → 65,810**; legitimate refills are treated as inconclusive retries by the harness, not duplicate-money failures.
- Existing level-3 parking controls, a timed 120-second parking view, room/entrance close-ups, toilet/vending controls and normal/reduced-motion walking captures.
- Background/resume and terminate/relaunch, with wallet **66,905** and reduced-motion preference retained. Offline income remained available for collection.

The completed read-only observer validated **113 checksummed snapshots**, **six conserving collection transitions**, six entry intervals and seven exit intervals, with **no failures**. It observed all four vehicle phases and the complete lifecycle of **driver 1089, bay 1**: entry, clinic care, return, vehicle exit and removal after the recorded deadline. [Final native verification](../../build/qa-clinic-v2/parking-native07/native-verification.json), [observer summary](../../build/qa-clinic-v2/parking-native07/parking-summary.json) and [raw observations](../../build/qa-clinic-v2/parking-native07/parking-live.jsonl) retain the evidence.

The observer ran **555.792 seconds**, 11:11:09–11:20:25 UTC, including the zero-test prelude and a separate post-tour extension. This is not the XCTest's 242.096-second duration. Exit waiting was long: driver 1089 waited **278.5 seconds**; the longest completed wait observed was **313.4 seconds** for driver 1074. Both eventually departed without losing their reserved bay. This proves eventual completion in the recorded session, not short or bounded exit latency under every workload.

The finalized [recording](../../build/qa-clinic-v2/clinic32-parking-walls-walking.mp4) is **559.226667 seconds**, 750 × 1334, H.264, 233,052,136 bytes; SHA-256 `1064f6c9d95f649d45c20a6a848170b7fc8ab3ea933482117c341d8fa1d34d87`. Actual animation/video review is **PENDING**. Sampled saves cannot prove every wheel turn, footstep, short phase or intermediate clearance; those require review of the recording.

**Run 07 is NOT RUN:** its selector matched zero tests. The [preserved log](../../build/qa-clinic-v2/ios-smoke/07-parking-walls-walking.log) explicitly reports zero executed cases; an empty selection's success status is not acceptance evidence. Run 07b used the identifier actually discovered by the rebuilt harness and executed the method.

## Limits and historical context

- **NOT RUN:** current-candidate physical check, blocked by the locked phone at the latest 11:19 UTC attempt. No further run is planned unless the phone is unlocked. Physical FPS, frame intervals, GPU time, thermal behavior and actual VoiceOver operation are **NOT ASSESSED**. Simulator accessibility assertions are not a VoiceOver test.
- **NOT RERUN:** the zero-money opening and complete guided economy timing on this expansion candidate. The current native tour continues the existing developed clinic.
- **PENDING:** promotion and independent App Store Connect processing, expiry, Internal group membership and exact testing-note readback. See the [release record](../idle-clinic32-release.md) and [metadata handoff](../clinic32-metadata-handoff.md). New monetisation and leaderboards remain deferred.

Historical migration/progression evidence is retained separately: the [real 3.1 save receipt](../../build/qa-clinic-v2/legacy-3.1/receipt.json) records wallet 3,807 before migration; earlier runs established purchases and room construction on that continuing save. Intermediate `0b60` run 06 and its [money/construction review](../../build/qa-clinic-v2/hosted-archive/final-native-review.json) do not prove this candidate's runtime behavior. The earlier signed `0b60` app's 142-second physical smoke is also historical only, with no measured FPS or thermals. The [3.1 QA record](idle-clinic.md) remains unchanged.
