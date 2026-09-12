# Little Lifeline 3.2 (16) release

**Little Lifeline 3.2 (16) is available to Internal TestFlight testers**, independently confirmed at **12:16:16 UTC on 12 September 2026**. The build is valid and unexpired, with all three beta-text readbacks matching. [Final App Store Connect receipt](../build/qa-clinic-v2/passing-release/asc-final-live.json).

Frozen source **`ea4b905ee230fa7435f3ac3c79816d058950a402`** passed **500/500 Unity tests** and **26/26 packaging tests**. The signed archive and simulator passed independent verification; native run 08 passed one test in 185.174 seconds, and sampled visual review found no actionable blocker. The corrected game also completed the bounded physical smoke check below.

The clinic expansion adds varied patients and street life, an upgradeable car park, toilets and vending tips, individual workstation upgrades, staff training, room construction, partitions and furnished surroundings. See the [feature specification](idle-clinic-expansion.md), [QA record](qa/clinic-expansion.md) and [beta metadata handoff](clinic32-metadata-handoff.md).

## Frozen candidate

| Item | Value |
| --- | --- |
| Source | `ea4b905ee230fa7435f3ac3c79816d058950a402` |
| Unity Git tree | `dd21888da7cf79acec0cd14ddaa3516ebc3f8ec4` |
| Source inventory | 444 files; SHA-256 `b288a8f33da4ceb178dadbddb351cb5a1dc424b9f5bef783caff77910e74b0b1` |
| Editor / scene | Unity 6000.3.24f1; `Assets/IdleClinic/Scenes/Clinic.unity` |
| App | Little Lifeline; `com.flutterly.gravitile`; App Store Connect app `6786840477` |
| Version / build | 3.2 (16) |
| Full Unity run | 500 passed; zero failed, skipped or inconclusive; 12 September 2026, 11:32:55–11:33:29 UTC |
| Test-report SHA-256 | `30a79913c796e4ec91aadc6f41c8d1ca1edfc0db8792b7a72a64823f1db6b941` |

The [reviewed identity](../build/qa-clinic-v2/passing-release/reviewed-identity.json), [source inventory](../build/qa-clinic-v2/passing-release/final-source.json) and [test summary](../build/qa-clinic-v2/passing-release/final-test-summary.json) bind these values. The complete run contains **352 clinic cases across twelve required suites**, including ten new passing-clearance cases. The packaging gate now requires the passing suite alongside the existing eleven clinic suites.

## Passing correction and replacement builds

Recorded patients travelling in opposite directions previously intersected below the care doorway. Routes now use separate directional lanes through the shared corridor and waiting doorway; the waiting sliding door is **1.40 metres wide**. Workstation, seat and parking anchors and authoritative service times remain unchanged. Six regression cases failed before the correction; the final focused run passed **83/83**. Native run 08 and its sampled visual review now cover the corrected routes.

