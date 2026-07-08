# Gravitile v1.3 — "Math Pop & Palettes" Design Document

Date: 2026-07-08
Status: as-built
Trigger: App Review rejected v1.0 (build 8) under Guideline 4.3(a) — Design — Spam
("shares a similar … concept as apps submitted by other developers, with only
minor differences"). The engine and every asset are original, so the correct
response is not a dispute alone but visible, distinctive content no merge-clone
has: an arithmetic-learning game mode and a strong color identity.

## 1. Goal

Two features that make Gravitile obviously not-a-2048-clone at first glance:

- **A. Math Pop mode** — a kid-friendly arithmetic trainer built on the
  tumbling-gravity engine. Tiles carry small numbers (1–9); two adjacent tiles
  merge only when they **add up to the current target** ("Make 10"), then pop
  off the board with their equation shown ("3 + 7 = 10"). Targets progress in
  stages, teaching number bonds — the pairs-that-make-N skill drilled in early
  primary math. Tile colors follow the Cuisenaire rod system used in real
  classrooms.
- **B. Palettes** — five hand-tuned OKLCH color themes (Ember, Tidepool,
  Meadow, Aurora, Sorbet) selectable in Settings, covering dark and light
  boards. Free for everyone: differentiation matters more than upsell here.

Non-goals: Game Center boards/achievements for Math Pop (v1.4 once the mode has
telemetry), Hammer powerup and run history (still deferred), watch-app themes.

## 2. Scope

In: GravitileKit merge-rule generalization + math mode, math UI (home card,
target HUD, equation pops, stage celebrations, first-run hint), theme system +
Settings picker, listing copy, 4.3(a) resolution-center reply draft, version
1.3 (build 11).

Out: any change to the doubling modes' RNG streams, merge semantics, scoring,
or daily determinism. Golden games must stay byte-identical.

## 3. Engine design (GravitileKit)

### 3.1 MergeRule

```swift
public enum MergeRule: Equatable, Sendable {
    case doubling            // existing behavior, default everywhere
    case sumTarget(Int)      // a+b == target merges; result value == target
}
```

`MoveResolver.slide`, `cascadeRound`, `resolveMove`, and `spawn` gain a
`rule: MergeRule = .doubling` parameter. Every existing call site compiles
unchanged; `.doubling` paths are bit-for-bit the old code.

Under `.sumTarget(t)`:
- Slide/cascade pairing merges when `a + b == t` (equal pairs like 5+5=10
  included). Result tile value is `t`; points = result value × round, i.e. the
  existing scoring code holds for both rules.
- **Bond clear:** immediately after each merge pass, result tiles with value
  `== t` are removed (`ClearEvent`), before the fall compacts the gap. Board
  invariant: no resting tile ever equals the target (spawns < target; bonds
  clear on creation), so leftover tiles from earlier stages never collide with
  a rising target.
- Spawns draw values uniformly from `max(1, t-9)...min(9, t-1)` — every value
  in range has its complement in range, so no unpairable tile ever spawns.
  Spawn draws two RNG values (line, value) exactly like classic spawns.

### 3.2 GameMode.math + progression

`case math` joins GameMode (new case = decode-compatible; old saves never
contain it). `MathProgression` owns the curriculum:

- `targets`: 5 → 10 → 12 → 14 → 16, then looping 10…16 forever (9+9=18 caps
  what single-digit tiles can bond, and a 9+9-only stage would be degenerate).
- `bondsPerStage = 6`, `starterCount = 6` fresh tiles at game/stage start.
- `spawnRange(target)` as §3.1.

GameState grows `mathStage`, `bondsThisStage`, `bondsCleared` (all
decodeIfPresent-defaulted, snapshotted for undo). Mode policy: spawn 2 per
move, no boulders, no stasis, no move budget (zen-like; game over = board
locked). On crossing `bondsPerStage`, `applyMove` post-processes: stage+1,
board sweep (all tiles removed), `starterCount` fresh spawns for the new
target, `+newTarget×5` bonus points, all reported via
`MoveResult.stageAdvance: StageAdvance?` (mutable-after pattern, like
`heldGravity`) so the UI can animate sweep → banner → drop-in.

### 3.3 MoveEvents additions

```swift
public struct ClearEvent: Equatable, Codable, Sendable {
    public let tileID: Int        // popped result tile
    public let at: Coordinate
    public let value: Int         // == target
    public let addends: [Int]     // the two values that bonded, for "3 + 7 = 10"
}
public struct StageAdvance: Equatable, Sendable {
    public let newStage: Int
    public let newTarget: Int
    public let bonusPoints: Int
    public let sweptTileIDs: [Int]
    public let starterSpawns: [SpawnEvent]
}
```

`SlideOutcome.clears` and `CascadePhase.clears` default to `[]`.
`hasLegalMove` passes the game's rule so a full math board with equal-but-
unbondable neighbors correctly reads as locked.

## 4. App design

### 4.1 Math Pop UI

- Home: third full-width card (the differentiator deserves the billing) —
  "Math Pop · Make the target. Pop the tiles." with best score.
- Game header title "Math Pop"; best badge from `persisted.bestMathScore`.
- Target chip beside the gravity compass: "MAKE 10" + `bondsThisStage/6`
  progress dots.
- Equation pops: each ClearEvent floats "3 + 7 = 10" from the cleared cell
  (self-expiring, like score pops), with a merge sound/haptic tick.
- Stage advance: swept tiles fade, "Now make 12!" celebration (milestone
  pattern), starter tiles drop in.
- First-run hint banner (boulder-hint pattern, `hasSeenMathHint`): "Two tiles
  that ADD UP to 5 pop together. Gravity turns after every swipe!" The
  standard doubling tutorial never shows in math mode.
- Tiles ≤9 use Cuisenaire-inspired colors (1 cream, 2 red, 3 light green,
  4 lavender, 5 yellow, 6 dark green, 7 graphite, 8 terracotta, 9 blue),
  contrast-adjusted per WCAG; the target-valued tile flashes gold as it pops.
- Lifecycle: `persisted.mathGame` autosave, `bestMathScore`, records into
  `LifetimeStats` like other modes. ShareCard gains a math title line.
  Game Center: math submits nothing (no board exists; doubling-tile
  achievements/leaderboards would be semantically wrong).

### 4.2 Palettes

`ThemePalette` struct (chrome + tile ramp + text pairings + isLight);
`Theme.current` static holds the active palette and the existing `Theme.x`
statics become passthroughs, so no view changes except where palettes are
picked. Theme switches happen only on the Settings screen (never while a
game screen is on the nav stack), Home re-renders because Settings lives
inside the observed `persisted` struct, and game screens are freshly pushed —
so static-backed switching is safe without an environment refactor.

Palettes (all OKLCH-derived, tinted neutrals, AA text contrast):
Ember (current default), Tidepool (deep ocean, aqua→coral ramp), Meadow
(light cream chrome, garden ramp), Aurora (pine dusk, mint→magenta ramp),
Sorbet (light candy — pairs beautifully with Math Pop). Picker in Settings
with live swatch rows; `settings.themeID` persists; `preferredColorScheme`
follows `isLight`.

## 5. Testing

TDD throughout. New engine tests in `MathModeTests.swift`: sum-rule slide
merge + clear, non-bonding equal pair stays, spawn range per stage, stage
advance sweep + starter spawns + bonus, undo restores stage/bond counters,
Codable round-trip of a math game, `hasLegalMove` under sum rule, doubling
golden-stream regression (seeded endless game identical before/after).
App tests: mode lifecycle (`mathGame` resume/clear), GC routing returns no
entries for math, ShareCard math text. Theme: palette lookup + fallback,
settings round-trip.

## 6. Risks

- Regressing doubling modes → mitigated by default-parameter design + golden
  seeded-game test + full existing suite.
- Old-device saves: new GameState keys are decodeIfPresent; new GameMode case
  never appears in old files.
- Math mode balance (board flooding vs draining): spawn 2/move vs 2 tiles per
  bond ≈ equilibrium; stage sweep resets pathology; BalanceSim untouched
  (math is zen-class, not competitive).
