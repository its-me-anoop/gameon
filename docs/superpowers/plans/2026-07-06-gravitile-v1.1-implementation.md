# Gravitile v1.1 — Modes, Juice & Watch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development
> (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.
> *This session:* executed inline (executing-plans style) by the authoring agent —
> full session context makes per-task subagent handoff slower and riskier here.

**Goal:** Ship Gravitile v1.1: Zen + Sprint modes, BGM + expanded SFX/haptics/juice,
surfaced Game Center leaderboards (incl. weekly recurring), a standalone watchOS
mini game, edge-swipe fix, refreshed metadata/screenshots — released via CI, ready
to submit the moment v1.0 clears review.

**Architecture:** All game logic stays in the pure-Swift `GravitileKit` package
(TDD, `swift test`). The iOS app consumes engine events via `AnimationPlanner`;
juice/audio/haptics hook the existing `onMerge`-style callback seams in
`GameViewModel`/`GameScreen`. Persistence gains hand-written decoding so v1.0 user
data survives schema growth. The watch app is a new XcodeGen target reusing the
engine verbatim.

**Tech stack:** Swift 6, SwiftUI, XcodeGen (`./Tools/generate.sh`), Swift Testing
(engine) + XCTest (app), AVFoundation, CoreHaptics, GameKit, watchOS 10 SwiftUI.
Spec: `docs/superpowers/specs/2026-07-06-gravitile-v1.1-design.md`.

**Environment facts (do not rediscover):** Xcode 27 beta quirks — XcodeBuildMCP needs
`simulatorId` (not name); StoreKit tests stay `.disabled`; NEVER archive releases
locally (beta BuildMachineOSBuild ⇒ Invalid Binary) — use
`.github/workflows/release.yml`. ASC API via `Tools/ascapi.py` (needs `cryptography`;
venv in session scratchpad works). v1.0 is WAITING_FOR_REVIEW — do not cancel it.

---

### Task 1: Engine — mode-aware spawn pacing (Zen + Sprint)

**Files:**
- Modify: `GravitileKit/Sources/GravitileKit/GameState.swift`
- Test: `GravitileKit/Tests/GravitileKitTests/GameStateTests.swift` (extend)

- [ ] **Step 1: Failing tests** — add to `GameStateTests.swift`:

```swift
@Test func zenAlwaysSpawnsOne() {
    var game = GameState(mode: .zen, seed: 7)
    for _ in 0..<130 {
        guard let dir = Direction.allCases.first(where: { d in
            var copy = game; return copy.applyMove(d) != nil
        }) else { break }
        let before = game.board.tiles.count
        let result = game.applyMove(dir)!
        #expect(result.spawns.count <= 1)
        _ = before
    }
    #expect(game.movesRemaining == nil)
}

@Test func sprintSpawnsTwoAndExhaustsBudget() {
    var game = GameState(mode: .sprint(moveBudget: GameMode.sprintMoveBudget), seed: 7)
    #expect(game.movesRemaining == 60)
    var spawnedTwo = false
    while !game.isGameOver {
        guard let dir = Direction.allCases.first(where: { d in
            var copy = game; return copy.applyMove(d) != nil
        }) else { break }
        let result = game.applyMove(dir)!
        if result.spawns.count == 2 { spawnedTwo = true }
    }
    #expect(spawnedTwo)
    #expect(game.moveCount <= 60)
}

@Test func modeCodableRoundTrip() throws {
    for mode in [GameMode.zen, .sprint(moveBudget: 60), .endless, .daily(puzzleNumber: 3)] {
        let game = GameState(mode: mode, seed: 1)
        let data = try JSONEncoder().encode(game)
        let back = try JSONDecoder().decode(GameState.self, from: data)
        #expect(back.mode == mode)
    }
}
```

- [ ] **Step 2:** `swift test --package-path GravitileKit` → new tests FAIL (no such case `zen`).
- [ ] **Step 3: Implement** in `GameState.swift`:

```swift
public enum GameMode: Equatable, Codable, Sendable, Hashable {
    case endless
    case daily(puzzleNumber: Int, moveBudget: Int)
    case zen
    case sprint(moveBudget: Int)

    public static let dailyMoveBudget = 40
    public static let sprintMoveBudget = 60
    public static func daily(puzzleNumber: Int) -> GameMode { .daily(puzzleNumber: puzzleNumber, moveBudget: dailyMoveBudget) }
    public static var sprint: GameMode { .sprint(moveBudget: sprintMoveBudget) }
}
```

`movesRemaining` handles `.sprint`; spawn pacing:

```swift
public var spawnCountForNextMove: Int {
    switch mode {
    case .zen: 1
    case .sprint: 2
    case .endless, .daily: Self.spawnCount(forMovesPlayed: moveCount)
    }
}
```

`applyMove` uses `spawnCountForNextMove` instead of the static call.

- [ ] **Step 4:** `swift test --package-path GravitileKit` → PASS (all suites).
- [ ] **Step 5:** Commit `feat(engine): zen and sprint modes with mode-owned spawn pacing`.

### Task 2: Persistence — lossless migration + per-mode slots

**Files:**
- Modify: `Gravitile/Services/PersistenceService.swift`
- Test: `GravitileTests/PersistenceTests.swift` (extend; add a v1.0 JSON fixture literal)

- [ ] **Step 1: Failing tests** — decode a captured v1.0 envelope JSON string (settings
  without `musicOn`, state without zen/sprint fields) → asserts records/streak/settings
  survive, `settings.musicOn == true`, `zenGame == nil`, `bestSprintScore == 0`.
  Round-trip test with new fields populated.
- [ ] **Step 2:** Run app tests (xcodebuild test, simulatorId) → FAIL.
- [ ] **Step 3: Implement** — add fields + hand-written `init(from:)` (`decodeIfPresent`
  + defaults) for `Settings` and `PersistedState`:

```swift
struct Settings: Codable, Equatable {
    var soundOn = true
    var musicOn = true
    var hapticsOn = true
    var themeID = "ember"
    var hasSeenTutorial = false

    init() {}
    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        soundOn = try c.decodeIfPresent(Bool.self, forKey: .soundOn) ?? true
        musicOn = try c.decodeIfPresent(Bool.self, forKey: .musicOn) ?? true
        hapticsOn = try c.decodeIfPresent(Bool.self, forKey: .hapticsOn) ?? true
        themeID = try c.decodeIfPresent(String.self, forKey: .themeID) ?? "ember"
        hasSeenTutorial = try c.decodeIfPresent(Bool.self, forKey: .hasSeenTutorial) ?? false
    }
}
```

`PersistedState` identically (`zenGame`, `sprintGame`, `bestZenTile`,
`bestSprintScore` new; every existing field via `decodeIfPresent`).

- [ ] **Step 4:** App tests PASS. **Step 5:** Commit `feat(app): persist zen/sprint slots; lossless v1.0 state migration`.

### Task 3: Mode lifecycle + UI (routes, Home, GameScreen)

**Files:**
- Modify: `Gravitile/App/AppModel.swift` (zen/sprint mirrors of endless lifecycle;
  `recordGameEnd`/`checkpoint` switch all four modes; bests),
  `Gravitile/App/HomeScreen.swift` (two-up Zen/Sprint chip row under Daily, distinct
  styling), `Gravitile/App/RootView.swift` (+ `Route.zen`, `Route.sprint`),
  `Gravitile/Game/GameScreen.swift` (mode-aware title/new-game/moves indicator),
  `Gravitile/Game/HUDView.swift` (GameOverOverlay copy per mode)
- Test: `GravitileTests/StreakTests.swift` untouched; add `GravitileTests/ModeLifecycleTests.swift`
  (AppModel with temp-file PersistenceService: newZen/newSprint create right modes;
  recordGameEnd updates `bestZenTile`/`bestSprintScore`; checkpoint stores per-mode slots)

- [ ] Steps: failing tests → implement → app tests PASS → sim screenshot Home shows 4 entries → commit
  `feat(app): zen and sprint playable end-to-end`.

### Task 4: Audio — BGM + new SFX

**Files:**
- Modify: `Tools/gensounds.swift` (add `whoosh`, `land`, `tap`, `milestone`,
  `newbest`, `bgm` — bgm: 48 s pad progression Am–F–C–G at ~0.10 amplitude,
  detuned sine pairs + slow sine LFO, pentatonic bell every 2 beats at −24 dB,
  10 ms loop-end crossfade), regenerate `Gravitile/Resources/Sounds/*.wav`
- Modify: `Gravitile/Services/SoundService.swift` (BGM player, `isMusicEnabled`,
  new effect funcs), `Gravitile/App/SettingsScreen.swift` (+ Music toggle),
  `Gravitile/App/AppModel.swift` (wire setting), `Gravitile/Game/GameScreen.swift`
  (`.onAppear` startMusic / `.onDisappear` stopMusic; scene-phase pause)

- [ ] Regenerate sounds; `afinfo` sanity-check the WAVs; SoundService plays bgm at 0.22,
  `numberOfLoops = -1`; commit `feat(audio): generative ambient BGM + six new effects`.

### Task 5: Haptics expansion

**Files:** `Gravitile/Services/HapticsService.swift` (+ `rotationTick()`, `landing()`,
`milestone()`, `newBest()`), wiring in `GameViewModel` callbacks (Task 6).

- [ ] Commit `feat(haptics): rotation tick, landing settle, milestone, new-best patterns`.

### Task 6: Juice — score pops, shake, tilt, squash, milestones

**Files:**
- Modify: `Gravitile/Game/GameViewModel.swift` — new published state:
  `scorePops: [ScorePop]` (id, points, round), `shakeTrigger: Int` (increments on
  cascade round ≥2 with magnitude = round), `tiltCue: Direction?` during gravity
  step, `landedTileIDs: Set<Int>` per fall step, `milestone: Int?` (first
  256/512/1024/2048 per game, tracked in a `celebratedValues` set); new callbacks
  `onRotation`, `onLanding`, `onMilestone`, `onNewBest`.
- Modify: `Gravitile/Game/BoardView.swift` (shake offset via
  `.offset` driven by animatable jitter, tilt via `.rotationEffect`, landing squash
  `.scaleEffect(y:)` for `landedTileIDs`), `Gravitile/Game/GameScreen.swift`
  (score-pop overlay above ScoreBadge, milestone burst layer, wire callbacks to
  sounds/haptics), `Gravitile/Game/ParticleBurstView.swift` (milestone volley variant)
- All motion transform/opacity only; `reduceMotion` path: crossfade only, no
  shake/tilt/pops (existing branch in `animate(_:)`).
- Test: extend `GravitileTests` with `JuiceStateTests` (view-model level: milestone
  fires once per value per game; shakeTrigger only for round ≥2; pops accumulate & clear).

- [ ] Failing tests → implement → PASS → sim: autoplay soak, screenshot cascade → commit
  `feat(juice): score pops, cascade shake, gravity tilt, landing squash, milestones`.

### Task 7: Game Center — routing + surfacing + weekly board

**Files:**
- Modify: `Gravitile/Services/GameCenterService.swift`:

```swift
static let sprintLeaderboardID = "grv.sprint.best"
static let zenTileLeaderboardID = "grv.zen.tile"
static let dailyWeeklyLeaderboardID = "grv.daily.weekly"
```

  `submit(game:)` routes: endless→endless board; daily→daily + weekly recurring
  (`GKLeaderboard.loadLeaderboards(IDs:)` then `submitScore` on the loaded board so
  the score lands in the current occurrence); zen→zen tile board; sprint→sprint board;
  bestTile board for all modes. `GKAccessPoint.shared.isActive` managed from Home
  appear/disappear.
- Modify: `Gravitile/Stats/StatsScreen.swift` — "Leaderboards" row presenting
  `GKGameCenterViewController(state: .leaderboards)` via a `UIViewControllerRepresentable` sheet.
- Test: refactor submit paths behind a small `ScoreReporter` protocol seam; spy test
  asserts leaderboard-ID routing per mode (pure logic, no GameKit network).
- ASC side (deferred to Task 10): add 3 leaderboards via `Tools/publish_gamecenter.py`.

- [ ] Failing spy tests → implement → PASS → commit
  `feat(gamecenter): per-mode boards, weekly recurring daily, in-app leaderboards`.

### Task 8: watchOS companion

**Files:**
- Modify: `project.yml` — new target:

```yaml
  GravitileWatch:
    type: application.watchapp2
    platform: watchOS
    deploymentTarget: "10.0"
    sources: [GravitileWatch]
    dependencies:
      - package: GravitileKit
    info:
      path: GravitileWatch/Info.plist
      properties:
        CFBundleDisplayName: Gravitile
        WKCompanionAppBundleIdentifier: com.flutterly.gravitile
        WKRunsIndependentlyOfCompanionApp: true
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: com.flutterly.gravitile.watchkitapp
        CURRENT_PROJECT_VERSION: 9
        MARKETING_VERSION: "1.1"
```

  plus `Gravitile` target gains `dependencies: - target: GravitileWatch` (embed watch content).
- Create: `GravitileWatch/GravitileWatchApp.swift` (`@main`),
  `GravitileWatch/WatchGameView.swift` (5×5 board scaled to screen, swipe gesture →
  `applyMove`, score header, gravity arrow, long-press → new game, WKInterfaceDevice
  haptic per merge), `GravitileWatch/WatchGameStore.swift` (UserDefaults JSON persist
  of `GameState` + best score; mode `.zen`), `GravitileWatch/Info.plist` (generated keys note).
- Watch UI is a compact rendering (no AnimationPlanner phases — direct state sync with
  a short implicit animation; watch sessions are seconds long).

- [ ] `./Tools/generate.sh` → build for watch simulator via xcodebuild → runs, swipe
  works, screenshot → commit `feat(watch): standalone zen mini game for Apple Watch`.

### Task 9: Full verification pass

- [ ] `swift test --package-path GravitileKit` — all green.
- [ ] `xcodebuild test` Gravitile scheme on iPhone sim (simulatorId lookup first) — all
  green (StoreKit 4 stay disabled).
- [ ] Autoplay soak endless + sprint (GRAVITILE_AUTOPLAY=1) 3+ minutes, no hangs;
  screenshots of every screen; watch sim run.
- [ ] Commit any fixes; `docs/balance-report.md` note if sprint tuning changed.

### Task 10: Metadata, screenshots, ASC config

- [ ] Update `docs/appstore/listing.md` (description/keywords/What's New per spec §H).
- [ ] Recapture 6.9" screenshots (1290×2796) incl. mode row + cascade juice; watch
  screenshots from watch sim; store under `docs/appstore/screenshots/`.
- [ ] ASC via scratchpad venv python: register `com.flutterly.gravitile.watchkitapp`
  bundle ID; create 3 new leaderboards (weekly one: `POST /v1/gameCenterLeaderboards`
  with `submissionType: BEST_SCORE`, `scoreSortType: DESC`, weekly
  `recurrenceRule`/`startDate` per API docs; verify against live API).
- [ ] Push metadata via `Tools/publish_metadata.py` once v1.1 version exists on ASC.
- [ ] Commit `docs: v1.1 listing, screenshots, publishing updates`.

### Task 11: Release & submission gate

- [ ] Bump `project.yml`: `MARKETING_VERSION "1.1"`, `CURRENT_PROJECT_VERSION 9`;
  `./Tools/generate.sh`; commit; push.
- [ ] Trigger `.github/workflows/release.yml` (gh workflow run) — archive/upload build 9
  from released-macOS runner. NEVER archive locally.
- [ ] Check v1.0 state (`Tools/ascapi.py GET /v1/apps/6786840477/appStoreVersions`).
  - Approved/released → create v1.1 ASC version, attach build 9, submit.
  - Still waiting → report to user with exact next commands; optionally schedule a
    check. Do NOT cancel the queued v1.0 submission autonomously.
- [ ] Tag `v1.1.0` after submission.

---

## Self-review notes

- Spec coverage: A (done pre-plan, commit pending) B→1,2,3 C→4 D→5 E→6 F→7,10 G→8
  H→10 I→11. Testing §4 → Tasks 1-3,6,7,9. ✔
- Watch target `application.watchapp2` + embed-via-dependency is the XcodeGen pattern;
  verify generated project embeds watch app in iOS product during Task 8 build.
- Weekly recurring leaderboard ASC API fields must be verified against live docs at
  Task 10 (research flagged naming drift risk).
