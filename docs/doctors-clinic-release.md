# Little Lifeline 3.3 (17)

**Available in the Internal TestFlight group**, independently verified on 12 September 2026 at 18:46:49 UTC. The frozen game source is on `main`; the release described below is the same archive checked on the iPhone.

The doctors clinic opens after fully upgrading the starter clinic and paying 100,000 coins. It doubles room area and corresponding income, prices and service/build times. Players can hire four receptionists, four doctors, four nurses and two pharmacists, progress through six room tiers, and upgrade room components, individual workstations and staff training to level 12. Both locations retain their progression and share an atomically saved wallet.

Patients visibly check in, consult a doctor, receive first aid and collect medication. The grounds include twelve parking bays, separate vehicle entrances and exits, a larger waiting room, two toilet cubicles and a taxi stand. Renovations display construction progress while existing services continue. Paid patients keep care reservations, and queues use saved physical paths so advancing or restoring a queue does not send patients back through the entrance.

Cars and taxis share complete safe road windows. Taxi requests retain their age through the curb stop; six active bookings limit demand, with additional visitors arriving on foot. Eight reserved waiting positions separate outgoing passengers. Only the called passenger walks to the assigned dock before boarding. Reception serves visitors already joining its physical queue while transport bookings approach. Background resume restores actors to their saved journey positions without moving the camera.

An original 48-second score and thirteen action sounds accompany collection, staffing, care, construction, doors, walking and taxis. Music and effects have separate persistent controls; backgrounding preserves music position and offline earnings do not replay old effects.

## Validation

**Automated tests:** 663/663 Unity EditMode tests passed with zero failures or skips (`tests-09.xml`). Separately, 100/100 managed Core tests passed. A two-hour simulation measured maximum new taxi arrival, pickup and paid-car exit waits of 489.0, 357.7 and 532.6 seconds respectively. Every first-hour taxi booking completed by the end. These are deterministic stress checks, not measured native gameplay or recommended waiting times.

**Current-source simulator:** `native-expanded-05` and `native-expanded-taxi03` each executed one passing native test. Recorded review accepted expanded-clinic controls, reception flow, parking entry, distinct taxi waiting positions, articulated walks to both docks, boarding and pickup departure. Saved-state audits found no money, reservation or care-order failures. Save observations included nine car departures and one completed taxi pickup in the expanded run. Audio telemetry separately recorded music/effects output, silence with both disabled, and uninterrupted playback position across travel/resume; the videos have no audio stream.

Coverage remains bounded: no complete same-patient arrival–care–return journey was filmed, no complete car reverse-to-road exit was visible in the current parking hold, and at most three taxi waiting positions were occupied in recordings. The crowded clinic showed long finite car-owner waits, including 398.4 seconds before nurse dispatch.

**Physical iPhone:** Five starter-clinic HUD snapshots over 120.98 seconds on iPhone 17 Pro Max showed 59.99 FPS and Nominal thermal state. This is neither a continuous performance trace nor a physical doctors-clinic benchmark. Normal relaunch removed the HUD. The real schema 2 save migrated to schema 3 with its original bytes archived; preferences and progression persisted. Concurrent phone interaction changed upgrades and collection; the resulting money ledger and ownership changes reconciled exactly. Audible listening, long-duration thermals and battery performance were not assessed.

## Archive and delivery

Frozen source: `6ffd4739d03c2f8f1af0543e44d91a44aa6ff08d`.

Adopted archive run: `34710988653`; SHA256: `831f56b38016eb48bf5870713e9610d19513eaac4804d3024d4636286b7e61a7`. Independent verification compared all 46 archive files byte-for-byte and verified signatures, protected executable bytes and pinned provisioning. The authorized physical check used this archive. [Promotion run 34711896409](https://github.com/its-me-anoop/gameon/actions/runs/34711896409) uploaded the same archive successfully at 18:44:04 UTC, with export/archive jobs skipped and no rebuild. The release used Xcode 26.6 (17F113) on macOS 26.6.2 (25G83).

Apple build `04cf8849-68c0-4555-a843-c37b462d5715` is `VALID`, unexpired and `IN_BETA_TESTING`, with explicit membership in the existing Internal group. Upload status is `COMPLETE`, with no reported errors or warnings. The beta description, reviewer notes and build-specific What to Test were applied and read back exactly.

The [final Apple readback](../build/qa-doctors-clinic/taxi-release/promotion-01/asc-final-delivery.json), [upload report](../build/qa-doctors-clinic/taxi-release/promotion-01/provider-runtime-report.json), [simulator acceptance](../build/qa-doctors-clinic/taxi-release/gameplay-acceptance.json) and [physical acceptance](../build/qa-doctors-clinic/taxi-release/physical-01/acceptance.json) retain the evidence. Original simulator saves were restored byte-for-byte. The phone retains the real campaign and subsequent play; no QA campaign was installed. Transfer and recording backups remain unpublished drafts.

New leaderboards and monetisation remain deferred. This release does not submit App Store review or enable external beta testing.
