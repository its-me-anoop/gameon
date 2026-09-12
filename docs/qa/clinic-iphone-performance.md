# Clinic iPhone development validation

This is local device QA, separate from the release archive. The corrected development candidate was built, signed, installed over the existing app and run on the physical iPhone on 12 September 2026. Its two-minute staffed-clinic sample and two inspected frames passed the bounded visual/performance check. Physical gesture, purchase-restore and VoiceOver behavior were not exercised. The earlier missing-collider diagnostic is retained below as historical evidence.

On 12 September 2026, the paired iPhone 17 Pro Max (identified in the local `build/qa-clinic/iphone-details.json`) reported iOS 27, developer mode enabled, DDI services available and local-network connectivity. Local Xcode is 27.0 (27A266a). Keep the phone unlocked and reachable for installation; recheck connectivity before beginning. Local beta/RC tooling is used for development validation; TestFlight still promotes the separately sealed archive produced by the hosted release workflow.

## Signing readback

The existing exact-bundle development profile included this phone but lacked the usable local certificate. No existing active App Store Connect development profile matched all three of the bundle, device and certificate. The authorized dedicated profile was therefore created and installed:

- Name: **Little Lifeline Clinic QA 2026-09-12**
- ASC resource: `WRLMFMPXJ5`
- UUID: `4d90305f-e964-45d4-803d-e4cbe017fd71`
- State: ACTIVE; expires 12 September 2027, 06:37 UTC
- App identifier: `K6623R3GP5.com.flutterly.gravitile`
- Development/get-task-allow: true; Game Center entitlement: true
- Existing certificate: `ZY8GW5VNJY`, SHA1 `061C8BDF060BB91583225BEC6AF7C8107653FC43`
- Target iPhone registration: verified included; its private identifier stays in local QA evidence
- Installed in `~/Library/Developer/Xcode/UserData/Provisioning Profiles/4d90305f-e964-45d4-803d-e4cbe017fd71.mobileprovision`

The profile content was decoded and checked for the exact app/team, phone, local certificate and entitlements before installation. Evidence: `build/qa-clinic/development-profile-readback.json`. No certificates were created/revoked, no previous profiles deleted and no app capabilities changed. The second local identity reported revoked and must not be selected.

## Development export and build

After root adds the recorder following clinic readiness, under `#if DEVELOPMENT_BUILD || UNITY_EDITOR`, export with the dedicated method. It uses `BuildOptions.Development` without script debugging or automatic profiler connection.

```sh
CLINIC_DEVICE_UDID="$(python3 - <<'PY_DEVICE'
import json
from pathlib import Path
p=json.loads(Path('build/qa-clinic/iphone-details.json').read_text())
print(p['result']['hardwareProperties']['udid'])
PY_DEVICE
)"
ORCHARD_IOS_EXPORT="$PWD/Unity/OrbitOrchard/Builds/iOSClinicDevelopment" \
/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD/Unity/OrbitOrchard" \
  -executeMethod OrbitOrchard.Editor.OrchardBuild.BuildIOSDevelopment \
  -logFile "$PWD/build/clinic-development-export.log"

xcodebuild \
  -project Unity/OrbitOrchard/Builds/iOSClinicDevelopment/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone -configuration Release -sdk iphoneos \
  -destination 'generic/platform=iOS' \
  -derivedDataPath "$PWD/build/clinic-device-derived" \
  DEVELOPMENT_TEAM=K6623R3GP5 COMPILER_INDEX_STORE_ENABLE=NO \
  DEBUG_INFORMATION_FORMAT=dwarf -jobs 4 \
  build > build/clinic-device-build.log 2>&1
```

For this machine, automatic signing selected an older Xcode-managed profile that lacked the local certificate. The successful signing preparation pins **only the generated Unity-iPhone app target** to Manual signing, the QA profile name/UUID above and the matching certificate fingerprint. UnityFramework receives no application profile. The original generated project is preserved at `build/qa-clinic/project-before-manual-signing.pbxproj`; the local PBXProject helper is `build/qa-clinic/SelectQaProfile.cs` and its compiled executable. Apply those target settings before the build command above; do not pass global signing overrides that undo them.

