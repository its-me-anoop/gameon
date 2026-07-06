# Gravitile v1.1 — "Modes, Juice & Watch" Design Document

**Date:** 2026-07-06
**Status:** Approved for implementation (autonomous session — decisions made by agent
under `/goal` directive; user review welcome, all decisions reversible)
**Baseline:** v1.0 build 8, WAITING_FOR_REVIEW on App Store Connect since 2026-07-02.
v1.1 is developed and uploaded now; review submission happens once v1.0 clears
(cancelling a queued submission loses its place and is a user-level call).

## 1. Goal

Per the user directive: make Gravitile markedly more fun and addictive — more game
modes, richer animations, background music, more sound effects and haptics,
leaderboards surfaced in-app, free assets where sensible, a mini watchOS version,
and a fix for the left-edge back-swipe that interrupts gameplay. Test everything,
refresh screenshots/copy, and submit following App Store guidelines.

Market research (2026-07-06, see session notes) ranked the highest fun-per-effort
moves: (1) Balatro-style cascade escalation juice — synchronized pitch-rising audio,
sharpening haptics, and score-pop visuals; (2) a recurring weekly Game Center
leaderboard so new players can realistically place; (3) juicing the gravity-rotation
transition itself — Gravitile's unique mechanic; (4) Woodoku-style mode variety
(timed/zen splits) as proven retention structure.

## 2. Scope overview

| # | Feature | Risk |
|---|---------|------|
| A | Edge-swipe fix: own the exit, disable interactive pop on GameScreen | low |
| B | Two new modes: **Zen** (no pressure, no clock) and **Sprint** (60-move score attack) | low |
| C | Background music (generative ambient loop) + expanded SFX + music toggle | low |
| D | Haptics: rotation tick, landing settle, milestone celebration, new-best | low |
| E | Animation juice: score pops, board shake ≥2-round cascades, gravity tilt cue, milestone bursts | med |
| F | Game Center: 2 new leaderboards + weekly recurring daily board; GKAccessPoint + leaderboard sheet | med |
| G | watchOS companion: standalone playable 5×5 Zen game | high |
| H | Metadata: listing refresh, What's New, new 6.9" + watch screenshots | low |
| I | Version 1.1 (build 9), release via CI, submit when v1.0 clears | external |

Non-goals for v1.1: iPad-specific layouts beyond current constraints, localization,
themes, widgets, engine board-size parameterization (watch reuses 5×5), watch↔phone
sync, new IAPs.

## 3. Design details

### A. Edge-swipe fix (shipped in this session already)

