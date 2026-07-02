# Gravitile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Gravitile — a rotating-gravity merge puzzle for iOS with endless + daily modes, StoreKit 2 monetization, and Game Center — production-ready through App Store submission prep.

**Architecture:** Pure deterministic Swift engine (`GravitileKit`, local SPM package, zero UI deps) emits `MoveResult` event data; the SwiftUI app target consumes it via an animation planner. XcodeGen generates the Xcode project. All randomness flows through a Codable seeded RNG so every game is replayable and testable.

**Tech Stack:** Swift 6.x, Swift Testing (engine/app tests), XCTest (UI tests), SwiftUI, CoreHaptics, AVFoundation, StoreKit 2, GameKit, XcodeGen, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-07-02-gravitile-design.md` — rules in §3 are normative; when this plan and the spec disagree, the spec wins.

---

## Grid conventions (used everywhere)

- `Coordinate(row:col:)`, row 0 = top, col 0 = left, 5×5.
- Gravity cycle (clockwise): `.down → .left → .up → .right → .down`.
- Tile IDs are `Int`s issued by an incrementing counter in game state (deterministic; stable for SwiftUI animation identity).
- Values stored as the displayed number (2, 4, 8, …).

---

## Phase 1 — Scaffold

### Task 1: GravitileKit SPM package

**Files:**
- Create: `GravitileKit/Package.swift`
- Create: `GravitileKit/Sources/GravitileKit/Direction.swift` (stub for now)
- Create: `GravitileKit/Tests/GravitileKitTests/DirectionTests.swift`

- [ ] **Step 1: Create package manifest**

```swift
// GravitileKit/Package.swift
// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "GravitileKit",
    platforms: [.iOS(.v17), .macOS(.v14)],
    products: [.library(name: "GravitileKit", targets: ["GravitileKit"])],
    targets: [
        .target(name: "GravitileKit"),
        .testTarget(name: "GravitileKitTests", dependencies: ["GravitileKit"]),
    ]
)
```

macOS platform is included so `swift test` runs natively (fast, no simulator).

- [ ] **Step 2: Write failing test** (`DirectionTests.swift`)

```swift
import Testing
@testable import GravitileKit

@Test func gravityRotatesClockwiseThroughFullCycle() {
    #expect(Direction.down.rotatedClockwise == .left)
    #expect(Direction.left.rotatedClockwise == .up)
    #expect(Direction.up.rotatedClockwise == .right)
    #expect(Direction.right.rotatedClockwise == .down)
}
```

- [ ] **Step 3: Run to verify failure** — `cd GravitileKit && swift test` → FAIL (Direction undefined)
- [ ] **Step 4: Implement `Direction`**

```swift
public enum Direction: String, CaseIterable, Sendable, Codable, Hashable {
    case up, down, left, right

    /// Gravity cycle: down → left → up → right → down (visually clockwise).
    public var rotatedClockwise: Direction {
        switch self {
        case .down: .left
        case .left: .up
        case .up: .right
        case .right: .down
        }
    }

    public var opposite: Direction {
        switch self {
        case .up: .down
        case .down: .up
        case .left: .right
        case .right: .left
        }
    }

    /// Unit step in grid space (row delta, col delta), row 0 at top.
    public var step: (dr: Int, dc: Int) {
        switch self {
        case .up: (-1, 0)
        case .down: (1, 0)
        case .left: (0, -1)
        case .right: (0, 1)
        }
    }
}
```

- [ ] **Step 5: Run tests** — `swift test` → PASS
- [ ] **Step 6: Commit** — `git add GravitileKit && git commit -m "Scaffold GravitileKit package with Direction"`

### Task 2: XcodeGen app target

**Files:**
- Create: `project.yml`
- Create: `Gravitile/App/GravitileApp.swift`, `Gravitile/App/RootView.swift`
- Create: `Gravitile/Resources/Assets.xcassets` (accent + app icon placeholders)
- Create: `Gravitile/Info.plist` values via project.yml (generated Info.plist)

- [ ] **Step 1: Write `project.yml`**

```yaml
name: Gravitile
options:
  bundleIdPrefix: com.flutterly
  deploymentTarget:
    iOS: "17.0"
  createIntermediateGroups: true
packages:
  GravitileKit:
    path: GravitileKit