This is a development-export adjustment, not a source or capability change. Regenerate or restore the original project before a production export. The development provenance marker and release gates also prevent this diagnostic build from being packaged for TestFlight. No provisioning-update flags are necessary because the profile already exists.

Before installation, select the single resulting `.app` in `build/clinic-device-derived/Build/Products/Release-iphoneos`, then verify its signature and embedded profile. These commands preserve a path with spaces:

```sh
CLINIC_DEVICE_APP="$(python3 - <<'PY'
from pathlib import Path
apps=list(Path('build/clinic-device-derived/Build/Products/Release-iphoneos').glob('*.app'))
assert len(apps)==1, 'Expected one development app bundle'
print(apps[0].resolve())
PY
)"
codesign --verify --deep --strict "$CLINIC_DEVICE_APP"
python3 - "$CLINIC_DEVICE_APP" "$CLINIC_DEVICE_UDID" <<'PY'
import hashlib,plistlib,subprocess,sys
from pathlib import Path
app=Path(sys.argv[1]); info=plistlib.loads((app/'Info.plist').read_bytes())
profile=plistlib.loads(subprocess.check_output(['security','cms','-D','-i',str(app/'embedded.mobileprovision')],stderr=subprocess.DEVNULL))
e=profile['Entitlements']
assert info['CFBundleIdentifier']=='com.flutterly.gravitile'
assert info['CFBundleShortVersionString']=='3.1' and str(info['CFBundleVersion'])=='15'
assert e['application-identifier']=='K6623R3GP5.com.flutterly.gravitile'
assert e.get('get-task-allow') is True and e.get('com.apple.developer.game-center') is True
assert sys.argv[2] in profile['ProvisionedDevices']
assert any(hashlib.sha1(c).hexdigest().upper()=='061C8BDF060BB91583225BEC6AF7C8107653FC43' for c in profile['DeveloperCertificates'])
print('Verified development identity/profile:',profile['UUID'])
PY
```

Only once root begins the coordinated phone run:

```sh
xcrun devicectl device install app --device "$CLINIC_DEVICE_UDID" \
  "$CLINIC_DEVICE_APP" --json-output build/qa-clinic/iphone-install.json
xcrun devicectl device process launch --device "$CLINIC_DEVICE_UDID" \
  --json-output build/qa-clinic/iphone-launch.json com.flutterly.gravitile
```

This installs over the same app identity; do not uninstall or clear app data. The clinic uses its own save and preserves earlier campaign files. Development export provenance contains `developmentBuild=true`; production packaging, archive validation and promotion reject it. Export the final release separately with `BuildIOS`, which writes false. A development performance sample is useful evidence but includes development-build instrumentation overhead.

## Recorder and report extraction

`IdleClinic.Services.ClinicPerformanceRecorder` is opt-in: it has no bootstrap or UI and is excluded from non-development players. It writes `clinic-performance-v1.json` in `Application.persistentDataPath` every 30 active seconds and on pause/disable/quit. No network calls or gameplay/account data are collected. Frame sampling uses Unity's existing unscaled frame delta; only report labels use `DateTime.UtcNow`.

The report includes:

- Session ID, app/Unity versions, capture/start UTC, viewport and target frame rate.
- Total sampled frame count and active elapsed time. First/resume frames are skipped so background absence does not become a hitch.
- Last 3,600 frame samples summarized as median, nearest-rank p95 and maximum frame duration in milliseconds; their count and elapsed span are explicit.
- Unity allocated/reserved memory and managed used memory in bytes at each report. These are Unity counters, not total process physical footprint; zero can indicate an unavailable counter. Lifetime memory peaks are **sampled at report times**.
- Latest iOS thermal state and maximum seen at five-second polls: `-1` unsupported (including simulator), `0` nominal, `1` fair, `2` serious, `3` critical.
- At most 120 report checkpoints, in chronological order. Storage and frame buffers remain bounded; each report replaces the previous file atomically.

