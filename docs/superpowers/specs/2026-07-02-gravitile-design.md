# Gravitile — Design Document

**Date:** 2026-07-02
**Status:** Approved for implementation (autonomous session — decisions made by agent under `/goal` directive; user review welcome, all decisions reversible)
**Platform:** iOS 17.0+, Swift 6, SwiftUI
**Working bundle ID:** `com.flutterly.gravitile` (configurable before submission)

## 1. Product summary

Gravitile is a one-thumb merge puzzle game with a twist: **gravity rotates 90° clockwise after every move**. You swipe to slide and merge numbered tiles (2048-style doubling), but after each swipe the board's gravity turns, tiles tumble toward the new "down," and equal tiles that collide during the fall **auto-merge in cascades** with escalating score multipliers. The loop is: plan a swipe → watch the tumble → chase the cascade.

Two modes:

- **Endless** — classic high-score chase. Game over when no swipe changes the board.
- **Daily** — everyone worldwide gets the same seeded tile sequence and a 40-move budget. Final score is shareable as an emoji grid (Wordle-style), driving streaks and organic growth.

## 2. Why this concept

The goal demands *unique, fun, addictive, revenue-generating, buildable end-to-end by an agent, and testable*. Three approaches were evaluated:

**A. Physics arcade (gravity-well dodger, SpriteKit).** Maximum juice, but game-feel tuning is subjective and hard to verify with automated tests; physics engines resist deterministic unit testing. High risk of shipping something that *looks* right but *feels* wrong.

**B. Daily-only logic puzzle (laser/mirror rotation).** Highly deterministic and testable, but the genre is crowded, session length is capped at one puzzle/day (weak addictiveness), and monetization surface is thin.

**C. Merge core + rotating gravity (chosen).** Merge games are among the most proven-addictive mechanics on the App Store. The auto-rotating gravity cycle plus fall-cascades is a genuine twist — App Store research (2026-07-02) found gravity-drop merge games and one 3D-rotation merge game, but no title combining swipe-merge, an automatic gravity rotation cycle, cascade multipliers, and a seeded daily challenge. The engine is pure deterministic Swift (fully unit-testable), input is one thumb, and the dual-mode structure (endless for session depth, daily for retention/virality) supports IAP monetization.

The name "Gravitile" showed no App Store collision as of 2026-07-02.

## 3. Game rules (engine specification)

### Board
- 5×5 grid. Cells hold a tile (value = power of 2, displayed as 2, 4, 8, …) or are empty.
- Gravity direction cycles **Down → Left → Up → Right → Down** (90° clockwise), starting at Down. The UI always shows current and next gravity direction.

### Move resolution (one swipe)
1. **Slide+merge** in the swipe direction with 2048 semantics: each line of tiles compacts toward the swipe edge; adjacent equal pairs merge once per move (nearest-to-edge pair merges first); merged tiles double. **A swipe is legal iff this slide+merge phase changes the board** — gravity rotation and falling only happen for legal swipes.
2. **Gravity rotates** 90° clockwise.
3. **Fall:** all tiles fall toward the new gravity direction.
4. **Cascade:** after falling, any tile resting directly on an equal-valued tile (along the gravity axis) merges into it (doubling). Within a round, lines are scanned from the gravity edge outward; the pair nearest the gravity edge merges first, and each tile participates in at most one merge per round. Merges open gaps → tiles fall again → repeat until stable. Each cascade round increases a multiplier: round 1 merges score ×1, round 2 ×2, round 3 ×3, …
5. **Spawn:** new tiles (90% a 2, 10% a 4) enter at the edge *opposite* gravity, each in a seeded-random column (relative to gravity) with at least one empty cell, sliding to rest toward gravity. **Spawn landings are inert** — they never merge; merging only happens in steps 1 and 4. Spawns stop early if the board fills. **Pressure ramp (endless-mode arc):** the number of tiles spawned per move is `min(3, 1 + movesPlayed/60)` — 1 tile for moves 0–59, 2 from move 60, 3 from move 120. Balance simulation (docs/balance-report.md) showed that with a flat 1-tile spawn, cascades relieve pressure so well that random play survives indefinitely; the ramp gives endless games a cruise → tension → collapse arc. Daily mode (40-move budget) never reaches the ramp.

### Scoring
- Swipe-phase merge: merged value × 1.
- Cascade-phase merge: merged value × cascade round multiplier.
- Running total displayed; best tile tracked.

