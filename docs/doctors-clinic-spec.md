# Small Doctors Clinic — next location

This release adds a playable second location to Little Lifeline, preserving the starter clinic and its patients. It also adds original background music and activity sounds. Delivery requires main to contain the reviewed implementation and the tested build to be available in Internal TestFlight.

## Scope and balance

- Open only after every starter room, component, workstation, staff training and amenity is at its existing maximum, all staff/stations are hired/built, and construction is complete. Spend exactly 100,000 collected coins once through an authoritative command.
- Preserve the whole starter simulation, its paid patients, counter cash, construction, preferences and purchase ownership. Both locations continue simulation and the eight-hour offline earnings rule. Switching locations never awards or loses money.
- The new location has twice the floor area for corresponding reception, first-aid and waiting rooms. Furniture and people keep their scale. It additionally has four distinct consultation rooms and a pharmacy with two dispensing stations.
- Maximum staff: four receptionists, four nurses, four doctors and two pharmacists, each with a distinct workstation and individual training/equipment upgrades. Opening includes one of each role and functioning first stations, so paying with exactly 100,000 cannot create a zero-income dead end. Remaining hires are progression purchases.
- Six room tiers. Component, workstation and staff-training caps increase by two per room tier, reaching twelve. The starter remains at its existing three tiers and six levels.
- New-location income, corresponding base prices and base service/construction times are multiplied by two. Existing exponential growth continues. Renovations take 120, 360, 1,080, 3,240 and 9,720 seconds. New-role prices use explicit documented bases and the same exponential policy.
- New patient flow: reception payment and admission reservation → consultation → nurse first aid → pharmacy dispensing → departure. One check-in quote covers care; transitions do not charge or award the same money again. Waiting and optional amenity visits preserve the next required service and reservation.
- Reception follows the order in which visitors physically join its queue. A car or taxi passenger still travelling to the clinic cannot block visitors already waiting inside.
- The taxi shelter has eight individually reserved waiting places. A taxi waits at its dock while its called passenger walks to board; that walk finishes before the boarding timer begins. When exterior places are full, paid patients retain their indoor care/waiting reservations until a place is available.
- Existing optional features remain functional: waiting seats, upgrades, decorations, scaffolding, notice boards, collection flights, parking, vending tips, sound/haptics and camera gestures.
- A larger car park supports twelve bays across six upgrades, with real entrance/parking/reverse/exit journeys. A larger toilet has at least two distinct usable cubicles and six upgrade levels. Vending has six levels. A taxi stand has six improvements and actual patient drop-off/pickup journeys, with bounded vehicle/dock reservations.
- Doubled room space does not scale characters or silently quadruple floor area. Queue/seating formulas must support the added staff without multiplying both their upgrade count and every increment indiscriminately.

## Persistence and ownership contract

The profile moves to schema 3. `state` remains the preserved starter state, `doctorsState` is the second state (null until purchased), and `activeLocation` selects the displayed location. Preferences remain global. Each core state has an append-only `ClinicLocation` identity.

The wallet follows the active location. Switching atomically transfers the active wallet to the destination, tracking cumulative `TotalTransferredIn` and `TotalTransferredOut` in each core state. Inactive wallets remain zero; uncollected tills belong to their own clinic. Conservation includes those transfers. No presentation or animation mutates currency. Both simulations advance together under the same committed wall-time watermark; offline resume aggregates earnings without replaying historical sound/coin events.

Opening is a profile transaction: clone both states, execute the core unlock debit/gate, create the doctors simulation, transfer the remaining wallet, select the new location, validate and commit the complete profile. Only publish the candidate to gameplay after the atomic write succeeds. Repeated taps/relaunch cannot double-charge, unlock early or lose paid patients.

Core API names for parallel work:

```csharp
ClinicLocation { StarterClinic = 0, DoctorsClinic = 1 }
ClinicRoom { Reception, FirstAid, Waiting, Consultation, Pharmacy }
ClinicStaffRole { Receptionist, Nurse, Doctor, Pharmacist }
ClinicAmenity { Parking, Toilet, Vending, Taxi }

ClinicRules.MaximumStaff(state, role)
ClinicRules.MaximumTier(state)
ClinicRules.ComponentCap(state, room)
ClinicRules.MaximumAmenityLevel(state, amenity)
ClinicRules.RoomForRole(role)
ClinicRules.HireCost(state, role)
ClinicRules.UpgradeCost(state, room, track)
ClinicRules.RenovationCost(state, room)
ClinicRules.RenovationSeconds(state, room)
ClinicRules.StationServiceTicks(state, role, stationId)
ClinicRules.StarterCompletion(state) // readable unmet requirements

ClinicSimulation.CreateForLocation(location, seed)
simulation.HireStaff(role)
simulation.AddStation(role)
simulation.UpgradeStation(role, stationId)
simulation.TrainStaff(staffId)
simulation.UnlockDoctorsClinic()
simulation.TransferWalletTo(destinationSimulation)
```

