# Idle clinic: design and maintenance

The playable scope is one miniature 3D clinic: a reception with up to two desks, one first-aid room with up to two nurse stations, and an optional waiting room. Each room has three tiers within its designed plot. New departments, rankings and a new shop are deferred. Runtime evidence belongs in [clinic QA](qa/idle-clinic.md); this document specifies behavior and reproducible simulation expectations, not proven player enjoyment or device performance.

## Controls and opening

- Drag the world to pan; pinch to zoom around the fingers. On desktop, precise trackpad scrolling pans, the mouse wheel zooms, Shift-scroll pans, and Ctrl/Command-scroll zooms. Home returns to reception; explicit zoom buttons remain available.
- Tap a room for its compact upgrade controls. Tap a reception cash stack to collect that desk's entire till into the wallet at the top of the screen. Dragging or a multi-touch gesture must not activate a purchase or collection on release.
- Progress rings show check-in, treatment and construction. Cash flights, working poses, staff arrivals and furnishings express actual state changes. Sound, haptics and reduced motion are independent preferences.

Start with one free receptionist, one treatment station, no nurse, and zero wallet/till cash. The first patient approaches reception and pays 50 coins. The player must collect those coins and hire the first nurse for 50; other purchases remain unavailable until the first treatment finishes. The nurse visibly enters before receiving a patient. Only then do further arrivals begin.

Patients pay before care. Completed check-in increases a desk's till, never the spendable wallet. Paid patients keep their place until treatment and are not removed for waiting too long. Collection is optional during continued operation, so a patient can be treated while the payment remains at reception.

## Economy and finite progression

The authoritative constants are in [ClinicRules.cs](../Unity/OrbitOrchard/Assets/IdleClinic/Core/ClinicRules.cs). Money uses whole `long` coins; simulation time uses ten ticks per second. Service times round upward to the next tick.

| Purchase | Coins | Prerequisite / time |
| --- | ---: | --- |
| First nurse | 50 | Collect the first payment; enters over 4 seconds |
| Waiting room | 160 | Unlock once two paid patients wait; construction takes 20 seconds; four initial seats |
| Second treatment station | 180 | First-aid room tier 2 |
| Second nurse | 450 | Second station installed; two nurses total |
| Second receptionist and desk | 300 | Opening tutorial complete; two receptionists total |

The two nurse prices follow `50 × 9^(hire ordinal − 1)`. The starting receptionist is free; the only additional receptionist purchase includes its desk. There are no recurring wages or further staff slots.

Component levels start at **1**, capped at **2 / 4 / 6** by room tier **1 / 2 / 3**. For current component level `L`, the next price is `ceil(base × (8/5)^(L−1))`; calculate the rational expression before rounding, not by repeatedly rounding the previous price.

| Room | Equipment base / effect | Facilities base / effect | Decoration base / effect |
| --- | --- | --- | --- |
| Reception | 60; check-in time `14 ÷ [1 + .15(L−1)]` seconds | 50; unpaid queue capacity `6 + (L−1)` | 40; adds 5 percentage points to the fee per level above 1 |
| First aid | 80; treatment time `18 ÷ [1 + .15(L−1)]` seconds | 60; adds 15 percentage points to the fee per level above 1 | 40; same decoration fee contribution |
| Waiting room | 35; seated-patient call delay `2 ÷ [1 + .15(L−1)]` seconds | 45; seat capacity `4 + 2(L−1)` | 40; same decoration fee contribution once built |

The first-aid facility upgrade does not install another station. Station capacity and its nurse are separate purchases. Waiting equipment affects the call delay for a patient coming from a waiting-room seat; walking remains physical movement.

The quoted visit fee is:

```text
floor(50 × [100 + 15 × (first-aid facilities − 1)
                + 5 × sum(decoration level − 1 across built rooms)] / 100)
```

It starts at 50, becomes 57 after the first first-aid facility upgrade, and 60 after one decoration upgrade. Capture the quote when reserving the reception appointment. Capture each service duration when that service begins; later upgrades do not change an existing quote or treatment deadline.

Room renovation prices are `ceil(room base × (5/2)^(current tier−1))`:

| Room | Tier 1→2 | Tier 2→3 |
| --- | ---: | ---: |
| Reception | 180 | 450 |
| First aid | 250 | 625 |
| Waiting room | 120 | 300 |

The corresponding durations are **60 and 180 seconds**, from `60 × 3^(current tier−1)`. Deduct the cost once at construction start. Existing care continues; the higher component cap and room tier activate only at completion. One construction job per room is allowed, with different rooms able to progress concurrently.

## Simulation, presentation and save contracts

`IdleClinic.Core.ClinicSimulation` owns the complete serializable `ClinicState`. `CreateNew`, `Advance`, `AdvanceOffline`, and `IsValidState` are the integration entry points. Commands are `Collect(deskId)`, `HireNurse`, `HireReceptionist`, `BuildWaitingRoom`, `AddTreatmentStation`, `Upgrade(room, track)`, and `Renovate(room)`. Each returns success, reason, cost and collected amount without letting the UI decide affordability.