settings:
  base:
    SWIFT_VERSION: "6.0"
    TARGETED_DEVICE_FAMILY: "1"
    INFOPLIST_KEY_UILaunchScreen_Generation: YES
    INFOPLIST_KEY_UISupportedInterfaceOrientations: UIInterfaceOrientationPortrait
    CURRENT_PROJECT_VERSION: 1
    MARKETING_VERSION: 1.0.0
targets:
  Gravitile:
    type: application
    platform: iOS
    sources: [Gravitile]
    dependencies:
      - package: GravitileKit
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: com.flutterly.gravitile
        INFOPLIST_KEY_CFBundleDisplayName: Gravitile
  GravitileUITests:
    type: bundle.ui-testing
    platform: iOS
    sources: [GravitileUITests]
    dependencies:
      - target: Gravitile
schemes:
  Gravitile:
    build:
      targets: { Gravitile: all }
    test:
      targets: [GravitileUITests]
```

(App-logic tests that need iOS frameworks live in `GravitileKitTests` where possible; a `GravitileTests` unit bundle is added in Phase 6 when persistence tests need the app module.)

- [ ] **Step 2: Minimal app** — `GravitileApp.swift` with `@main struct GravitileApp: App` showing `RootView` (title text placeholder).
- [ ] **Step 3: Generate + build** — `xcodegen generate`, then build for iPhone 17 Pro simulator; launch and screenshot via XcodeBuildMCP. Expected: app launches showing placeholder.
- [ ] **Step 4: Commit** — include `project.yml`, exclude generated `Gravitile.xcodeproj`? **No — commit the generated project too** (CI and fresh clones then work without xcodegen; regenerate on structure changes).

---

## Phase 2 — Engine (strict TDD; every task: failing tests → implement → pass → commit)

### Task 3: Coordinate, Tile, Board

**Files:**
- Create: `GravitileKit/Sources/GravitileKit/{Coordinate,Tile,Board}.swift`
- Test: `GravitileKit/Tests/GravitileKitTests/BoardTests.swift`

Contracts:

```swift
public struct Coordinate: Hashable, Codable, Sendable {
    public var row: Int, col: Int
    public init(row: Int, col: Int)
    public func offset(by d: Direction) -> Coordinate
}

public struct Tile: Identifiable, Hashable, Codable, Sendable {
    public let id: Int
    public var value: Int
}

public struct Board: Equatable, Codable, Sendable {
    public static let size = 5
    public init()                                   // empty
    public subscript(_ c: Coordinate) -> Tile? { get set }
    public var tiles: [(Coordinate, Tile)]          // occupied cells
    public var emptyCoordinates: [Coordinate]       // row-major order
    public var isFull: Bool
    /// Lines of coordinates ordered from the `direction` edge inward.
    /// For .down: 5 columns, each ordered row 4 → row 0. Used by slide/fall/cascade.
    public static func lines(toward direction: Direction) -> [[Coordinate]]
}
```

Tests: subscript round-trip, emptyCoordinates ordering, `lines(toward:)` for all four directions (assert exact coordinate sequences for at least .down and .left), Codable round-trip.

### Task 4: SeededRNG + daily seed

**Files:**
- Create: `GravitileKit/Sources/GravitileKit/SeededRNG.swift`
- Test: `GravitileKitTests/SeededRNGTests.swift`

```swift
public struct SplitMix64: RandomNumberGenerator, Codable, Equatable, Sendable {
    public private(set) var state: UInt64
    public init(seed: UInt64) { state = seed }
    public mutating func next() -> UInt64 {
        state &+= 0x9E3779B97F4A7C15
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58476D1CE4E5B9
        z = (z ^ (z >> 27)) &* 0x94D049BB133111EB
        return z ^ (z >> 31)
    }
}