`GameScreen` hides the navigation bar and system back button (which also disables
UIKit's interactive pop gesture — the gesture that was eating left-edge game swipes)
and adds an explicit chevron button in the header (`exitGame` accessibility id).
Other screens keep standard navigation.

### B. Game modes

Engine (`GravitileKit`) — `GameMode` gains two cases:

```swift
case zen                      // no budget, no pressure ramp
case sprint(moveBudget: Int)  // fixed budget, constant pressure
static let sprintMoveBudget = 60
```

- **Spawn pacing becomes mode-owned.** `GameState` routes spawn count through the
  mode: endless keeps the `min(3, 1 + moves/60)` ramp; **zen always spawns 1**
  (BalanceSim showed 1-spawn play survives indefinitely — that's the point of zen);
  **sprint always spawns 2** (instant tension, no cruise phase); daily keeps the ramp
  (its 40-move budget never reaches it). Public API: instance property
  `spawnCountForNextMove`; the existing static stays for compatibility.
- `movesRemaining` covers sprint via its budget. Game-over logic is unchanged.
- Codable: adding enum cases is backward-compatible for decoding v1.0 data.

App:

- `PersistedState` gains `zenGame`, `sprintGame` (resumable, like endless),
  `bestZenTile`, `bestSprintScore`.
- **Persistence migration hazard:** synthesized `Codable` throws on missing keys for
  non-optional fields, and `load()` falls back to a *fresh state* — an update must
  never wipe user data. `PersistedState` and `Settings` get hand-written
  `init(from:)` using `decodeIfPresent` + defaults. A fixture test decodes captured
  v1.0 JSON and asserts nothing is lost.
- `AppModel`: zen/sprint lifecycle mirrors endless (resume, checkpoint, record end,
  per-mode bests). `recordGameEnd` switches over all four modes.
- UI: `Route.zen` / `Route.sprint`; HomeScreen adds a compact two-up mode row under
  the existing Endless/Daily cards (visually distinct chips — deliberately *not* a
  third and fourth identical card); GameScreen title/moves indicator/game-over copy
  become mode-aware ("Zen", "Sprint", "Time's Up" analog is "Out of Moves").

### C. Audio

Stay license-clean and regenerable: **all assets synthesized in-repo** by extending
`Tools/gensounds.swift` (research confirmed no turnkey CC0 loopable-BGM pack exists;
Kenney's CC0 packs ship .ogg, which AVAudioPlayer can't decode, and transcoding adds
a toolchain dependency). Kenney remains a documented fallback if synthesis
disappoints.

New generated assets:

- `bgm.wav` — ~48 s seamless generative ambient loop: slow detuned-sine pad chords
  (i–VI–III–VII progression around A minor pentatonic), sparse bell arpeggio,
  −18 dB headroom, mono 44.1 kHz (≈4 MB).
- `whoosh.wav` (gravity rotation — filtered noise sweep), `land.wav` (soft thock at
  fall settle), `tap.wav` (UI), `milestone.wav` (two-note chime for first
  256/512/1024/2048 of a game), `newbest.wav` (short rising sting).

`SoundService`:

- Dedicated looping `AVAudioPlayer` for BGM at low volume (0.22), `numberOfLoops
  = -1`; starts on game screens, pauses on backgrounding. Category stays `.ambient`
  + `.mixWithOthers` — silent switch respected, user's own music never interrupted.
- New: `isMusicEnabled` (persisted `Settings.musicOn`, default true, custom-decoded),
  `whoosh()`, `land()`, `tap()`, `milestone()`, `newBest()`.

### D. Haptics

- `rotationTick()` — feather transient when gravity rotates (with the whoosh).
- `landing()` — soft low-sharpness transient when falls settle.
- `milestone()` — 3-transient rising celebration + short continuous tail.
- `newBest()` — double tap pattern at game end.
- Cascade merge curve keeps escalating (already good); round cap raised so ×4+
  cascades keep sharpening.

### E. Animation juice (all `transform`/`opacity`-only; reduce-motion falls back to
crossfades, shake/tilt skipped)

- **Score pop:** floating "+N" delta rises from the score badge on each scoring step,
  scaled/coloured by cascade round; badge pulses via existing `numericText`.
- **Board shake:** cascade rounds ≥2 jitter the board container (2–5 pt, scaling
  with round) — Balatro's "shake scales with score" as a data channel.
- **Gravity tilt cue:** during the `gravityCue` step the board rotates ~2.5° toward
  the new gravity edge and springs back — makes the rotation legible and felt.
- **Landing squash:** tiles compress to 0.92 y-scale for ~80 ms when a fall settles.
- **Milestone burst:** first 256/512/1024/2048 in a game triggers a full-board
  particle volley + chime + milestone haptic; "New Best" state on the game-over card.

### F. Game Center

- New leaderboards (ASC via `Tools/publish_gamecenter.py`):
  `grv.sprint.best` (classic), `grv.zen.tile` (classic),
  `grv.daily.weekly` (**recurring weekly**, daily-mode scores; research: recurring
  occurrences give new players a realistic shot — the single biggest leaderboard
  retention lever).
- `GameCenterService.submit` routes zen→tile board, sprint→score board, daily→both
  classic daily and weekly recurring boards.
- Surfacing: `GKAccessPoint` active on Home (top-trailing, avoids the hero text);
  Stats screen gains a "Leaderboards" row presenting `GKGameCenterViewController`
  (leaderboards page) in a sheet.
- Existing 8 achievements unchanged; no new ones in v1.1 (ASC metadata approval is
  async — keep the surface small).

### G. watchOS companion — "Gravitile" for Apple Watch

- **Standalone playable game** (App Review 4.2 requires genuine standalone value; a
  mirror/launcher risks rejection): full `GravitileKit` engine, same 5×5 board, Zen
  pacing (1 spawn/move — right for short wrist sessions), swipe input (DragGesture
  works on watchOS), score + best + resumable game persisted via `UserDefaults`
  (Codable JSON), current-gravity arrow in the corner, tap-and-hold → New Game.
  `WKInterfaceDevice` haptics (`.click` per merge, `.success` on milestone).
- New XcodeGen target `GravitileWatch` (watchOS 10+, SwiftUI app lifecycle),
  bundle id `com.flutterly.gravitile.watchkitapp`, embedded in the iOS app as a
  companion; depends on `GravitileKit` only. No Game Center, no IAP on watch v1.
- ASC: register the watch bundle ID via API; CI archive picks the watch app up
  automatically as an embedded product (automatic signing with
  `-allowProvisioningUpdates` + ASC API key, as release.yml already does).
- Watch App Store screenshots (410×502 px class) generated from the watch simulator.

### H. Metadata & screenshots

- `docs/appstore/listing.md`: description gains modes/music/watch bullets; keywords
  add "zen", "sprint"; What's New drafted from git history (app-store-changelog
  skill) — leads with the back-gesture fix, modes, music, watch app.