For the authorized physical retest, capture no more than two minutes of foreground activity, plus report extraction. Only assess interactions actually performed; the installed device CLI cannot inject touches. Pause the app to force a final write when possible. Copy the report without modifying the app container:

```sh
xcrun devicectl device copy from --device "$CLINIC_DEVICE_UDID" \
  --domain-type appDataContainer --domain-identifier com.flutterly.gravitile \
  --source Documents/clinic-performance-v1.json \
  --destination "$PWD/build/qa-clinic/iphone-performance.json" \
  --json-output build/qa-clinic/iphone-performance-copy.json
```

If the file is absent, use `devicectl device info files` for the same app container and inspect `Documents`; verify this is a development export with the component attached. Check `capturedUtc` and `sessionId` before attributing an older report to a new run. Report actual frame times, memory counter scope and thermal state; do not label simulator or Editor results as physical-device evidence.

The separate `BuildIOSSimulator` method also enables Development mode for accessibility/diagnostic QA. Simulator reports remain host evidence; `isEditor=false` alone does not prove a physical phone run. The simulator thermal getter always reports -1.

Recorder verification: release and development C# branches compiled, three bounded-window/percentile tests passed, thirteen native exports compiled/linked. The physical phone wrote its report successfully. Actual build/signature verification also found all thirteen symbols in the signed UnityFramework.

## Physical diagnostic result — 12 September 2026

The first run began at 06:54:54 UTC and recorded 137.38 active seconds / 8,234 frames before the app left the foreground at 06:57:12 UTC. The last 3,600 frames (60.02 seconds) measured median **16.666 ms**, p95 **16.964 ms**, maximum **28.474 ms**. Thermal state stayed **nominal**. Peak sampled Unity allocation was **81.41 MiB**, separate from total process memory. This was the initial unstaffed tutorial, without automated gameplay input; it does not measure a developed clinic or prolonged thermal load.

The copied clinic save passed its SHA-256 checksum and the actual Core `IsValidState` method. It held tick 1373, one payment, 50 coins in the reception till, zero wallet balance and the collection tutorial step. Financial conservation passed: earned 50 = spent 0 + wallet 0 + till 50; collected 0 = spent 0 + wallet 0. The simulation and save continued despite visible errors. The first game screenshot showed Development Console errors for missing `SphereCollider` and `CapsuleCollider`, plus unresolved initial camera framing. A later capture found the phone had switched to another app; that unrelated image was discarded and further capture stopped. No settled-frame or gameplay acceptance is claimed.

Evidence stays in ignored local `build/qa-clinic`: `iphone-source-identity.json`, `iphone-signature-verified.json`, install/launch readbacks, `iphone-first-frame.png`, `iphone-performance.json`, `iphone-profile.json` and `iphone-diagnostic-summary.json`. The old diagnostic `.app` was later removed to make build space; its identity, signing, installation, screenshots and metrics remain. Its generated DerivedData was also reclaimed. No app data was reset. This superseded run remains a failed visual check.

## Corrected physical run — 12 September 2026

The corrected Unity Device export retained `developmentBuild=true`, `iosSdk=device` and `buildSucceeded=true`. Xcode compiled configuration **Release**, with `DEBUG_INFORMATION_FORMAT=dwarf`, indexing disabled and four jobs. It did not create a separate dSYM bundle. The final production archive keeps its normal symbols and must be exported separately.

The source was still uncommitted over `c81af1e53a89b5ac9826f9e48b82003c4ded582f`. Its recorded working-source SHA-256 was `50163dac26d131be03f7d8c4b4cf76fa8b33951af749f3f63471e2e81214f788`; every recorded file still matched after the native build. The associated 267-test Unity report hash was `125de36699db0327092b858df662c8c3e23ee7adff9228f551f1ce9ec0c31839`. This identifies a development snapshot, not a committed release candidate.