public enum DailySeed {
    /// FNV-1a 64-bit of "gravitile-YYYY-MM-DD" (UTC), plus day number since 2026-07-01.
    public static func seed(for date: Date) -> UInt64
    public static func puzzleNumber(for date: Date) -> Int   // #1 on 2026-07-06 (launch anchor)
}
```

Tests: same seed ⇒ same sequence; different seeds differ; Codable round-trip preserves stream position; fixed date ⇒ fixed known seed value (golden); puzzle number monotonic across day boundary in UTC (construct dates with `ISO8601DateFormatter`).

### Task 5: Slide + merge resolver

**Files:**
- Create: `GravitileKit/Sources/GravitileKit/MoveResolver.swift` (namespace enum `MoveResolver`)
- Create: `GravitileKit/Sources/GravitileKit/MoveEvents.swift`
- Test: `GravitileKitTests/SlideTests.swift`

Event types (consumed by the app's animation planner — design carefully):

```swift
public struct TileMove: Equatable, Codable, Sendable {
    public let tileID: Int
    public let from: Coordinate
    public let to: Coordinate
}
public struct MergeEvent: Equatable, Codable, Sendable {
    public let consumedTileIDs: [Int]   // exactly 2
    public let resultTile: Tile         // new id, doubled value
    public let at: Coordinate
    public let points: Int              // value × multiplier
    public let multiplier: Int          // 1 in slide phase; cascade round otherwise
}
public struct SlideOutcome: Equatable, Sendable {
    public let board: Board
    public let moves: [TileMove]
    public let merges: [MergeEvent]
    public var changed: Bool
}
```

```swift
enum MoveResolver {
    /// 2048 semantics toward `direction`. `nextTileID` is an inout counter for merge-result tiles.
    static func slide(_ board: Board, toward direction: Direction, nextTileID: inout Int) -> SlideOutcome
    static func fall(_ board: Board, gravity: Direction) -> (board: Board, moves: [TileMove])
    /// One cascade round: merge equal gravity-axis neighbors (edge-nearest pair first,
    /// one merge per tile per round). Returns nil when no merges (fixed point).
    static func cascadeRound(_ board: Board, gravity: Direction, round: Int, nextTileID: inout Int) -> (board: Board, merges: [MergeEvent])?
}
```

Slide algorithm per line (ordered from target edge): collect tiles in order; walk with write index; merge when consecutive equal and neither already merged this move; result tile gets fresh ID. Record `TileMove` for every tile whose coordinate changed and `MergeEvent` per merge (consumed tiles' moves recorded to the merge coordinate).

Slide tests (each asserts full board layout + events):
- single tile slides to edge in each of 4 directions
- `[2,2,_,_,_] →left` = `[4,_,_,_,_]`, one merge, points 4
- `[2,2,2,_,_] →left` = `[4,2,_,_,_]` (edge-nearest pair merges)
- `[2,2,2,2,_] →left` = `[4,4,_,_,_]` (two merges, no re-merge)
- `[4,2,2,_,_] →left` = `[4,4,_,_,_]` (no chain into 8 within one slide)
- full line of distinct values → `changed == false` when nothing moves
- merges produce fresh tile IDs; consumed IDs reported

### Task 6: Fall + cascade

**Test:** `GravitileKitTests/CascadeTests.swift`

Fall tests: tiles compact toward gravity preserving order, no merging, correct `TileMove`s; already-settled board returns empty moves.

Cascade tests (build boards directly):
- vertical `[2,2]` on gravity edge after fall → merges to `[4]`, multiplier = round number
- three-stack `[2,2,2]` (gravity .down, col from bottom: 2,2,2) → round 1 merges bottom pair → `[4,2]`, round 2: no merge (4≠2) → stops
- chain: `[4,2,2]` bottom-to-top → round 1: 2+2→4 above the 4 → falls → `[4,4]` → round 2: →`[8]`, multiplier 2, points 8×2
- multiplier math: round n merge of value v scores v×n
- no infinite loops: fixed point returns nil

### Task 7: Full move pipeline + spawn

**Files:**
- Create: `GravitileKit/Sources/GravitileKit/MoveResult.swift`
- Modify: `MoveResolver.swift` (add `resolveMove`)
- Test: `GravitileKitTests/MovePipelineTests.swift`

```swift
public struct CascadePhase: Equatable, Sendable {
    public let falls: [TileMove]
    public let merges: [MergeEvent]   // empty in final settling phase
    public let round: Int             // 0 for the initial post-rotation fall
}
public struct SpawnEvent: Equatable, Sendable {
    public let tile: Tile
    public let enteredAt: Coordinate  // opposite-gravity edge cell of chosen column
    public let restedAt: Coordinate
}
public struct MoveResult: Equatable, Sendable {
    public let swipe: Direction
    public let slide: SlideOutcome
    public let newGravity: Direction
    public let phases: [CascadePhase] // fall/merge alternation until stable
    public let spawn: SpawnEvent?
    public let scoreDelta: Int
}
```

```swift
static func resolveMove(board: Board, swipe: Direction, gravity: Direction,
                        rng: inout SplitMix64, nextTileID: inout Int) -> MoveResult?