| Build stage | Current evidence |
| --- | --- |
| Simulator export | SHA-256 `9b05f5b1a95f043a67064628dd5a36222e0370d5c4b97a466b3d2017f50ce43f` |
| Simulator CI | [Run 34691595515](https://github.com/its-me-anoop/gameon/actions/runs/34691595515) succeeded; [static verification](../build/qa-clinic-v2/passing-release/simulator-run-34691595515/verification.json) and [installation/save-preservation receipt](../build/qa-clinic-v2/passing-release/simulator-install.json) passed. Native run 08 **PASS** |
| Device export | SHA-256 `d8dbc22b8c2f8aaba166fffee2bb9804c2356b2fd2fd956074af3f8977078fcc` |
| Signed archive | [Run 34691733820](https://github.com/its-me-anoop/gameon/actions/runs/34691733820) attempt 2 **PASS**, upload job skipped. [Independent verification](../build/qa-clinic-v2/passing-release/archive-run-34691733820/verification.json) confirms exact source/report, signature and intended-iPhone eligibility |
| Promotion | [Run 34692759452](https://github.com/its-me-anoop/gameon/actions/runs/34692759452), attempt 2 **PASS**; the same sealed archive was uploaded without rebuilding. [Verification](../build/qa-clinic-v2/passing-release/promotion-run-34692759452-attempt-2/verification.json) |
| TestFlight | Uploaded 12:12:34 UTC; processing `COMPLETE`, build `VALID`, unexpired, `IN_BETA_TESTING`, explicit Internal membership; no processing errors or warnings |
| ASC build / Internal group | Build `4c8e6d64-88da-439f-9611-a1382946b4fe`; group `a1098f67-cfde-4eaa-8626-e250f881596a` |

[Current candidate status](../build/qa-clinic-v2/passing-release/current-candidate.json) tracks the replacement builds. Simulator verification checked exact source/tree, arm64 binaries, all thirteen bridge exports and required privacy reasons; installation retained all three save-file hashes. Its sealed ZIP SHA-256 is `9b99f390d9ce102ef7db5095406b054b26f6fee7ce49c737b4014c40a5fb13e2`. The signed archive SHA-256 is `500922d83cc07dd12c1279d977dfdbc2bd926652b0586f1ea2d4d5421b163a4a`. Both apps use released Xcode 26.6 (17F113) on macOS 26.6.2 (25G83). The Device app is a non-development Unity build with a valid development profile; the simulator is unsigned.

Archive attempt 1 failed at Apple's certificate limit ([failure receipt](../build/qa-clinic-v2/passing-release/archive-run-34691733820-attempt-1-failure/failure.json)). The task-created development certificate `WA87JY7K6N`, which signed only the discarded `200b466` CI archive in the available evidence, was retired: DELETE 204 and independent GET 404; every other certificate remained unchanged. [Retirement receipt](../build/qa-clinic-v2/passing-release/signing/retirement-result.json). That old archive's development profile is now invalid, so its backup is historical evidence and cannot be installed with the original profile. The unsigned simulator is unaffected. No earlier archive substitutes for this frozen source.

## Current runtime evidence

[Native run 08](../build/qa-clinic-v2/corridor-native08/native-verification.json) executed exactly one XCTest with no failures, checked camera gestures, separate 44-point collection targets and unchanged-wallet repeat taps, then verified save/relaunch. Fifty checksummed save observations passed their invariants. The 215.382-second [recording](../build/qa-clinic-v2/clinic32-corridor-passing.mp4) and [visual review](../build/qa-clinic-v2/corridor-native08/visual-review.md) show articulated walking, observed doorway clearance and a bay-to-street vehicle exit in the sampled windows. [Current gameplay still](../build/qa-clinic-v2/corridor-native08/final-parking-overview.png). This short follow-up did not observe a complete new same-driver parking lifecycle or every opposing doorway combination.

The beta description/review notes were applied at 12:02:03 UTC and build testing notes at 12:15:47 UTC. The final 12:16:16 UTC readback confirms all three match the [metadata handoff](clinic32-metadata-handoff.md). [App-text receipt](../build/qa-clinic-v2/passing-release/asc-beta-text-applied.json), [build-notes receipt](../build/qa-clinic-v2/passing-release/asc-build-notes-applied.json). The corrected signed app completed a **122.218-second physical HUD smoke check** on A19 Pro / iOS 27.0. Four captures showed current 59.99 FPS, 16.67 ms frame interval and nominal thermal state; startup displayed a 30.25 FPS minimum. [Physical receipt](../build/qa-clinic-v2/physical-native08/physical-verification.json). The HUD was removed on normal relaunch. This short passive check does not establish sustained performance or physical gesture coverage.

## Delivery identity

| Beta field | Resource ID | Text SHA-256 |
| --- | --- | --- |
| Description | `3b4b5dbe-e68b-4298-ae55-e70c59c8a285` | `a6b7b7a326b3fb0fddbe29a067bad7279f713f429e11e61c3655fbdbc3945f3e` |
| Review notes | App `6786840477` | `029b59c116b793a7be3f71b42cd50a3df93bf4c35996624df3ef53b96e9bd673` |
| What to Test | `dc1d5488-9a85-42b7-861c-440fc0638ff4` | `5ef954696b7f18d828ed889e010b64761db683cbdd7aff25d954c81cd3824418` |

Source provenance comes from the verified archive and promotion checks; App Store Connect does not return a source commit. Its independent readback establishes the exact app/version/build, processing, Internal membership and metadata. No external beta or App Store review submission was performed.

## Historical evidence and remaining checks

The previous `200b466` candidate completed native run 07b: **one test in 242.096 seconds**, with 113 conserving save observations and a complete same-driver parking journey. Video review confirmed articulated walking and vehicle movement but exposed the corridor overlap that prompted this replacement. Its Core simulation and parking presentation are unchanged in the new candidate; the earlier run does not establish that the new pedestrian routes render correctly. Observed exit waits reached **313.4 seconds** before eventual departure.

Historical evidence remains available through the [native receipt](../build/qa-clinic-v2/parking-native07/native-verification.json), [visual review](../build/qa-clinic-v2/parking-native07/visual-review.md) and [overlap still](../build/qa-clinic-v2/parking-native07/visual-review-frames/video-237.880s.png). The full recording and old signed archive were backed up to an unpublished draft with exact hashes before local reclamation: [video restore manifest](../build/qa-clinic-v2/traffic-release/reversible-reclaim-200b/video200b/prepared-and-restore.json), [archive restore manifest](../build/qa-clinic-v2/traffic-release/reversible-reclaim-200b/archive200b/prepared-and-restore.json).

- **DELIVERED:** the exact sealed archive is in Internal TestFlight; processing, availability and all beta text readbacks passed. Promotion attempt 1 failed with an Xcode format error; [its evidence](../build/qa-clinic-v2/passing-release/promotion-run-34692759452/verification.json) is preserved, and attempt 2 succeeded using the same archive.
- **NOT ASSESSED:** sustained physical performance/thermal behavior, physical gesture coverage and actual VoiceOver operation. The short HUD sample is documented separately above.
- **NOT RERUN:** the zero-money opening and complete guided economy timing on this expansion candidate; recent native checks use the existing clinic save.

Historical run 07 selected zero tests and is **NOT RUN**. The older `0b60` physical smoke is not evidence for this candidate. The [3.1 release](idle-clinic-release.md) remains separate. New monetisation and leaderboards remain deferred; authorized TestFlight delivery does not include external beta or App Store review submission.