Before dispatching to a counter, reserve downstream capacity: two standing positions before the waiting room exists, otherwise its seats, plus the hired nurse stations. A full paid reservation pool holds newcomers in the bounded unpaid queue. Full unpaid queues defer arrivals; missed arrivals do not accumulate into an offline backlog. Reception desks use longest-idle-first allocation; nurses and patients reserve distinct treatment stations. Waiting-room completion moves standing patients toward distinct seat sockets without collecting another fee.

`Advance` returns payment/treatment totals and drains presentation events, including any undrained command events. The UI drains command events immediately after a successful action. Events have stable increasing IDs, patient/desk/room identity, amount and source socket. **A cash flight never credits money**: `Collect` has already transferred and cleared that till, so another tap cannot repeat the transfer. Offline advancement suppresses presentation events.

The [core types](../Unity/OrbitOrchard/Assets/IdleClinic/Core/ClinicTypes.cs) are the shared renderer contract. Patients carry phase, source/destination socket, phase start/end ticks, desk, station, seat and reservation IDs; staff carry movement endpoints and deadlines. [ClinicWorld](../Unity/OrbitOrchard/Assets/IdleClinic/Runtime/Presentation/ClinicWorld.cs) renders the persistent diorama, resolves hit tests and owns camera transforms. `ClinicActors` routes pooled actors through authored circulation points; `ClinicUpgrades` reflects the component levels. Socket families are `reception.desk.{0,1}`, `firstaid.station.{0,1}`, `reception.queue.{0..10}`, `waiting.seat.{0..13}`, and `firstaid.standing.{0,1}`. Keep core IDs, furniture and socket placement synchronized.

[ClinicProfileStore](../Unity/OrbitOrchard/Assets/IdleClinic/Runtime/Services/ClinicProfileStore.cs) saves `idle-clinic-profile-v1.json`: full state, tutorial, wallet/tills, construction, preferences and accounted UTC time. It checks the payload checksum and state, flushes a temporary file, atomically replaces the primary, retains a good backup and preserves unreadable originals. Previous-profile migration copies only sound, haptics and reduced-motion preferences.

Offline operations use the same simulation for at most **eight hours**; earnings stay in reception tills until tapped. Construction follows the full absence. Beyond the earnings cap, patient/staff deadlines shift so the skipped interval cannot later become earned care. Advance a copied profile and commit it with the full accounted interval before exposing results. While a commit is pending, block active ticks and spending; retry without consuming that interval through another save. After a successful resume, rebind the simulation to `store.Profile.state`. The checksum detects damage; it is not server-side economy verification.

## Reproducing balance and acceptance

Use `ClinicSimulationTests.FirstTenMinutesFundEquipmentWaitingRoomTwoNursesAndSecondReceptionist` in [the core suite](../Unity/OrbitOrchard/Assets/IdleClinic/Tests/Core/ClinicSimulationTests.cs). It starts a fresh clinic, advances one second per step, collects every ten seconds, hires the first nurse when prompted, and attempts this sequence as soon as each purchase becomes valid:

1. First-aid equipment → waiting room → first-aid facilities → first-aid decoration.
2. First-aid tier 2 → second station → second nurse → second receptionist/desk.

The implemented model has produced **1:40** for the first upgrade, **2:39** for waiting-room purchase (**2:59** completion), **7:30** for the second nurse and **9:00** for the second receptionist. These are reproducible simulation milestones, not human playthrough measurements. Real input, onboarding comprehension, animation clarity and performance remain subject to the separate QA record.

Acceptance bands are first discretionary upgrade within 1–2 minutes, waiting room within 2–4 minutes, and second nurse within 7–10 minutes without ads. Also exercise decorations-first spending, infrequent collection, full queues, simultaneous treatments, collection/relaunch, construction/relaunch, clock rollback, failed saves and all finite upgrade caps. Change launch curves only when measured acceptance cadence fails, and retain these regressions.

Run `python3 Tools/check_unity_sources.py --output build/clinic-source-check` from the repository root for managed compilation and selected core tests. Use the coordinated Unity EditMode procedure in [release preparation](idle-clinic-release.md#preparation-and-validation) for native JSON, scene and presentation coverage; never open a second Editor against the project. Keep current test counts, screenshots, device measurements and release status in [QA](qa/idle-clinic.md).

## Research informing the loop

Primary references were checked on 12 September 2026. The implementation choices below are design interpretations, not claims that another game's results predict this game's engagement.

- [My Perfect Hotel's official listing](https://apps.apple.com/us/app/my-perfect-hotel/id1635760774) describes reception, payment collection, reinvestment and hiring. It informed the visible sequence from work to collected cash to a staff member taking over.
- [Hospital Empire Tycoon's first tips](https://codigames.helpshift.com/hc/en/19-hospital-empire-tycoon/faq/533-how-to-play-hospital-empire-tycoon-first-tips/) explains linked staffing needs and room upgrades followed by rank/expansion. It informed visible bottlenecks and room tiers that unlock further improvements.
- [Its economy guide](https://codigames.helpshift.com/hc/en/19-hospital-empire-tycoon/faq/571-tips-to-make-more-money/) recommends checking the whole patient-service chain and tapping rooms to understand upgrades. It informed contextual room controls and testing reception throughput against treatment capacity. This clinic's upfront reservation policy and persistent paid queue are explicit local design decisions.