### Game over
- Endless: no legal swipe exists (no direction's slide+merge phase changes the board).
- Daily: 40 moves consumed, or no legal swipe.

### Determinism
- All randomness flows through a seeded RNG (SplitMix64) owned by the game state. Same seed + same move sequence ⇒ identical game. Daily seed = FNV-1a hash of "gravitile-YYYY-MM-DD" (UTC). This makes the daily mode globally fair and every game replayable/testable.

### Undo
- Snapshot-based: engine keeps a bounded history of full states. Free tier: 1 undo per game. Plus: unlimited. Daily mode: undo restores the move budget too (it's a full state restore), keeping share scores honest.

## 4. Architecture

```
gameon/
├── GravitileKit/                 # Local SPM package — pure Swift, no UI deps
│   ├── Sources/GravitileKit/
│   │   ├── Board.swift           # Grid model, tile placement
│   │   ├── Tile.swift            # Tile identity + value
│   │   ├── Direction.swift       # Swipe + gravity directions, rotation
│   │   ├── MoveResolver.swift    # Slide/merge/fall/cascade pipeline
│   │   ├── GameState.swift       # Score, moves, history, game-over, undo
│   │   ├── SeededRNG.swift       # SplitMix64 + daily seed derivation
│   │   └── ShareCard.swift       # Daily result → emoji share string
│   └── Tests/GravitileKitTests/  # Swift Testing — exhaustive engine tests
├── Gravitile/                    # iOS app target (XcodeGen project.yml)
│   ├── App/                      # @main, root navigation, theme
│   ├── Game/                     # Board view, tile views, gesture handling,
│   │   #   animation planner (maps engine events → animation phases),
│   │   #   particle layer (cascade bursts), gravity compass UI
│   ├── Daily/                    # Daily mode screen, streaks, share sheet
│   ├── Stats/                    # History, best scores, charts
│   ├── Store/                    # StoreKit 2 paywall, entitlements
│   ├── Services/                 # Persistence (JSON+UserDefaults), haptics,
│   │   #   sound, Game Center
│   └── Resources/                # Assets, sounds, app icon
├── GravitileUITests/             # Smoke UI tests
├── docs/                         # This spec, plans, App Store metadata,
│   #   privacy policy, publishing runbook
└── .github/workflows/ci.yml     # swift test + xcodebuild test on push
```

**Key boundary:** the engine emits a `MoveResult` value describing everything that happened (slides, merges, falls per cascade round, spawn) as data. The app's animation planner consumes that to choreograph SwiftUI animations phase-by-phase. The engine never knows about views; the views never mutate game state directly.

**State management:** `@Observable` game view-model owning a `GameState`; persistence via Codable snapshots (resume in-progress games across launches).

## 5. Presentation & feel

- **Rendering:** Pure SwiftUI. Tiles are id-stable views positioned by grid coordinates; moves animate in phases (slide → board rotation cue → fall → cascade pulses → spawn pop) driven by the animation planner. Particles for cascade bursts via a lightweight `Canvas`/`TimelineView` emitter. `prefers-reduced-motion` honored (crossfades instead of movement).
- **Gravity cue:** the whole board subtly tilts/nudges toward new gravity; a compass ring around the board shows current + next direction. This is the core readability challenge — the player must never be surprised by where tiles fall.
- **Haptics:** CoreHaptics — light tick per merge, escalating pattern per cascade round.
- **Sound:** short synthesized samples (merge blip rising in pitch with cascade round); mute toggle; respects silent switch.
- **Visual identity (impeccable.style compliant):** OKLCH-tinted deep-space navy neutrals (no pure black), one committed accent ramp for tile values (heat progression), distinctive display face for numerals/logo (e.g., Bricolage Grotesque or similar licensed-free face), restrained body face. No purple-blue gradients, no glassmorphism, no card grids.

## 6. Monetization & retention

- **Free tier:** full Endless mode + today's Daily + 1 undo/game. No ads (no ad-network account available; also preserves premium feel).
- **Gravitile Plus** — non-consumable IAP (£2.99/$2.99): daily archive (play any past daily), unlimited undo, 4 exclusive themes, detailed stats. StoreKit 2, entitlement checked locally, restore purchases supported.
- **Tip jar** — 3 consumable tiers (nice/generous/heroic) on the settings screen.
- **Retention:** daily streaks with streak-freeze grace (1 missed day forgiven per week), Game Center leaderboards (daily score, endless best, best tile) and ~10 achievements, share cards for virality.
- All products defined in a StoreKit configuration file so purchasing is fully testable in simulator/CI before App Store Connect setup.

## 7. Testing strategy

1. **Engine (Swift Testing, `swift test`):** slide/merge semantics per direction incl. triple-tile edge cases; gravity rotation cycle; fall correctness; multi-round cascades and multiplier math; spawn placement/probability with fixed seeds; determinism (seed + moves ⇒ identical state); game-over detection; undo round-trips; daily seed stability (fixed dates ⇒ fixed seeds); share-string formatting.
2. **App logic tests:** persistence round-trips, streak logic (incl. timezone/UTC boundaries, streak freeze), entitlement gating, StoreKit purchase/restore via StoreKitTest.
3. **UI smoke tests (XCUITest):** launch, play a scripted game via accessibility actions, open daily, open paywall.
4. **Manual-equivalent verification:** simulator runs via XcodeBuildMCP with screenshots at each milestone; a debug auto-player (random legal moves) run for soak testing.
5. **CI:** GitHub Actions — engine tests on macOS runner + iOS simulator build/test on every push to main.

## 8. Publishing plan

- Prepare everything automatable: app icon + all sizes, launch screen, App Store metadata (name, subtitle, description, keywords, category: Games/Puzzle), privacy policy (no data collected → simple), privacy nutrition labels (none/Game Center), screenshots via simulator captures, "What's New" text, age rating answers (4+), export compliance (exempt — HTTPS only via system frameworks).
- Archive + `exportOptions.plist` for App Store distribution; upload via `xcrun altool`/App Store Connect API.
- **Hard external dependency:** final signing, App Store Connect app record, IAP product setup, and submission require the user's Apple Developer account credentials/API key. Everything up to that gate ships in-repo with a step-by-step runbook (`docs/publishing-runbook.md`).

## 9. Risks & mitigations

- **Rotation feels chaotic → confusion instead of depth.** Mitigate with strong directional cues, slow first-games pacing, and an interactive 5-step tutorial. Tune with the debug auto-player + manual simulator playtesting. If unfixable, fallback lever: gravity rotates every 2 moves (engine takes rotation period as a parameter).
- **Difficulty curve wrong (games too short/long).** The seeded engine allows Monte-Carlo simulation (thousands of random/greedy games) to measure median game length and tune spawn rates — done as part of implementation, not guesswork.
- **Name collision by submission time.** Re-check at submission; alternates: "Tumble Merge", "Gravity Merge: Tumble".
- **App Review:** no ads, no tracking, offline-first, standard IAP — low-risk profile.
