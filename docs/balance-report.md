# Gravitile Balance Report

**Date:** 2026-07-02 · **Tool:** `swift run -c release BalanceSim <games> <moveCap>` · 300 games/config, seeds 1…300

## Problem found

With the original flat 1-tile-per-move spawn, endless mode effectively never ended:
random play hit a 1000-move cap in 86% of games (greedy: 100%). The rotating-gravity
cascades relieve board pressure so efficiently that the board rarely fills. No losing
arc → no tension.

## Fix: spawn pressure ramp

`GameState.spawnCount(forMovesPlayed:) = min(3, 1 + moves/60)` — 1 tile/move for moves
0–59, 2 from move 60, 3 from move 120. Daily mode (40-move budget) never reaches the ramp.

## Results after fix (cap 3000, zero games hit it)

| Config | Moves p10/med/p90 | Score p10/med/p90 | Best tile med/p90 | Cascades/move |
|---|---|---|---|---|
| Endless / random | 154 / 200 / 260 | 3,376 / 6,024 / 9,228 | 256 / 512 | 0.60 |
| Endless / greedy (1-ply) | 268 / 410 / 672 | 10,864 / 21,548 / 43,220 | 1024 / 2048 | 1.08 |
| Daily(40) / random | 40 | 252 / 308 / 412 | 32 / 64 | 0.32 |
| Daily(40) / greedy | 40 | 292 / 388 / 444 | 64 / 64 | 0.47 |

## Interpretation

- **Every game ends.** Endless has a cruise (1 spawn) → tension (2) → collapse (3) arc.
- **Skill pays.** Even a 1-ply greedy policy doubles survival and 3.6×es score vs
  random; human planning (multi-move cascade setups) should widen this further.
- **Session length** ≈ 5–10 minutes for a casual endless run — right for mobile.
- **Daily spread** (p10–p90 ≈ 252–444) gives the share card meaningful variance.

## Frozen constants

- Grid 5×5 · spawn 90% 2 / 10% 4 · cascade multiplier ×round, uncapped
- Ramp thresholds: moves 60 / 120, cap 3 tiles
- Daily budget: 40 moves

The 10-move golden replay test (`goldenTenMoveGame`) is unaffected by the ramp
(it plays below move 60) and still guards base semantics.
