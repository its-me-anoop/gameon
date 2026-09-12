# Little Lifeline 3.2 (16) release

**Delivery held for the requested parking, walking and wall fixes.** The archive below is an intermediate verified snapshot, not the final candidate and not uploaded to TestFlight. After its native checks passed, visual review and user feedback expanded the work to proper vehicle entry/parking/exit, distance-driven walking and joined walls/doors. The [3.1 release](idle-clinic-release.md) remains historical. Feature rules are in the [expansion specification](idle-clinic-expansion.md), validation is in the [3.2 QA record](qa/clinic-expansion.md), and beta copy is in the [3.2 metadata handoff](clinic32-metadata-handoff.md).

## Intermediate verified identity

| Item | Value |
| --- | --- |
| Source commit | `0b60f86d058d68617dbcd68d36d435d1a1ac7b32` |
| Unity Git tree | `db7297f64fa89e985a6520f6dc63433a40a78727` |
| Unity file inventory | 424 files; SHA-256 `eec1df7d6fe52aa2090def1e7b5a2e64301445be9b587a8c6648dc2822eecace` |
| Unity Editor | 6000.3.24f1 |
| App | Little Lifeline, `com.flutterly.gravitile`, App Store Connect `6786840477` |
| Version/build | 3.2 (16) |
| Scene | `Assets/IdleClinic/Scenes/Clinic.unity` |
| Full Unity report | 383 passed, zero failed/skipped/inconclusive; 12 September 2026, 09:47:55–09:48:06 UTC |
| Report SHA-256 | `df36c1530fe595eb5685b99a099891c84f6f4e3ac48bea58e8555ddf3dbe552c` |
| Production export | 209,186,375 bytes; SHA-256 `50c613e746e96a7c24f76d1042523d9414f1a8564ec41a7c8dac02a746c9a030` |
| Production transfer | Private draft `unity-export-clinic32-0b60f86d058d-b16`; target, size and digest independently read back |
| Signed archive run | [34686954582](https://github.com/its-me-anoop/gameon/actions/runs/34686954582), **SUCCESS**; Xcode 26.6 (17F113), macOS 26.6.2 (25G83) |
| Sealed archive SHA-256 | `6c840d326534c858ffa9d668abf02eaa67e12a38c6b521f0deae46e4b0fda6ee` |
| Simulator export | 178,079,832 bytes; SHA-256 `b032c88ae41041e5c12340b672e872f470b4cc5e8ccdb36f3ce6c09d273ed34d` |
| Simulator transfer | Private draft `unity-export-clinic32-sim-0b60f86d058d-b16`; target, size and digest independently read back |
| Simulator build run | [34687080514](https://github.com/its-me-anoop/gameon/actions/runs/34687080514), **SUCCESS** |
| Simulator app ZIP SHA-256 | `edf265b826ce2c29c1eebe081d1e92df3f6d0d4ab31e09a5fe95f7577d092639` |

Both exports report a successful build, the exact source commit and a clean source tree. Production explicitly uses the Device SDK with development disabled. Simulator diagnostics explicitly use the Simulator SDK with development enabled. The complete source file inventory was unchanged after export. The simulator success record is written only after temporary SDK settings are restored; the build fails if the source commit changes during export.

The simulator dispatch path cannot run the production archive or upload jobs. Its source-executing job has read-only repository permission and no signing secrets. The final simulator player was installed over the existing 3.2 clinic derived from a real 3.1 save; its installed executable and UnityFramework hashes match the independently verified artifact. Native run 06 passed for this intermediate source; the later parking, walking and wall fixes require their own build and native evidence.

Independent archive verification passed the complete checksum, strict deep signature verification, all thirteen Apple bridge exports, privacy declarations, Game Center entitlement, exact scene and version, clean non-development Device provenance, and final Unity test report. [Archive verification](../build/qa-clinic-v2/hosted-archive/verification.json) and [simulator verification](../build/qa-clinic-v2/hosted-simulator/hosted-verification.json) retain the evidence. This archive is historical evidence only and must not be promoted as the corrected candidate.

## Delivery and limits

Promotion, App Store Connect processing, Internal group membership and exact testing-note readback are **PENDING**. Upload authorization does not include external beta or App Store review submission. New monetisation and leaderboards remain deferred.

The physical iPhone reconnected and was unlocked. The exact signed archive app was installed and launched over the existing clinic without resetting its save. The process remained running for 142 seconds and screenshots confirmed the clinic was visible. Instruments could not attach to the running game; physical frame timing and thermals remain **NOT ASSESSED**. Actual VoiceOver operation is also **NOT ASSESSED**. Simulator evidence is not physical-device evidence. Unrelated native changes, the old campaign and verified purchase ownership are preserved.
