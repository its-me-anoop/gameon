# Gravitile v2.0 — "Tilt a World" — design spec

**Status:** approved direction, 2026-07-25
**Supersedes:** the merge-puzzle line (v1.0–v1.3), rejected under App Store
Guideline 4.3(a) — Design — Spam.

---

## 1. Why the game changes

v1.0 was rejected under 4.3(a): "similar binary, metadata, and/or concept as
apps submitted by other developers." v1.3 answered with more content (Math Pop,
themes) inside the same genre. That answer is weak, because the objection isn't
about content volume — it's that a swipe-to-merge board of doubling numbered
tiles reads as one of a thousand 2048 descendants at a glance, no matter how
original the engine underneath is.

The fix is a genre change, not a content patch. v2.0 keeps the name, the
studio's engineering standards, and the reusable services (Game Center, audio
synthesis, haptics, persistence, OKLCH themes) and throws away the board.

**Non-negotiables carried into v2.0:**

- Nothing about the new game may be describable as merge / match-3 / 2048.
- The signature mechanic has to be visible in a ten-second screen recording,
  because that is how long a reviewer looks.
- Every asset stays procedurally generated in-repo by our own tools, so
  "original authorship" remains a fact we can demonstrate, not a claim.

## 2. The game

> **Gravitile — Tilt a World.** A 3D idle world-builder. You tile a small
> spinning planet, and the mass of everything you build steers where its spin
> axis points. The axis decides which of your tiles get sunlight. Build heavy,
> and that region drifts toward the hot equator. Leave a region light, and it
> drifts to the frozen pole.

You are seeding a dead planetoid, one hexagonal tile at a time. Tiles produce
resources in real time and while the app is closed. But every tile you place
also adds **mass**, and mass changes how the world spins.

### 2.1 The signature mechanic: axis drift

A freely rotating rigid body that dissipates energy internally ends up spinning
about its axis of **maximum** moment of inertia. This is why a tossed phone
tumbles into a flat spin, and why concentrated mass on a spinning body migrates
to the equator. Gravitile takes that fact and makes it the whole game.

Player-facing rule, learnable in one sentence:

> **Mass drifts to the equator. Emptiness drifts to the poles.**

Because the equator is hot and sunlit while the poles are dark and frozen, and
because each building type wants a different temperature, the strategic act of
the game is *sculpting your mass distribution to aim the climate bands at the
buildings that need them*. Nothing else in the idle genre does this, and it
reads instantly on screen: you place a heavy Ballast, and over the next minute
the whole frost line visibly crawls across your world.