// nil if slide phase doesn't change board (illegal swipe).
// Pipeline: slide → rotate gravity → fall(round 0) → repeat [cascadeRound n, fall] → spawn.
// Spawn: choose uniformly (seeded) among gravity-relative columns with ≥1 empty cell;
// value 2 with p=0.9 else 4 (rng.next() % 10 < 9); tile enters at opposite edge, rests
// at furthest empty cell toward gravity in that column; NEVER merges on landing.
```

Tests:
- illegal swipe (nothing changes in slide phase) → nil, board untouched
- legal swipe: gravity rotated, all tiles settled toward new gravity, spawn present & rested correctly
- spawn probability: with seed sweep over 1000 spawns, 4-rate within [5%, 15%]
- spawn skipped on full board
- determinism golden test: seed 42, scripted 10-move sequence ⇒ exact final board, score, gravity (compute once, then freeze as golden)

### Task 8: GameState

**Files:**
- Create: `GravitileKit/Sources/GravitileKit/GameState.swift`
- Test: `GravitileKitTests/GameStateTests.swift`

```swift
public enum GameMode: Equatable, Codable, Sendable {
    case endless
    case daily(puzzleNumber: Int, moveBudget: Int)   // budget = 40
}

public struct GameState: Codable, Equatable, Sendable {
    public private(set) var board: Board
    public private(set) var gravity: Direction        // starts .down
    public private(set) var score: Int
    public private(set) var bestTile: Int
    public private(set) var moveCount: Int
    public let mode: GameMode
    public private(set) var undosUsed: Int

    public init(mode: GameMode, seed: UInt64)         // spawns 2 starting tiles (inert)
    public var movesRemaining: Int?                   // nil for endless
    public var isGameOver: Bool                       // budget exhausted or no legal swipe
    public var hasLegalMove: Bool
    @discardableResult
    public mutating func applyMove(_ direction: Direction) -> MoveResult?
    public mutating func undo() -> Bool               // restores full prior state (incl. rng, budget)
    public var canUndo: Bool
}
```

History: keep `private var history: [Snapshot]` where `Snapshot` captures core fields (board, gravity, score, bestTile, moveCount, rng, nextTileID), bounded to last 20; `undosUsed` survives undo (it is *not* part of the snapshot restore) so entitlement gating can count honestly.

Tests: init spawns exactly 2 tiles with seeded placement; applyMove updates score/moveCount/bestTile; illegal move returns nil and mutates nothing; undo round-trips to exact prior state and increments undosUsed; undo at start returns false; daily budget decrement + game-over at 0; endless game-over on stuck board (construct a full checkerboard board via a test-only `init(board:gravity:mode:...)` marked `@_spi(Testing)` or internal + `@testable`); Codable round-trip mid-game preserves determinism of subsequent moves.

### Task 9: ShareCard

**Files:**
- Create: `GravitileKit/Sources/GravitileKit/ShareCard.swift`
- Test: `GravitileKitTests/ShareCardTests.swift`

```swift
public enum ShareCard {
    /// e.g.
    /// Gravitile #12 — 4,320
    /// 🟪🟦🟩🟨🟧  (best-tile progression row: one emoji per power tier reached)
    /// 🔁 x7 cascades · 🏆 512
    /// Deterministic, locale-independent numbers (grouping via fixed en_US_POSIX).
    public static func text(for state: GameState, cascadeCount: Int) -> String
}
```

Golden-string tests for zero-cascade and multi-cascade games.

**Phase 2 exit criteria:** `swift test` green with 60+ assertions across 7 suites; commit + push after each task.

---

## Phase 3 — Balance simulation

### Task 10: Monte-Carlo balance harness

**Files:**
- Create: `GravitileKit/Sources/BalanceSim/main.swift` (executable target added to Package.swift)
- Create: `docs/balance-report.md`

- [ ] Executable runs N games (default 2000) with two policies: `random` (uniform legal move) and `greedy` (1-ply max scoreDelta), prints median/p10/p90 game length, score, best tile, cascade frequency, for endless and daily(40).
- [ ] Run it: `swift run -c release BalanceSim`. Targets: random-policy endless median game length 40–120 moves; greedy noticeably better than random (sanity that skill matters); daily(40) greedy median score meaningfully above random (≥1.5×).
- [ ] If outside targets, tune (in order): 4-spawn probability, spawn-column rule, cascade multiplier cap. Re-run engine tests after any change (goldens may need regen — regenerate deliberately, never blindly).
- [ ] Record results + chosen constants in `docs/balance-report.md`; commit.

---

## Phase 4 — Game UI (endless mode playable end-to-end)

### Task 11: Theme + design tokens

**Files:** `Gravitile/App/Theme.swift`, update `Assets.xcassets`

- `Theme` struct: OKLCH-derived color ramp for tile values 2→65536 (16 steps, heat progression, chroma reduced at lightness extremes), board background `Color(.displayP3, …)` navy-tinted neutrals (no pure black/white), text styles. Display face: bundle an OFL-licensed distinctive font (e.g. "Bricolage Grotesque" for numerals/logo; body: system rounded). Register via `UIAppFonts`.
- Font licensing note: only OFL/Apache fonts; keep license file in `Gravitile/Resources/Fonts/`.

### Task 12: GameViewModel + AnimationPlanner

**Files:** `Gravitile/Game/GameViewModel.swift`, `Gravitile/Game/AnimationPlanner.swift`, `Gravitile/Game/TileViewState.swift`
**Test:** planner logic lives in plain functions; unit-test phase timings in `GravitileKitTests`-style app test bundle later (Phase 6); for now verify via simulator.

```swift
@Observable @MainActor final class GameViewModel {
    private(set) var game: GameState
    private(set) var displayTiles: [TileViewState]   // id-stable, animated positions
    private(set) var isAnimating: Bool
    var freeUndosRemaining: Int                       // 1 unless Plus
    func handleSwipe(_ direction: Direction)          // guard !isAnimating; applyMove; run planner
    func undoTapped()
    func newGame()
}