The build, deep signature verification, exact app/profile/certificate check, signed Game Center/development entitlements and all thirteen native symbols passed. The same bundle was installed without uninstalling or resetting its save. The app executable SHA-256 was `2eeb7c7e0c5052f435999c3dafca9500ec66d0d6537287ad6830fdb594f64db6`; UnityFramework was `b98b81d804ff2c9075b217cbcdd1125fe1ac30cfeb16f1efc929932d8ba59145`.

Session `c5c2e270230547fd920f77cf2118aea7` began at **07:42:27 UTC**. Its **07:44:27 UTC** report recorded **120.0195 active seconds and 7,194 frames**. The last 3,600 frames spanned 60.0055 seconds: median **16.6674ms**, p95 **16.7345ms**, maximum **16.9444ms**. Thermal state stayed **nominal**. Peak sampled Unity allocation was **74.09 MiB**; this is not total process footprint. The first checkpoint included a 121.25ms startup hitch, outside the final rolling window.

The existing clinic was staffed, its tutorial complete and waiting room already built. Startup and approximately 96-second screenshots showed reception and its growing till, queued patients, the care doorway and a nurse treating a seated patient. The earlier collider-error console was absent in both inspected images. Default framing included reception and first aid. No physical touches were injected: the installed physical-device CLI has no touch API, so Home-button activation, drag/pinch and care-control interactions were not assessed on this physical run. Earlier simulator Home/selection assertions were later found insufficient; the captured-pointer fix requires a fresh native retest. Runtime stdout was not captured; the console-option attempt was parsed as application arguments, so the error observation is visual rather than an exhaustive log audit.

Read-only save copies from before installation and after the run both passed their SHA-256 checksum, the frozen Core's `IsValidState` and financial conservation. The final state held 17 payments, 13 treatments, completed tutorial, wallet 90 and till 414: **earned 864 = spent 360 + wallet 90 + till 414**, and **collected 450 = spent 360 + wallet 90**. The pre-install snapshot had eight payments, earned 400, spent 270, wallet 130 and empty tills. These snapshots bracket installation and resume as well as the measured run; they are not a controlled no-command economy test.

Evidence uses `build/qa-clinic/corrected-device-*`: source and export provenance, signature and signed entitlements, app Info.plist, build/install/launch readbacks, startup/running screenshots, before/after profile copies and the performance report. After copying the evidence, only this run's generated device DerivedData was removed (approximately **1,854.9 MiB**). The local `.app` was not retained; the installed phone app and its data remain. No further phone interaction followed collection. The generated export still carried manual QA signing settings and must be regenerated or restored before production export.

## Final-source physical retest — pending

The 287-test exterior/capture-fix snapshot exported successfully as a Device Development player. Its working-source digest was `f17565251ab8a61259620bab3459bf49373059b460e45da859b9ad60725c9a85`. The native build exited 70 before compilation because the paired iPhone destination could not become available. A single subsequent read-only `devicectl` check failed with CoreDevice error 4016: trusted connectivity and the required power assertion were unavailable. No app was installed or launched for this attempt.

The export was then superseded by the pending cash accessibility-value and inactive-node-frame correction. Native compilation was held, with its empty DerivedData directory retained for reuse. Evidence is in `build/qa-clinic/final-device-*`, including the successful export, source inventory, failed destination build, availability response and hold receipt. A future build uses `generic/platform=iOS` so compilation does not depend on device discovery; installation still requires a reachable device and a newly verified source export.

Final-source physical visuals, performance and touch behavior remain **NOT ASSESSED**. The corrected 267-source two-minute sample above remains bounded evidence for that earlier snapshot. It does not validate the later exterior, pointer capture or accessibility changes. Final native full-progression, Home/selection and post-pinch collection acceptance are tracked separately in `idle-clinic.md`.

References: [Apple thermal states](https://developer.apple.com/documentation/foundation/processinfo/thermalstate-swift.enum), [Unity allocated-memory counter](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler.GetTotalAllocatedMemoryLong.html), [Apple development profile API](https://developer.apple.com/documentation/appstoreconnectapi/post-v1-profiles). Device command syntax was read from the installed Xcode 27 `devicectl --help`.
