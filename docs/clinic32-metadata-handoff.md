# Little Lifeline 3.2 (16) beta handoff

**Prepared; not uploaded or applied.** This document is the 3.2 candidate's copy and release handoff. The [3.1 release record](idle-clinic-release.md) and [3.1 metadata handoff](clinic-metadata-handoff.md) remain historical evidence. Do not reuse their build 15 identifiers or text-file commands for this release. Feature scope is documented in [the expansion specification](idle-clinic-expansion.md).

Read-only App Store Connect verification at **09:16:39 UTC on 12 September 2026** found no build 16 in either endpoint: 14 upload records and 14 build records, one complete page each. Recheck immediately before upload. App `6786840477` still has bundle `com.flutterly.gravitile`. Existing group `a1098f67-cfde-4eaa-8626-e250f881596a` is `Internal`, is an internal group, and has access to all builds. No provider data was changed by this check.

## Beta description

Use the following block, with outer whitespace stripped, as the en-US beta description:

```text
Build a little clinic with a lot of life.

Welcome patients at reception, collect their payments and hire nurses to provide first aid. Add a waiting room, improve each desk and nursing station, and train individual staff members to work faster. Expand rooms to unlock better equipment, facilities and decorations while care continues.

Grow beyond the clinic with an upgradable car park. Watch cars drive in, park and leave after their patients receive care. Give waiting patients a toilet and vending machine, then collect the tips they leave. A varied cast of visitors, passing traffic and pedestrians brings the neighbourhood to life.

Explore the miniature 3D world by dragging and pinching. Coins fly from their collection point to your wallet, and scaffolding shows room renovations in progress. Your existing clinic continues in this update, including progress made while away.
```

## Beta review notes

Replace only the existing beta review `notes` field with this block; preserve contact details and `demoAccountRequired=false`:

```text
Little Lifeline 3.2 (16) expands the Unity idle-clinic game. No account or sign-in is required. This is an internal TestFlight update; no external beta or App Store review submission is requested.

A new clinic starts with one receptionist. The first patient pays 50 coins. Tap the counter cash, then use the highlighted control to hire the first nurse. Regular arrivals and other purchases unlock after the first treatment. Two paid patients waiting unlock the waiting-room construction option.

Tap a room for shared upgrades and renovation. Tap an individual reception desk or nursing station for its equipment and assigned staff member's training; numbered shortcuts are also available in the room controls. Use the car-park control in the camera toolbar to manage parking. Once the waiting room is built, its controls include shortcuts to the toilet and vending machine. These amenities have three levels; waiting-room tier limits toilet and vending upgrades.

Tips appear at the vending machine after eligible waiting patients use it. Tap its coins or use its collection control. Reception payments and vending tips accumulate until collected. Offline simulation earns for up to eight hours; construction uses the full absence. Services continue during room renovation.

Updating from 3.1 migrates the clinic save, preserving progress and preferences. Older campaign files and verified purchase ownership are preserved. Settings include sound, haptics, reduced motion and restoration of existing purchases. This update adds no new real-money purchases, advertisements or leaderboards.
```

## What to Test

Bind this block only to the independently verified **3.2 (16)** build's en-US `betaBuildLocalizations.whatsNew`:

```text
The clinic has a fuller neighbourhood, more varied people and individual staff and workstation progression.

• Update from 3.1 and check that your clinic money, staff, rooms, waiting patients, preferences and ongoing renovation are preserved.
• Upgrade one reception desk or nursing station and train its staff. Confirm the selected workplace becomes faster and the other workplace retains its level. Try the room-tier upgrade limits.
• Build and improve the car park. Watch cars enter, park and wait while their patients receive care. Patients should return before cars reverse and use the exit; occupied bays must remain reserved throughout.
• Add a toilet and vending machine after building the waiting room. Watch patients visit and return, then collect vending tips. Repeated taps or relaunching should never repeat a collection.
• Renovate a room while patients receive care. Check the scaffolding, progress display and completion, including after leaving and reopening the game.
• Explore the furnished clinic and surrounding streets. Check continuous walls, framed doors, traffic, notice boards and clear walking paths. Patients should walk rather than glide, including when queues advance and reduced motion is enabled.
• Pan, pinch and tap on a small screen. Verify that dragging across a purchase or cash never activates it. Try reduced motion and VoiceOver, including individual upgrade controls.

Please report the device, iOS version and steps for any visual overlap, missing control, stuck patient or lost progress.
```

