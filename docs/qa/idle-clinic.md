# Idle clinic candidate verification

Candidate: Little Lifeline 3.1 (15), `idle-clinic`, `Assets/IdleClinic/Scenes/Clinic.unity`.

The corrected physical-phone run covers the 267-test snapshot with working-source SHA-256 `50163dac26d131be03f7d8c4b4cf76fa8b33951af749f3f63471e2e81214f788`. Pointer-capture cleanup, mobile accessibility activation and exterior/world files have changed since that run. The updated Unity suite passed 289 tests, but native progression/camera, accessibility and exterior acceptance remain pending. The earlier phone result is not proof of the newer source; a final-source phone retest is pending.

| Evidence | Status | Scope |
| --- | --- | --- |
| Clinic persistence/profile-test C# compilation | PASS | Against installed Unity 6000.3.24f1 assemblies; 18 profile tests executed in Unity EditMode |
| Clinic model importer C# compilation | PASS | Scoped Legacy animation import settings |
| Native Apple bridge Swift 6 / ObjC++ compilation and linkage | PASS | Thirteen exports including UIKit point sizing and thermal state; also verified in the corrected signed physical-phone UnityFramework. StoreKit restore remains separate |
| macOS native scroll plugin | PASS | arm64/x86_64 build and signature; synthetic local NSEvent precision transitions and lifecycle; physical Unity input not yet assessed |
| Workflow static validation | PASS | actionlint on release.yml |
| Synthetic package validation | PASS | 25 tests; explicit Device SDK and production marker, all five clinic fixtures, hashes, unsafe paths, disk guard and output preservation |
| Full Unity import and EditMode suite | PASS | 289/289 passed on 12 September 2026 at 08:05:16 UTC, including capture dispatch, exterior clearance and live accessibility cash/hidden-frame cases; build/clinic-tests.xml and build/clinic-tests-cash-accessibility.log. Final committed-source rerun remains a release gate |
| New clinic source compilation across UI and world | PASS | Current neighbourhood source compiled all 20 branches and passed 89 selected managed tests; build/clinic-source-check-neighbourhood.log. This does not replace native runtime acceptance |
| Clinic Core managed NUnit | PASS | 20 tests executed outside Unity; no rendering or JSON native engine coverage |
| Eight-hour income / full-clock construction | PASS, simulation | Profile regressions executed in Unity; device background/resume remains separate |
| New clinic simulator | CURRENT FULL ACCEPTANCE PENDING | Earlier SE run reached nurse at 27.1283s and equipment at 102.8369s; panning and anchored zoom were observed. Home, selection and post-pinch cash failed due to captured-pointer cleanup; earlier marker-only assertions gave a false positive for those actions. Targeted capture diagnostic passed. Repeat complete progression/camera flow on current source |
| VoiceOver / StoreKit restore on signed iOS build | NOT ASSESSED / NOT RUN | Earlier simulator hierarchy exposed wallet, guidance and point-sized controls. Mobile action activation has since changed; native accessibility retest and actual VoiceOver interaction remain pending. No signed restore result is claimed |
| Recorder source/percentile validation | PASS | Three managed percentile/ring tests; corrected staffed phone sample: 120.0195s / 7,194 frames, last-window median16.6674ms, p95 16.7345ms, thermal nominal, peak sampled Unity allocation74.09MiB (not process footprint) |
| Dedicated iPhone development signing profile | PASS | Created/installed ACTIVE matching app, phone and existing valid certificate; actual app signature/profile and all13 native symbols verified |
| Physical iPhone development export/build/install/launch | PASS, 267-test snapshot | 3.1(15), Unity Development export compiled with Xcode Release/dwarf; source50163… and exact profile/signature verified; same bundle installed without data reset |
| Physical iPhone visual/performance check | PASS, bounded earlier snapshot | Corrected startup/running frames show reception, care and no earlier collider-error console. Existing staffed save passed checksum, Core validation and financial conservation. No physical taps, gestures, restore or VoiceOver were exercised; newer UI/exterior changes are outside this result |
| Production Device export, signed archive and TestFlight | NOT RUN | Previous train release and diagnostic development app do not validate this clinic release candidate |