Keep old starter convenience methods and overloads where needed. New enums are appended, never reordered. Doctor IDs use 200 + station and pharmacist IDs 300 + station; existing receptionist/nurse IDs remain stable. Service-stage, cubicle, parking and taxi reservations are saved and validated explicitly. Missing anchors are validation errors, never disguised as the entrance.

## Presentation and audio

Keep the miniature ivory/sage/apricot/timber direction. The new clinic must visibly feel larger and more capable: four private consultation rooms, recognisable doctors and pharmacists, pharmacy shelves/bags, toilets, taxi shelters and a bigger neighbourhood. Keep paths and sliding-door openings continuous and unobstructed. Separate opposing walking lanes and retain distance-driven gait in reduced motion.

Use a location-specific world builder/layout/navigation provider with stable anchors; keep the render texture and camera infrastructure. Travel rebuilds the displayed world and clears old selections, markers and flights. The compact location control shows the remaining unlock checklist on demand. Four reception collection markers need real collision handling and separate accessible targets.

Original looping music and short effects must be real audio assets, with separate persisted music/effects controls. Existing `sound` remains the effects preference. Migration sets music off for previously muted users. Audio consumes authoritative events and actual movement/door activity; limit overlapping footsteps/events and suppress historical resume bursts. Pause/resume and location switching must not stack music sources.

## Acceptance and delivery evidence

1. Exact-boundary 100,000 unlock; every missing prerequisite independently blocks it. Repeated tap, write failure and relaunch are safe.
2. Genuine 3.2-save migration, prior 3.1 migration chain, both-location save restoration, currency/transfer conservation, and no repeated offline interval.
3. All staff counts, station assignments, twelve-level caps, six-tier room construction and six-level amenities can be reached through commands. Corresponding prices/income/time multipliers are verified numerically.
4. Paid patients complete consultation, first aid and pharmacy through full queues, renovations and resume. Toilet cubicles, parking bays, taxis and workstations keep distinct reservations.
5. Recorded native input visits both locations, unlocks through the real UI on a legitimate maxed progression fixture, verifies new hiring/upgrades and camera bounds, and shows real clinical/pharmacy/taxi/parking activity without missing geometry or gliding.
6. Music and effects are audible, independently switchable, persisted, safely muted on migrated muted profiles, and do not stack or burst on resume. Inspect audio files for valid duration, peak levels and seamless loop boundaries; distinguish audible playback from asset existence.
7. Full appropriate Unity tests and export checks pass on frozen source. Reconcile main without staging unrelated native work, push the reviewed implementation to main, upload the same tested archive, and independently confirm valid/unexpired Internal TestFlight availability and exact release notes.

No new advertisements, purchases, leaderboards, external beta submission or App Store review submission are part of this release.

## Save representation

Profile schema 3 retains the original `state` field and exposes `doctorsState`
through an empty-or-singleton `additionalClinics` list. Unity JSON materializes
null inline objects, so the empty list explicitly represents a locked location.
Migration archives retain the exact schema 1 or 2 envelope. Changing audio
preferences during a failed migration cannot alter the immutable source identity
used to verify that archive.

## Original audio

`Tools/create_clinic_audio.py` composes “Morning Rounds”, a 48-second, 16-bar
score at 80 BPM, plus thirteen original action sounds. No sampled recordings or
third-party music are included. One persistent music source and six bounded
sound-effect voices survive travel. Music and effects mute independently;
backgrounding pauses music and stops effects. Historical offline events do not
replay sounds. Footsteps, door openings and care effects follow actual world
activity. Development performance reports include music playback position,
active effect voices and mixed-output RMS; these are engine output measurements,
not a claim about physical speaker volume.

New taxi bookings are limited to six concurrent journeys. Further visitors walk in; existing bookings remain intact. A taxi keeps its original road-request priority through the curb stop, while the movement scheduler still requires a complete safe road window.
