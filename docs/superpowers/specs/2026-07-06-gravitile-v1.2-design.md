# Gravitile v1.2 — "Stasis, Boulders & Bragging Rights" Design Document

**Date:** 2026-07-06
**Status:** Approved for implementation (autonomous session under `/goal`; decisions
grounded in the 5-agent competitor-research workflow run wf_1a8d5949-76b — full
reports in that run's output; user review welcome, all reversible)
**Baseline:** v1.1 (build 9) on TestFlight; v1.0 still WAITING_FOR_REVIEW. v1.2 work
lands on main and ships as build 10 whenever the review pipeline unblocks.

## 1. Goal

Per the user directive: research competitors; improve game UI and stats; add
powerups and hurdles if they fit; improve achievements/sharing.

Research verdicts that shaped the scope (fun-per-effort ranked):

- Pure-skill merge games (Threes, official 2048/Suika, Woodoku classic) are praised
  for restraint — booster economies (hammer/swap/shuffle for coins) are the genre's
  most-hated pattern. Ship at most two earn-by-play powerups; **never** shuffle,
  swap-two, board-clear, continue-lifelines, or any coin abstraction.
- The one powerup only Gravitile can have: **controlling when the world turns**.
- Obstacles that players like *fall with the board* and clear through play
  (Puyo ojama, Drop7 sealed discs). Static blockers and countdown bombs are the
  hated variants — and a non-tumbling tile would contradict this game's identity.
- Stats screens read premium when they show **six big records, distributions with
  a "you" marker, and streak calendars** — not raw totals or sparkline decoration.
- Sharing: **text emoji grid is the viral spine, image card is the reach** — one
  button, both formats via `Transferable`; share lives on results surfaces, never
  modal, never nagged. Failure must be shareable too.

## 2. Scope

| # | Feature | Effort |
|---|---------|--------|
| A | Stasis powerup (hold gravity for one move) | M |
| B | Boulder hurdle (iced tile: falls, never merges, shattered by adjacent merges) | M |
| C | Share upgrade: image card + text via one `Transferable`; achievements sharing | M |
| D | Stats upgrade: per-mode records block, daily distribution + calendar, number polish | M |
| E | Game Center Challenges eligibility (ASC-side config at publish; no in-app iOS-26-only code yet) | S |

Non-goals (explicitly rejected or deferred): Hammer powerup (v1.3, after Stasis
proves the charge system), Wildcard tile (v1.3), run-history list + per-move
efficiency grades (v1.3), shuffle/swap/board-clear/lifelines (never), countdown
bombs (never — brand collision), boulders/powerups on watch (watch stays pure zen).

## 3. Engine design (GravitileKit)

### 3.1 Stasis (A)

- `GameState.applyMove(_ direction: Direction, stasis: Bool = false)` — when
  `stasis` is true the pipeline runs slide→merge→fall→cascade→spawn **without
  rotating gravity** (`newGravity == gravity`). Encoded in `MoveResult` as
  `heldGravity: Bool` for animation/HUD.
- Charges are engine-owned (they must survive resume): `stasisCharges: Int`.
  Earned: +1 the first time each of {256, 512, 1024} is forged in a run (engine
  detects bestTile crossings in `applyMove`). Cap 2 banked. No carryover between
  games (they live in `GameState`).
- Mode policy in engine (`canUseStasis`): Endless — usable while charges > 0;
  **Zen — always usable, no charge bookkeeping**; **Daily & Sprint — never**
  (seeded/competitive integrity; matches undo staying honest there).
- Undo: snapshot includes `stasisCharges` and the held-gravity outcome restores
  exactly (a used charge comes back on undo — undo is a full state restore, and
  charge-burn-through is prevented by the unchanged board legality rule).

### 3.2 Boulder (B)

Model: `Tile.ice: Int = 0` — 0 is a normal tile; >0 is a boulder: a numbered tile
encased in ice with `ice` hit-points.

Rules (all deterministic, all from the seeded stream):
1. Boulders slide and fall exactly like tiles. They **never merge** while iced
   (excluded from slide-merge pairing and cascade pairing; they still compact).
2. Any merge that resolves **orthogonally adjacent** to a boulder removes 1 HP —
   in the slide phase and in every cascade round. Multiple merges can chip the
   same boulder in one round. Ice events are reported (`IceHit(tileID, at,
   hpAfter)`) per phase for animation/haptics.
3. At 0 HP the tile is freed **mid-resolution** and participates in subsequent
   cascade rounds normally — obstacle becomes combo fuel.
4. Spawn schedule (replaces one normal spawn; value drawn from the usual 2/4
   distribution; HP noted):
   - Endless: first boulder at move 40 (ramp onset), then every
     `max(8, 20 - movesPlayed/30)` moves; HP 1 before move 100, HP 2 after.
   - Daily: **exactly 2 per puzzle**, at seed-determined moves in 5..35, HP 1 —
     identical for every player, so share cards stay comparable.
   - Sprint: at most 1, after move 40, HP 1. Zen: none.
5. Scoring: shattering awards `10 × HP-at-spawn × cascade round multiplier`
   (min ×1) — chip value routed through the existing points channel.

Compatibility: `Tile` gains custom decoding (`ice` defaults 0) — v1.1 saved games
and the v1.1 fixture test must keep passing; golden ten-move game must be
unchanged (boulder schedule starts at move 40; seed streams for pre-boulder moves
are untouched because boulder spawns *replace* scheduled spawns rather than
drawing extra randoms... **invariant: RNG draw count per spawn is identical for
boulder and normal spawns**).

### 3.3 MoveEvents additions

- `SpawnEvent` unchanged (boulders arrive as spawns; `tile.ice` carries state).
- `SlideOutcome.iceHits: [IceHit]`, `CascadePhase.iceHits: [IceHit]` (default []
  — custom decoding not needed; MoveResult isn't persisted).
- `MoveResult.heldGravity: Bool`.

## 4. App design

### 4.1 Powerup & boulder UI

- GameScreen controls row gains a **Stasis button** (icon: `pause.circle` over a
  compass glyph) between Undo and New: shows charge dots (Endless) or ∞ (Zen);
  hidden entirely in Daily/Sprint. Tap to arm (button + compass highlight, the
  compass "next" arrow gets a lock badge), tap again to disarm; next swipe
  consumes. Armed state lives in `GameViewModel`.
- Earned-charge moment: milestone celebration already fires at 256/512/1024 —
  append "+1 Stasis" to the celebration when a charge was banked.
- Boulder rendering: `TileView` shows iced state — desaturated tile color under a
  stroked frost overlay + HP shown as cracks (2HP: intact frost; 1HP: cracked).
  `IceHit` animates a chip (scale pulse + frost particles), shatter reuses the
  burst view. New sounds: `chip.wav`, `shatter.wav` (gensounds). Haptics:
  `iceChip()` (sharp light), shatter reuses milestone-weight transient.
- Tutorial: one new play-along step appears the first time a boulder spawns
  ("Ice never merges — merge beside it to crack it free.") — a one-shot hint
  banner, not a tutorial rewrite (`Settings.hasSeenBoulderHint`).

### 4.2 Sharing (C)

- `ShareCard.text` upgrade: header gains moves used for daily
  ("Gravitile Daily #N — 1,234 · 34/40 moves"), a human-readable summary line
  (screen-reader friendly, per research), and the App Store URL footer.
- New `ShareCardView` (SwiftUI): 1080×1350 (4:5, feed-friendly) dark-navy card —
  wordmark, mode label, big score, best tile chip in its heat color, cascades +
  deepest round, the **final board rendered as a mini tile grid** (real tiles,
  not emoji), streak flame for daily, footer URL. Rendered via `ImageRenderer`
  (`scale = 3`, `@MainActor`).
- `ShareableResult: Transferable` — `DataRepresentation(.png)` + 
  `ProxyRepresentation` exporting the text; `SharePreview(title:image:)`.
  ShareLink replaces the UIKit `ShareSheet` on GameOverOverlay and DailyScreen
  (both formats behind the single existing button — no new prompts anywhere).
- Achievements sharing: Stats gains an **Achievements** section listing the 8 GC
  achievements (`GKAchievement.loadAchievements` + `GKAchievementDescription`;
  graceful when unauthenticated) with earned state; earned rows get a ShareLink
  with a small milestone card ("Forged a 2048 · Gravitile") + text. The milestone
  in-game celebration gains no share button (never interrupt play — research).

### 4.3 Stats (D)

- **Records block** (per current mode segment picker: All · Endless · Zen ·
  Sprint · Daily): six big tabular numerals — Best Score, Best Tile, Deepest
  Cascade (now tracked), Total Cascades, Games, Total Score. Flat rows, no cards.
- **Daily section**: score distribution histogram of the player's own daily
  results (8 buckets, log-ish), today's bar highlighted in accent; percentile
  line "Top N% this week" from the weekly GC board (rank/total via
  `loadEntries`) when authenticated, hidden otherwise. Last-30 strip becomes a
  tappable **monthly calendar** — completed days filled, "clean" days (no undo,
  budget kept) gold-ringed; tapping a past day routes to the archive (Plus) or
  paywall tease (free), today routes to play.
- **Number polish** everywhere: `.monospacedDigit()`, `contentTransition(.numericText())`
  count-up on appear, records animate in once.
- New persisted per-mode counters (custom-decoded defaults, fixture-tested):
  `DailyRecord.movesUsed`, `DailyRecord.usedUndo`, per-mode `gamesPlayed` rolled
  into a `ModeStats` dictionary keyed by a mode discriminator string.

### 4.4 Leaderboard/Challenges (E)

- No new boards. At publish time, attempt ASC challenge-eligibility config for
  `grv.sprint.best` + `grv.daily.weekly` via the Game Center challenges API if
  the endpoints exist for this key (research: iOS 26 Challenges ride existing
  leaderboards with zero client code); otherwise document the ASC-UI step in the
  runbook. No iOS-26-only GameKit code in v1.2 (beta-SDK risk).

## 5. Testing

1. **Engine:** stasis (no rotation, charge earn/cap/spend, zen-unlimited,
   daily/sprint-banned, undo restore); boulder (never merges, slides/falls,
   adjacency chip incl. cascade rounds, multi-chip, mid-resolution free + next
   round merge, per-mode schedules, RNG-draw-count invariant, scoring); v1.1
   fixture + golden game unchanged; new v1.2 round-trip fixture.
2. **App:** persistence round-trips for new fields + v1.1 fixture extension;
   share text format goldens; ModeStats accumulation; stasis arming state machine
   (view-model tests).
3. **UI smoke:** stasis button gating per mode; achievements section renders
   unauthenticated; calendar navigation.
4. **Sim pass:** autoplay soak in endless past move 60 (boulders on screen),
   screenshots, watch build unaffected.

## 6. Risks

- **Difficulty complaints** (research: the #1 obstacle failure mode) — mitigated:
  HP1 first, fixed count in Daily, none in Zen, shatter feeds combos (reward).
- **Engine churn near save-compat** — same fixture discipline as v1.1/v1.2 engine
  work; every Codable change ships with a byte-frozen old-format test.
- **GC percentile needs network** — feature-flagged by authentication, hidden
  when unavailable; no layout jank.
- **Golden test drift** — boulder schedule deliberately starts past the golden
  game's 10 moves; if the golden signature changes, the change is a bug.