struct TileViewState: Identifiable, Equatable {
    let id: Int
    var value: Int
    var coordinate: Coordinate
    var scale: CGFloat        // pop on spawn/merge
    var isMergeResult: Bool
}
```

`AnimationPlanner.run(result: MoveResult, apply: (Phase) async -> Void)`: sequences slide (0.14s spring) → gravity cue (board nudge, 0.1s) → per cascade phase: fall (0.16s) + merge pulse (0.1s, haptic hook) → spawn pop (0.12s). Uses `withAnimation` + `Task.sleep` between phases. Total move < 0.8s worst case; interruption forbidden (input gated by `isAnimating`).

### Task 13: BoardView + TileView + gestures

**Files:** `Gravitile/Game/{BoardView,TileView,GameScreen}.swift`

- `BoardView`: `GeometryReader`-sized grid; background cell wells; `ForEach(viewModel.displayTiles)` positioned by coordinate → `.position()`, animated. Tile: rounded rect, value-colored fill, auto-scaling numeral text, no drop-shadow default (subtle inner depth via overlay stroke).
- Swipe: `DragGesture(minimumDistance: 24)` on the board; direction = dominant axis of translation; fires once per gesture.
- Verify on simulator via XcodeBuildMCP: play 10 manual moves via `snapshot_ui`/gestures, screenshot, confirm no visual glitches.

### Task 14: HUD — score, gravity compass, game over

**Files:** `Gravitile/Game/{HUDView,GravityCompass,GameOverOverlay}.swift`

- Compass ring around board: 4 dots; filled dot = current gravity, pulsing hollow = next. Also arrow chevrons on board edge.
- Score with rolling number animation; best score; move counter (daily).
- Game over: overlay (not modal) with score summary, best-tile, New Game, Share (endless: score text).
- Accessibility: every element labeled; board exposes custom rotor summary string ("5 by 5 board, top row: 2, empty, …"); `accessibilityIdentifier`s for UI tests: `board`, `score`, `newGameButton`, swipe via `accessibilityAction`s (up/down/left/right) so XCUITest can play.
- Reduced motion: planner swaps movement for crossfade.

**Phase 4 exit:** endless mode fully playable, screenshot set captured, committed & pushed.

---

## Phase 5 — Feel

### Task 15: Haptics, sound, particles

**Files:** `Gravitile/Services/{HapticsService,SoundService}.swift`, `Gravitile/Game/ParticleBurstView.swift`

- Haptics: CoreHaptics engine wrapper; `.merge(round:)` intensity/sharpness rises with cascade round; graceful no-op on unsupported/denied. Settings toggle.
- Sound: 6 short caf samples generated programmatically offline (sine/FM blips, pitch rises with cascade round) via a small Swift script (`Tools/gensounds.swift` run with `swift Tools/gensounds.swift` writing PCM→caf) — committed as assets. `AVAudioPlayer` pool; respects silent switch (`.ambient` category); mute toggle persisted.
- Particles: `ParticleBurstView` — `TimelineView(.animation)` + `Canvas`, 12–24 sparks from merge coordinate on cascade rounds ≥2; disabled under reduced motion.

---

## Phase 6 — Modes, persistence, meta

### Task 16: Persistence service + app test bundle

**Files:** `Gravitile/Services/PersistenceService.swift`; add `GravitileTests` unit-test target to project.yml (`type: bundle.unit-test`, host Gravitile)

```swift
struct PersistedState: Codable {   // versioned envelope: {version: 1, payload: …}
    var endlessGame: GameState?
    var dailyGames: [Int: DailyRecord]   // puzzleNumber → record
    var bestEndlessScore: Int
    var settings: Settings               // sound, haptics, theme id
    var streak: StreakState
}
final class PersistenceService {       // JSON file in Application Support, atomic writes
    func load() -> PersistedState
    func save(_ state: PersistedState)
}
```

Tests (Swift Testing, in `GravitileTests`): round-trip, corrupt-file recovery (returns fresh state, preserves nothing but doesn't crash), version envelope tolerates unknown future fields.

### Task 17: Daily mode + streaks + share

**Files:** `Gravitile/Daily/{DailyScreen,StreakState,DailyRecord}.swift`
**Test:** `GravitileTests/StreakTests.swift`

```swift
struct StreakState: Codable, Equatable {
    var current: Int
    var longest: Int
    var lastCompletedPuzzle: Int?
    var freezesUsedThisWeek: Int
    /// Completing puzzle n: n == last+1 → current+1; n == last+2 && freeze available → current+1, freeze consumed; else reset to 1.
    mutating func recordCompletion(puzzleNumber: Int)
}
```

- Daily screen: today's puzzle (UTC), moves-remaining ring, streak flame, results state once played (score, share button, countdown to next). Share sheet with `ShareCard.text`.
- Past dailies list — locked rows tease Plus (Phase 7 gates them).
- Streak tests: consecutive, gap-of-1 with freeze, gap-of-2 resets, freeze resets weekly (ISO week of completion), longest tracked.

### Task 18: Stats screen

**Files:** `Gravitile/Stats/StatsScreen.swift`

Games played, best score, best tile, total merges, cascade record, daily history strip (last 30 days, mini bar per day — real data, not decoration), streaks. Swift Charts optional; simple custom bars fine.

### Task 19: Tutorial

**Files:** `Gravitile/Game/TutorialOverlay.swift`, first-launch flag in settings

5 interactive steps on a scripted 3-tile board (fixed seed): 1) swipe to merge 2) watch gravity rotate (compass highlighted) 3) tiles fall 4) cause a cascade 5) free play prompt. Each step advances only when the taught action is performed. Skippable.

### Task 20: Root navigation

**Files:** rewrite `Gravitile/App/RootView.swift`

Home: big Play (endless resume/new), Daily card (state-aware: not played / played score), Stats, Settings (sound, haptics, theme picker, restore purchases, tip jar, licenses, privacy link). No tab bar — single stack, board is the hero.

---

## Phase 7 — Revenue + Game Center

### Task 21: StoreKit 2

**Files:** `Gravitile/Store/{StoreService,PaywallView,TipJarView,Entitlements}.swift`, `Gravitile/Gravitile.storekit` (StoreKit config), project.yml scheme gains `storeKitConfiguration`
**Test:** `GravitileTests/StoreTests.swift` using `StoreKitTest` (`SKTestSession`)

Products: `com.flutterly.gravitile.plus` (non-consumable, $2.99 tier), `…tip.small|medium|large` (consumables $0.99/$2.99/$9.99).

```swift
@Observable @MainActor final class StoreService {
    private(set) var isPlus: Bool
    private(set) var products: [Product]
    func loadProducts() async
    func purchase(_ product: Product) async throws -> Bool
    func restore() async
    private func updateEntitlements() async   // Transaction.currentEntitlements
    // Transaction.updates listener task started at init
}
```

Gating: `Entitlements.maxUndosPerGame(isPlus:)` (1 vs ∞), daily archive locked rows → paywall, premium themes marked in picker. Paywall: single screen, feature list, price from `Product.displayPrice`, restore link, no dark patterns (App Review safe).

StoreTests: purchase unlocks isPlus; restore after "reinstall" (fresh session) restores; tip consumable doesn't affect entitlements; revoked transaction clears isPlus.

### Task 22: Game Center

**Files:** `Gravitile/Services/GameCenterService.swift`

- Authenticate on launch (silent; UI only if GC presents it), submit scores: leaderboards `endless.best`, `daily.score` (daily recurring), `best.tile`. Achievements (10): first merge, first cascade, cascade x3, x5, 256/512/1024/2048 tile, 7-day streak, 30-day streak. All IDs namespaced `grv.…` and listed in `docs/publishing-runbook.md` for App Store Connect setup.
- Degrades silently when unauthenticated; access point shown on home only when authenticated.

---

## Phase 8 — Ship

### Task 23: App icon + launch polish

- Icon: geometric mark — rounded-square tile falling into a 3×3 dot grid rotated 15°, navy field, heat-orange tile (matches theme ramp). Generate 1024px master SVG→PNG via a small Swift/Python script or hand-built vector in code rendered offscreen; all sizes via `Assets.xcassets` single-size icon. No text in icon.
- Launch screen: generated (plain board-navy background) — already configured in Task 2.

### Task 24: UI tests

**Files:** `GravitileUITests/GravitileUITests.swift`

Smoke: launch → title exists; start endless → perform 5 accessibility swipe actions → score label changes; open daily; open settings; open paywall (products stubbed via StoreKit config). Run on simulator via `test_sim`.

### Task 25: CI

**Files:** `.github/workflows/ci.yml`

Jobs on push/PR to main: (1) `swift test` in GravitileKit on `macos-15`; (2) `xcodebuild test` Gravitile scheme, iPhone 16/17 simulator (best available on runner image), with `-skipMacroValidation`. Cache SPM. ~10 min budget.

### Task 26: App Store metadata + privacy

**Files:** `docs/appstore/{description.md,keywords.txt,privacy-policy.md,privacy-labels.md,review-notes.md,age-rating.md}`

- Description (4000 char max), subtitle (30), keywords (100), promotional text. Category: Games > Puzzle. Privacy: no data collected (Game Center is Apple-operated — label per current guidance); policy hosted as GitHub Pages from repo `docs/` or in-repo link for now.
- What's New v1.0.0 via app-store-changelog skill conventions.

### Task 27: Screenshots

Use simulator captures (iPhone 17 Pro for 6.9" + resize pipeline) via XcodeBuildMCP `screenshot` at curated game states (mid-cascade with particles, daily result, stats, themes). Frame + caption via the app-store-screenshots approach if time permits; raw captures acceptable for v1. Store under `docs/appstore/screenshots/`.

### Task 28: Archive + publishing runbook

**Files:** `docs/publishing-runbook.md`, `ExportOptions.plist`

- `xcodebuild archive` (Release, generic iOS device, `-allowProvisioningUpdates` documented but requires signing) — verify archive succeeds unsigned (`CODE_SIGNING_ALLOWED=NO` build check) and document the signed path.
- Runbook: exact App Store Connect steps — create app record, bundle ID, IAP products (IDs above), Game Center leaderboards/achievements (IDs above), upload build (Xcode Organizer or `xcrun notarytool`/altool alternatives), TestFlight, submit. Flag: **requires user's Apple Developer Program membership** — the only step the agent cannot perform.

---

## Self-review notes

- Spec §3 rules → Tasks 5–8; §4 architecture → Tasks 1–2, 12; §5 → Tasks 11, 13–15; §6 → Tasks 17, 21–22; §7 testing → Tasks 3–9, 16–17, 21, 24, 25; §8 → Tasks 26–28; §9 balance risk → Task 10, readability risk → Tasks 14, 19.
- Naming consistency check: `rotatedClockwise`, `resolveMove`, `CascadePhase.round`, `TileViewState` used consistently above.
- Commit after every task minimum; push after every phase minimum.