- Screenshots: recapture 6.9" set (1290×2796, existing accepted size) featuring
  Zen/Sprint cards, a big cascade with score pop, and the watch app gets its own
  set. Screenshot text/pipeline: simulator captures per publishing runbook.

### I. Versioning & release

- `MARKETING_VERSION 1.1`, `CURRENT_PROJECT_VERSION 9`.
- Build + upload strictly via `.github/workflows/release.yml` (beta-macOS build
  stamp on this Mac causes Invalid Binary at the review gate — post-mortem in the
  publishing runbook).
- Submission: create v1.1 on ASC, attach build 9, reuse review notes; **submit only
  after v1.0 leaves WAITING_FOR_REVIEW**. If v1.0 is approved → normal v1.1
  submission. If v1.0 is rejected → fold fixes into v1.1 and resubmit per runbook
  (ASC UI path; the API PATCH 409s after Invalid Binary recoveries).

## 4. Testing strategy

1. **Engine (swift test):** zen/sprint spawn pacing, sprint budget exhaustion and
   game-over, zen never gains a budget, mode Codable round-trips, determinism per
   mode.
2. **Persistence:** v1.0 JSON fixture decodes losslessly (streak, records, settings,
   in-flight games); settings round-trip with `musicOn`.
3. **App logic:** AppModel per-mode lifecycle (resume/checkpoint/record/bests), GC
   submit routing (leaderboard IDs per mode) via a spy seam.
4. **UI smoke (XCUITest):** new routes reachable; exitGame button pops; a scripted
   zen/sprint move works.
5. **Simulator verification (XcodeBuildMCP, simulatorId not name — beta quirk):**
   screenshot pass over every screen incl. new modes; `GRAVITILE_AUTOPLAY=1` soak in
   endless + sprint; audible/BGM sanity is device-only (note in runbook TestFlight
   checklist).
6. **Watch:** engine reuse means logic is already covered; build + run on watch
   simulator, screenshot, one scripted swipe.
7. **StoreKit tests remain `.disabled` on this beta-Xcode machine (SKTestSession
   quirk) — unchanged.**

## 5. Risks

- **v1.0 review timing** is outside our control; v1.1 submission is gated on it.
  Mitigation: everything else lands now; submission is a two-command runbook step.
- **Watch target in CI signing** — first archive containing a watch app needs the
  new bundle ID registered; do it via ASC API before the release run.
- **Persistence migration** — mitigated by custom decoding + fixture test (above).
- **BGM loop quality** — synthesized ambient is a taste risk; volume kept low,
  separate toggle, and the Kenney fallback documented.
- **Board shake overdone** — kept small (≤5 pt), only ≥2-round cascades, disabled
  under reduce-motion.