## Exact release sequence

Complete final Unity and native acceptance, then freeze and push only the intended source and release changes. Preserve unrelated native work. `OrchardBuild.Prepare` now sets **3.2 (16)**. The package requires all twelve fixtures: `ClinicSimulationTests`, `ClinicProfileTests`, `ClinicWorldTests`, `ClinicHUDTests`, `ClinicPerformanceTests`, `ClinicExpansionTests`, `ClinicMigrationTests`, `ClinicParkingFlowTests`, `ClinicWalkingTests`, `ClinicArchitectureTests` `ClinicParkingWorldTests` and `ClinicPassingTests`, under `IdleClinic.Tests`. Every case in the final unfiltered report must pass, with zero skipped or inconclusive cases. A filtered or historical report is not final-source evidence.

Export the frozen source using `OrbitOrchard.Editor.OrchardBuild.BuildIOS` into a new Device export directory. Package it with `Tools/package_unity_export.py EXPORT_DIRECTORY --output NEW_DIRECTORY/orbit-orchard-ios.zip --test-results FINAL_UNITY_XML --write`. Require the exact source SHA, `sourceDirty=false`, `developmentBuild=false`, `iosSdk=device`, successful export, and package version 3.2/build 16.

Replace every uppercase placeholder below with its reviewed value. Each candidate needs a new draft tag and output path. Keep the transfer release unpublished.

```sh
gh release create unity-export-clinic32-SHORT_SHA-b16 \
  NEW_DIRECTORY/orbit-orchard-ios.zip \
  --repo its-me-anoop/gameon --target FULL_SOURCE_SHA --draft \
  --title 'Little Lifeline 3.2 (16) — clinic expansion export' \
  --notes-file REVIEWED_TRANSFER_NOTES_FILE

gh workflow run release.yml --repo its-me-anoop/gameon --ref codex/orbit-orchard \
  -f source_sha=FULL_SOURCE_SHA \
  -f export_release_tag=unity-export-clinic32-SHORT_SHA-b16 \
  -f export_sha256=EXACT_EXPORT_SHA256 -f upload_to_testflight=false
```

Before dispatch, read back the draft flag, exact target SHA, asset size and asset digest. Require archive success and inspect the signed archive manifest, source, 3.2/16 identity, checksum, signature, privacy declarations, scene and thirteen Apple bridge exports. Record the actual released Xcode and macOS versions used by the workflow.

Once the exact archive passes review, promote that archive without rebuilding:

```sh
gh workflow run release.yml --repo its-me-anoop/gameon --ref codex/orbit-orchard \
  -f source_sha=FULL_SOURCE_SHA -f archive_run_id=VERIFIED_ARCHIVE_RUN_ID \
  -f upload_to_testflight=true
```

Apply only the two beta text fields before promotion: `description` at `/v1/betaAppLocalizations/3b4b5dbe-e68b-4298-ae55-e70c59c8a285` and `notes` at `/v1/betaAppReviewDetails/6786840477`. Confirm each resource's app/locale identity and read back exact text equality. Preserve prices, products, boards, contacts, agreements, testers and review state. Do not invoke the legacy train configuration helper.

After upload, follow pagination for `/v1/apps/6786840477/buildUploads?limit=200` and `/v1/builds?filter[app]=6786840477&include=preReleaseVersion&limit=200`. Resolve the exact 3.2/build 16 UUID. Require `processingState=VALID`, `expired=false`, `internalBuildState=IN_BETA_TESTING` from `/v1/builds/{id}/buildBetaDetail`, and explicit membership in `/v1/betaGroups/a1098f67-cfde-4eaa-8626-e250f881596a/builds?limit=200`.

PATCH that build's existing en-US beta localization, or POST one if absent, with the exact What to Test block. Read it back independently. Archive/upload success alone does not establish internal TestFlight availability. Record the source SHA, export/archive digests, archive/promotion runs, Apple build UUID, internal membership and localization ID. This handoff does not send tester messages or submit external beta/App Store review.
