# Little Lifeline 3.2 (16) release

**Release held:** native video review of `200b466` verified driving and articulated walking, but found opposing patients intersecting in the main corridor. A presentation correction and replacement builds are in progress. The source and artifacts below are the tested intermediate snapshot and must not be uploaded as the final fix.

The corrected clinic candidate is **built and statically verified**, and native simulator run **07b passed**. Promotion and independent App Store Connect/Internal TestFlight confirmation remain **PENDING**. This record concerns source `200b4668897d9537009b436aabcb2d2fbcd85f72`; earlier 3.2 artifacts are historical and must not be promoted in its place.

The candidate adds durable car entry/parking/exit, walking driven by travelled distance, joined walls and working doors, and the clinic expansion described in the [feature specification](idle-clinic-expansion.md). Parking scheduling is deterministic across frame-sized and offline updates. A persisted traffic pause offset keeps departures synchronized with crossings after the eight-hour earnings cap; construction retains the full elapsed clock. See the [QA record](qa/clinic-expansion.md) for evidence and limits, and the [metadata handoff](clinic32-metadata-handoff.md) for beta copy.

## Candidate identity

| Item | Verified value |
| --- | --- |
| Source | `200b4668897d9537009b436aabcb2d2fbcd85f72` |
| Unity Git tree | `ee7899f4cb446517746f20326fe655e85e2a2b35` |
| Unity inventory | 442 files; SHA-256 `0b92db265285e918ad1287e6b64251602bebe5893cdb642d834a3e174926a7e6` |
| Editor / scene | Unity 6000.3.24f1; `Assets/IdleClinic/Scenes/Clinic.unity` |
| App | Little Lifeline; `com.flutterly.gravitile`; App Store Connect app `6786840477` |
| Version / build | 3.2 (16) |
| Full Unity report | 490 passed, zero failed/skipped/inconclusive; 12 September 2026, 10:56:10–10:56:45 UTC |
| Test-report SHA-256 | `ff159414a8cebc75c2cc2e161844a8b87c8b820afac2f1a735cfabbdc616c9b3` |

[Candidate identity](../build/qa-clinic-v2/traffic-release/current-candidate.json) and [final test summary](../build/qa-clinic-v2/traffic-release/final-test-summary.json) bind the source and test evidence.

## Verified artifacts and native input

| Artifact | Build and verification |
| --- | --- |
| Signed Device archive | [CI run 34689982012](https://github.com/its-me-anoop/gameon/actions/runs/34689982012) succeeded; upload job skipped. [Independent verification](../build/qa-clinic-v2/traffic-release/archive-run-34689982012/verification.json) passed |
| Device export SHA-256 | `c196fe04c8ee3375a9261c259302e2d64ea5eb228506967ba62105bc54dff8d5` |
| Sealed archive SHA-256 | `9ecef6cd247eb01f93c8520302b1a8c968b50c8745b3c977c1f2f6ad7f281df5` |
| Simulator player | [CI run 34689923416](https://github.com/its-me-anoop/gameon/actions/runs/34689923416) succeeded. [Independent verification](../build/qa-clinic-v2/traffic-release/simulator-run-34689923416/verification.json) and [installed-binary/save-preservation receipt](../build/qa-clinic-v2/traffic-release/simulator-install.json) match |
| Simulator export SHA-256 | `ac42f570736fabf95ba6d4bc2c70e123e5525d1c93b042e1269d4e3189707bbb` |
| Sealed simulator app SHA-256 | `fe57c2c547461100c4b8cfb7eb9b6fb5720ba1517645877d66d56e8c2d46421f` |
| Native simulator run 07b | Exactly one test passed in **242.096 seconds**, zero failures/skips/runtime warnings. [XCTest summary](../build/qa-clinic-v2/ios-smoke/07b-summary.json) and [raw log](../build/qa-clinic-v2/ios-smoke/07b-parking-walls-walking.log) |

Both apps are arm64 and were built with released Xcode 26.6 (17F113) on macOS 26.6.2 (25G83). Static verification checked safe ZIP paths, wrapper/sealed digests, exact source/tree/version, all thirteen Apple bridge exports, and required privacy reasons. The Device archive is a non-development Unity build with a valid development signing profile, strict/deep signature verification, Game Center entitlement and confirmed eligibility for the intended iPhone. The simulator intentionally enables development diagnostics and is unsigned. The archive embeds the verified test-report digest; the simulator's test binding is the locally reviewed report plus exact source/tree, not an embedded report digest.

The release packaging gate requires eleven clinic suites: simulation, profiles, world, HUD, performance, expansion, migration, parking flow, walking, architecture and parking world. **26 packaging tests** and **47 managed Core tests** passed. Run 07b used actual simulator input for camera gestures, separate 44-point cash targets, repeat taps, existing parking/amenity controls and background/relaunch. The [final native receipt](../build/qa-clinic-v2/parking-native07/native-verification.json) records 113 conserving save snapshots, six collections and one complete same-driver parking lifecycle. Observed exit waits reached **313.4 seconds** before eventual departure; no bay reservation was lost. Actual animation/video review remains **PENDING**; saved state alone cannot prove every frame.

## Delivery and remaining checks

- **PENDING:** promote this exact sealed archive, confirm App Store Connect processing, unexpired 3.2 (16), Internal group availability and exact testing-note readback. No upload or availability is claimed by this record.
- **NOT RUN:** current-candidate physical iPhone check, blocked by the locked phone at the latest 11:19 UTC attempt. Physical FPS, frame times, GPU time and thermal behavior are **NOT ASSESSED**. Actual VoiceOver operation is **NOT ASSESSED**.
- **NOT RERUN:** the zero-money opening and complete guided economy timing on this expansion candidate; native acceptance uses the existing clinic save.

Run 07 selected zero tests and is **NOT RUN**, not a pass. Older `0b60` native and 142-second physical smoke evidence is historical only. The [3.1 release](idle-clinic-release.md) remains separate. New monetisation and leaderboards remain deferred. Authorized TestFlight delivery does not include external beta or App Store review submission.