The tension is the **Wobble** gauge — the angle between the current spin axis
and where the mass distribution wants it. Wobble rises the moment you build
somewhere heavy and relaxes as the axis settles. High wobble is productive
(you're steering) but risky: above a threshold, quakes damage random tiles.

### 2.2 The loop

1. **Place and upgrade tiles.** They produce continuously, online and off.
2. **Watch the axis drift.** Climate bands sweep across the surface; yields
   change; you adapt or you steer.
3. **Catch meteors.** Every few minutes an ore meteor approaches; tap it to
   shatter it into Ore. Ignore it and it craters a tile you must repair. This
   is the reason to open the app rather than only collect from it.
4. **Collapse.** Once the world is massive enough, compress it into a denser
   core: the surface resets, and you keep permanent **Gravity** multipliers, a
   bigger tile grid, and a longer offline cap.

Session shape: open → "while you were away" → tap meteors → spend → re-aim the
axis → close. Two minutes, several times a day, with a long-arc goal.

## 3. Simulation model

All of this lives in `OrbitKit`, a pure-Swift SPM target with no UI imports,
deterministic and unit-tested — the same discipline as the old `GravitileKit`.

### 3.1 Planet geometry

The world is a **Goldberg polyhedron**: subdivide an icosahedron `f` times,
normalize to the sphere, and take the dual. Each vertex of the subdivided
icosphere becomes one tile — 12 pentagons and the rest hexagons.

| Collapse tier | Frequency `f` | Tiles (`10f²+2`) |
|---|---|---|
| 0 | 4 | 162 |
| 1 | 5 | 252 |
| 2 | 6 | 362 |
| 3+ | 7 | 492 |

Each tile carries: unit-sphere center, ordered polygon corners (for the mesh),
and its neighbor indices. Generation is deterministic and cached per frequency.

Invariants worth testing: tile count is exactly `10f²+2`; exactly 12 tiles have
5 corners; neighbor relations are symmetric; polygon areas sum to 4π.

### 3.2 Axis dynamics

Point masses `mᵢ` sit at unit positions `pᵢ`. Build the mass-distribution
covariance:

```
C = Σ mᵢ · pᵢ pᵢᵀ            (symmetric 3×3)
```

The inertia tensor of point masses on a sphere of radius r is
`I = r²(Σmᵢ)·E − r²·C` plus the core's isotropic term, so the axis of maximum
inertia is the eigenvector of C with the **smallest** eigenvalue. Eigen-
decomposition is cyclic Jacobi on the symmetric 3×3 — exact, allocation-free,
and testable against hand-computed cases.

```
a* = eigenvector(C, min eigenvalue), signed to the hemisphere of the current axis
axis ← slerp(axis, a*, 1 − exp(−k·Δt))
wobble = angle(axis, a*)
```

`k` is the settle rate (upgradeable via Gravity; slower early, so early
steering is a deliberate multi-minute act rather than an instant snap).

Sanity checks that double as unit tests: a uniform shell has no preferred axis
(degenerate eigenvalues → axis holds); one heavy pile settles onto the equator
(`a* ⟂ pile`); two antipodal piles behave like one.

### 3.3 Climate

The star sits at a fixed world direction `ŝ`. For a tile at latitude `φ`
(measured from the current spin axis), with the star's declination
`δ = asin(ŝ · axis)`, the rotation-averaged insolation is the standard
daily-mean formula:

```
H₀ = acos(clamp(−tan φ · tan δ, −1, 1))          // half-day length
S(φ) = (H₀ · sin φ · sin δ + cos φ · cos δ · sin H₀) / π
```

`H₀ = 0` is polar night; `H₀ = π` is polar day. Temperature is
`T = Tmin + (Tmax − Tmin)·Ŝ + greenhouse`, where `Ŝ` is `S` normalized to 0…1
and `greenhouse` is an unlockable global offset. Biomes are temperature bands:
Frozen · Tundra · Temperate · Arid · Molten. The **frost line** — the ring
where `T` crosses freezing — is drawn on the planet, and it is the single most
important line in the game because Condensers peak on it.

### 3.4 Buildings

Every building has a mass, a cost curve, a level, and an efficiency curve over
temperature. Mass is the hidden second cost — the thing that makes placement a
real decision instead of "fill every tile".

| Building | Produces | Peak at | Mass |
|---|---|---|---|
| Solar Array | Charge | max insolation (equator) | low |
| Ore Mine | Ore | any (crust density scales with collapse tier) | medium |
| Ice Condenser | Water | the frost line | low |
| Smelter | Alloy (Ore + Charge) | hot | high |
| Greenhouse | Biomass (Water + Charge) | temperate | medium |
| Beacon | +% to its neighbors | any | very low |
| Ballast | nothing | any | **very high** — the steering tool |

Levels: output ×1.6, cost ×1.75, mass ×1.35 per level. Ballast is the pure
expression of the mechanic: it produces nothing and exists only to move your
world's axis.

### 3.5 Offline accrual

Production is a closed-form rate, so offline income is exact: integrate the
rate over the elapsed time, capped (8h base, extended by Gravity and by
Gravitile Plus). The axis also drifts while away — a long absence can leave a
world visibly re-tilted, which is a feature: the "welcome back" panel shows
what moved.

### 3.6 Collapse (prestige)

Unlocked at a mass threshold. Collapsing grants
`ΔG = floor(C · √(mass / threshold))` Gravity, resets the surface, and
permanently improves: all yields (+2% per G), settle rate, offline cap, tile
count (per the table in §3.1), and unlocks higher-tier buildings.

## 4. Presentation

### 4.1 3D

**RealityKit** via `RealityView` (SwiftUI, iOS 18+). SceneKit is deprecated on
the iOS 26 SDK and is not an option for new work; RealityKit is the supported
path and needs no Reality Composer Pro assets for what we do.

- **Planet surface:** one `ModelEntity` whose mesh is generated from the tiling.
  Per-tile color comes from a generated 1D palette texture that tile UVs index
  into — so the whole surface is one mesh and one material, and a tile recolors
  by rewriting a UV, not by adding a draw call.
- **Buildings:** low-poly extruded prisms generated in code, one small entity
  per built tile, scaled by level.
- **Sky:** a starfield generated procedurally (points on a large inverted
  sphere), plus the star as a bright disc with a bloom-ish billboard.
- **Axis:** a translucent shaft through the poles, plus a ghost shaft showing
  `a*` — the target axis — so "where the world wants to spin" is always visible.
  This one piece of UI is what makes the mechanic legible.
- **Frost line:** a ring drawn on the sphere at the freezing latitude.
- **Camera:** orbit-drag with momentum, pinch to zoom. The planet keeps
  spinning under your finger.
- **Picking:** analytic ray–sphere intersection in the engine, then nearest
  tile center — no physics colliders, and testable without a renderer.
- **Collapse:** the implosion is the set-piece — surface tiles fall inward, the
  core flashes, the new smaller-radius planet expands with more tiles.

### 4.2 Audio

Extend `Tools/gensounds.swift`, which already synthesizes the whole sound set
in-repo. New cues: place, upgrade, meteor approach (doppler sweep), meteor
shatter, quake rumble, frost-line crossing chime, collapse (long descending
implosion into a sub-bass thump), and a slow ambient bed of drifting sine pads
tuned to the current biome mix.

### 4.3 UI shell

SwiftUI over the RealityView: resource bar, build tray, tile inspector,
Wobble gauge, welcome-back panel, collapse screen, stats. The five OKLCH
palettes from v1.3 carry over and re-skin the planet materials, not just the
chrome — a theme change is now visible on the world itself.

## 5. Game Center

New identifiers (the old `grv.*` boards describe a game that no longer exists;
they are retired, not reused):

| ID | Name | Metric |
|---|---|---|
| `grv2.mass.best` | Heaviest World | peak world mass |
| `grv2.gravity` | Total Gravity | lifetime prestige currency |
| `grv2.collapse.tier` | Deepest Collapse | collapse count |
| `grv2.speedrun.first` | First Collapse (fastest) | seconds, low-to-high |

Achievements cover first placement, first axis steer (wobble > 30° then
settled), first frost-line condenser, first collapse, tier 3, and a 7-day
return streak.

## 6. Store positioning

- **Name:** `Gravitile — Tilt a World` (24 chars). Deliberately avoids "Idle
  Planet …", which collides with an existing popular title — metadata
  similarity is part of what 4.3(a) tests.
- **Subtitle:** `Build a world. Steer its spin.`
- **Category:** Games › Simulation, secondary Games › Strategy (moving out of
  Puzzle also moves us out of the crowd we were compared against).
- **Review note:** point the reviewer at the axis mechanic explicitly — place a
  Ballast, watch the ghost axis and the frost line move — and at the in-repo
  generators for every asset.

## 7. Scope of the first build

In: the tiling, axis dynamics, climate, seven buildings, meteors, collapse,
offline accrual, the 3D planet, the shell, audio, Game Center, persistence.

Out (later): the Apple Watch companion (the watch target's merge game is
retired with the rest; a status/collect companion comes after the phone game is
proven), multiplayer, seasonal events.

## 8. Alternatives considered

- **Keep the merge game, add more modes.** Rejected: this is what v1.3 already
  tried, and content volume does not answer a genre-similarity objection.
- **3D physics stacker.** Rejected: Tower Bloxx / Stack are exactly the crowd
  we're trying to leave.
- **Orbital ring builder** (place stations that must hold stable orbits).
  Interesting, but the fun lives in orbital-mechanics intuition most players
  don't have, and it reads as abstract on screen. Axis drift gets a comparable
  novelty from physics that is *visible* — you can see the pole move.
- **Pure idle with no active layer.** Rejected: nothing to show in a screenshot
  or a review, and no reason to open the app twice.
