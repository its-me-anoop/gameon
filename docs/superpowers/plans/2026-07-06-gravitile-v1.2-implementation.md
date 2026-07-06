# Gravitile v1.2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (or
> subagent-driven-development). Executed inline by the authoring agent this
> session. Spec: `docs/superpowers/specs/2026-07-06-gravitile-v1.2-design.md`
> (§ references below). Environment facts: see the v1.1 plan header — all still
> true (beta-Xcode quirks, simulatorId, CI-only releases, ASC venv in scratchpad).

**Goal:** Stasis powerup, Boulder hurdle, share upgrade (image+text Transferable,
achievements), stats upgrade (records/distribution/calendar/number-polish).

**Save-compat discipline (every task):** any Codable change ships in the same
commit as a byte-frozen old-format fixture test proving lossless decode.

### Task 1: Engine — Stasis (spec §3.1)
- Modify: `GravitileKit/Sources/GravitileKit/GameState.swift` (stasisCharges,
  crossings, canUseStasis, applyMove(stasis:)), `MoveResolver.swift`
  (skip rotation), `MoveResult.swift` (heldGravity).
- Test first in `GameStateTests.swift`: `stasisHoldsGravityForOneMove`,
  `stasisChargesEarnedAtMilestonesCappedAtTwo`, `zenStasisNeedsNoCharges`,
  `dailyAndSprintCannotUseStasis`, `undoRestoresStasisCharges`,
  fixture test extended (`stasisCharges` defaults 0).
- Run `swift test --package-path GravitileKit` red → implement → green → commit
  `feat(engine): stasis — hold gravity for one move`.

### Task 2: Engine — Boulder (spec §3.2, §3.3)
- Modify: `Tile.swift` (ice + custom decode), `MoveEvents.swift` (IceHit,
  SlideOutcome.iceHits), `MoveResult.swift` (CascadePhase.iceHits),
  `MoveResolver.swift` (merge exclusion, chip pass per phase, freed-tile flow,
  spawnBoulder), `GameState.swift` (per-mode schedule §3.2.4, scoring §3.2.5).
- Test first: new `BoulderTests.swift` — one test per rule in spec §3.2 plus
  `boulderSpawnDrawsExactlyTheSameRandomsAsANormalSpawn` (golden-game guard)
  and `v11TileJSONDecodesWithIceZero`.
- Commit `feat(engine): boulder hurdle — iced tiles chip free through play`.

### Task 3: App — Stasis UI + boulder rendering (spec §4.1)
- Modify: `GameViewModel.swift` (armed state, stasis swipe path, ice-hit anim
  steps), `AnimationPlanner.swift` (ice hits in step stream), `GameScreen.swift`
  (stasis button + charge dots, mode gating), `HUDView.swift` (compass lock
  badge), `BoardView.swift`/`TileView` (frost/crack rendering), `Juice.swift`,
  `HapticsService.swift` (iceChip), `Tools/gensounds.swift` (chip, shatter) +
  regenerate, `TutorialOverlay`-adjacent one-shot hint, `PersistenceService.swift`
  (hasSeenBoulderHint w/ decode default).
- Tests: view-model arming state machine; persistence fixture extension.
- Commit `feat(app): stasis control + boulder rendering, sounds, haptics`.

### Task 4: Share upgrade (spec §4.2)
- Modify: `ShareCard.swift` (text v2 + goldens in `ShareCardTests`),
  new `Gravitile/Share/ShareCardView.swift`, `ShareableResult.swift`
  (Transferable), swap ShareSheet→ShareLink in `HUDView`/`GameScreen`/
  `DailyScreen`; Stats achievements section (`GameCenterService.loadAchievements`
  + descriptions) with per-earned-row ShareLink.
- Commit `feat(share): image+text share card, achievements sharing`.

### Task 5: Stats upgrade (spec §4.3)
- Modify: `StatsScreen.swift` (records block w/ mode segments, distribution
  histogram, monthly calendar replacing strip), `PersistenceService.swift`
  (DailyRecord.movesUsed/usedUndo, ModeStats — custom decode + fixtures),
  `AppModel.swift` (accumulation), `GameCenterService.swift` (weekly percentile
  via loadEntries).
- Commit `feat(stats): per-mode records, daily distribution + calendar, polish`.

### Task 6: Verify + wrap (spec §5)
- Full engine + app + UI suites on sim (boot first; reboot on mach-error flake).
- Autoplay soak past move 60 (boulders visible), screenshots, watch build check.
- `docs/balance-report.md` addendum if boulder tuning moved; runbook §Challenges
  note (spec §4.4). Update memory. Commit.