Persistence regression coverage: first-arrival tutorial gating; first payment collection/hire checkpoint; complete state and preferences; same-core offline parity; eight-hour income and uncapped construction clock; construction restart/no repeat charge; backward clock; active-save accounting; failed offline commit/retry; pending interval retained after clock rollback; failed active save; stale snapshot rejection; corrupt backup recovery; checksum failure; abandoned temporary file; preference-only migration; legacy backup fallback; migration precedence.

Use the [release handoff](../idle-clinic-release.md) for exact scene, build, package, draft-transfer, archive and processing steps. Keep source/build/test evidence separate from runtime/device proof and update this table only with actual results.

## Runtime corrections before release

The first simulator pass exposed a clipped first reception counter and standing patients using the seated animation. Home now fits the starter clinic to the viewport, and the character pose follows the authored destination socket. Eight additional renderer regressions cover standing positions, portrait framing and persistent camera position.

Unity native accessibility requires reactivation whenever the screen reader is enabled, and layout-change notifications when nodes or frames change. Development builds accept the `CLINIC_ACCESSIBILITY_QA=1` launch environment variable to export the same nodes for native test inspection without enabling the operating system screen reader. The optional `-clinic-accessibility-qa` argument is also parsed, but did not propagate through the tested IL2CPP simulator launch; the environment variable was verified. This diagnostic override is absent from release builds. References: [Unity activation contract](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Accessibility.AssistiveSupport-activeHierarchy.html), [hierarchy update contract](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Accessibility.AccessibilityHierarchy.html), [diagnostic override](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Accessibility.AssistiveSupport-screenReaderStatusOverride.html).

The existing simulator clinic and its earlier campaign save are preserved. A separate iPhone SE simulator provides an isolated fresh save for opening timings and small-screen layout checks.

The recorded opening exposed an oversized small-screen panel. The revised dock uses bounded 230-point rows, leaves controls at least 44 points, positions camera controls beneath the wallet after onboarding, and removes occluded world markers from accessibility. The simulator was rebuilt after this fix; current full guided acceptance remains separate from the earlier opening evidence.

A native double tap on the very first cash pile collected exactly 50 coins, but its second release selected reception and hid the first-nurse panel. Tutorial room selection is now guarded until onboarding completes; all 15 tutorial/room combinations and a repeated-collection simulation regression passed in the 267-test suite. A later native opening reached the nurse in 27.1283 seconds after repeating that double tap.

Subsequent native investigation found that captured pointer releases skip ancestor callbacks. The latest source listens at each capture target so a completed control tap or pinch cannot leave a stale finger that blocks later controls; new real-panel dispatch regressions cover cleanup and cancellation. Mobile accessibility no longer adds a second direct action when the platform already synthesizes activation. The 289-test Unity suite passed after these changes and the latest exterior/door dressing; the native retest remains pending. A targeted capture diagnostic passing does not establish complete acceptance. Native virtual nodes can remain queryable when inactive; cash now exports its authoritative coin amount, and hidden nodes export a zero rectangle plus disabled state. Native collection assertions require positive cash rather than assuming that query existence means a payable counter.

The corrected phone report, screenshots, save checks and exact limitations are in [the physical-device record](clinic-iphone-performance.md). Its local diagnostic app bundle was removed after installation and evidence collection to reclaim build space; the installed app and save remain. Runtime stdout was not captured, so the absence of collider errors is based on the two inspected frames.

## Release status

Read-only App Store Connect check at **07:54:25 UTC on 12 September 2026** found neither version 3.1 nor build 15 among all builds and build uploads. The Internal group still has access to all builds. Beta description and reviewer notes still describe the previous train game and must be replaced with the committed clinic copy before exposing this release. No clinic metadata changes, production archive or TestFlight upload have occurred. Use the scoped commands in [the metadata handoff](../clinic-metadata-handoff.md) only after final candidate acceptance.

Current 289-source native run (working-source digest `fd457b9aba187c1c2cad316b20552fbe664a7cf1a9db0cedc32dd8b8e1b8e85a`) passed first-nurse27.1994s, first equipment110.4478s and the strengthened camera sequence at128s. The test checks both room marker positions after Home, actual selection controls, and a credited positive cash pile after pinching. Full progression is still running at source freeze; no TestFlight availability is claimed yet.
