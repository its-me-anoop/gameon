# App Store Listing — Gravitile 2.0

## Identity

- **Name (30 chars max):** `Gravitile — Tilt a World` (24)
- **Subtitle (30 chars max):** `Build a world. Steer its spin.` (30)
- **Bundle ID:** `com.flutterly.gravitile`
- **SKU:** `gravitile-ios-001`
- **Primary category:** Games › Simulation
- **Secondary category:** Games › Strategy
- **Price:** Free, with in-app purchases

Category note: 1.x shipped in Games › Puzzle and was rejected under 4.3(a) for
resembling other apps in that category. 2.0 is a different genre and belongs in
a different aisle. The name deliberately avoids "Idle Planet …", which would
collide with an existing popular title — metadata similarity is part of what
4.3(a) tests.

## Promotional text (170 chars max)

Everything you build has weight, and weight pulls itself toward the equator.
Build heavy on one side and your whole world turns. No ads. No tracking. (149)

## Description (4000 chars max)

Gravitile is a world you build one hexagon at a time — and a world that answers
back.

Every machine you set down has mass. Mass wants to sit on the equator. So the
moment you build heavily on one side, your planet begins to turn: the pole
drifts, the frost line crawls across your ground, and machines that were basking
in sunlight find themselves in the dark.

That is not a scripted event. It is what actually happens to a spinning body
when you load it unevenly, worked out properly and put at the center of a game.

**HOW IT PLAYS**
• Tap a machine, tap the ground, and it is built
• Every machine has a temperature it likes — and the temperature of a tile
  depends on where your axis is pointing
• Mass drifts to the equator; emptiness drifts to the poles. That one sentence
  is the whole strategy
• Production keeps running while the app is closed

**SEVEN MACHINES**
• Solar Array — charge, and it wants all the light it can get
• Ore Mine — works anywhere, loves rich crust
• Ice Condenser — water, and it peaks exactly on the frost line
• Smelter — alloy from ore and charge, but only where it is hot
• Greenhouse — biomass, and only on mild ground
• Beacon — lifts every neighbor, and weighs almost nothing
• Ballast — produces nothing at all. It is pure weight. It is how you steer.

**THE WOBBLE**
The gauge in the corner is the angle between where your world spins and where
its mass wants it to spin. Low is calm. High means you are steering — and past
25 degrees, the ground starts to crack.

**AIM AT THE STAR**
Tilt far enough and one pole falls into permanent daylight — three times the
solar yield of any equator — while the other half of the world freezes solid.
Whether that is brilliant or ruinous depends on what you built there.

**COLLAPSE**
When your world is heavy enough, crush it into its own core. The surface is
lost; the Gravity you earn is permanent, and the next world is bigger: 162
tiles, then 252, then 362, then 492.

**MADE WITH CARE**
• Meteors to catch, quakes to repair, and a calm ambient bed under all of it
• Game Center leaderboards and achievements
• Five color worlds — and a theme repaints the planet, not just the menus
• Works fully offline. No account. No ads. No tracking. Ever.

The rules take a minute. Aiming a planet takes longer.

## Keywords (100 chars max)

idle,planet,build,space,sim,tycoon,incremental,gravity,orbit,3d,relax,offline,colony,strategy

(93 chars — no "merge", "puzzle" or "2048": the 1.x keyword set actively placed
us next to the apps we were compared against.)

## What's New — v2.0

Gravitile is a different game.

The merge board is gone. In its place is a world you build on — a small,
spinning planet where everything you construct has weight, and weight steers
where the axis points. Build heavy on one side and the pole drifts, the frost
line moves across your ground, and every machine's yield changes with it.

• Seven machines, each wanting a different climate — including Ballast, which
  produces nothing and exists only to turn your world
• The Wobble gauge: how far your world is from where its mass wants it to spin
• Meteors to catch, quakes to repair, offline production while you are away
• Collapse your world into its core for permanent Gravity and a bigger planet
• Rendered in 3D, with a sky and a sound set generated in-app
• Five color worlds that repaint the planet itself

## Support & marketing URLs

- Support URL: https://github.com/its-me-anoop/gravitile-support
- Marketing URL (optional): repository page until a site exists
- Privacy Policy URL: https://github.com/its-me-anoop/gravitile-support/blob/main/privacy.md

## Age rating questionnaire answers

All content descriptors: **None** (no violence, no fear themes, no gambling, no
unrestricted web, no user-generated content, no messaging). Expected rating: **4+**.

## App Privacy (nutrition labels)

- **Data collected by the developer: none.**
- All game data is stored on-device. No analytics, no ads, no third-party SDKs,
  no network calls made by app code.
- Game Center and In-App Purchase are Apple services; answer "Do you or your
  third-party partners collect data from this app?" → **No**.

## Export compliance

Uses only Apple OS encryption; qualifies for the exemption.
`ITSAppUsesNonExemptEncryption` is already `false` in the Info.plist.

## In-App Purchases

| Reference name | Product ID | Type | Price |
|---|---|---|---|
| Nice Tip | com.flutterly.gravitile.tip.small | Consumable | $0.99 |
| Generous Tip | com.flutterly.gravitile.tip.medium | Consumable | $2.99 |
| Heroic Tip | com.flutterly.gravitile.tip.large | Consumable | $9.99 |

**`com.flutterly.gravitile.plus` is removed from sale in 2.0.** It gated the
daily archive and unlimited undo in a game that no longer exists, and gating an
idle game's offline cap behind a purchase is exactly the kind of thing that
invites review scrutiny. 2.0 ships with no gated content; tips remain tips.

## Game Center configuration

The `grv.*` boards describe a game that no longer exists. They are retired, not
reused — reusing them would mix merge scores with world masses.

| ID | Name | Sort | Type |
|---|---|---|---|
| grv2.mass.best | Heaviest World | High to low | classic |
| grv2.gravity | Total Gravity | High to low | classic |
| grv2.collapse.tier | Deepest Collapse | High to low | classic |
| grv2.speedrun.first | First Collapse | **Low to high** | classic |

Achievements: `grv2.first.machine`, `grv2.first.meteor`, `grv2.steered`,
`grv2.first.collapse`, `grv2.tier.three`, `grv2.mass.2000`, `grv2.gravity.100`
(points 10–100 to taste; all visible).

## Review notes (for App Review)

Gravitile 2.0 is fully offline and needs no account.

**This is a complete replacement, not an update.** Version 1.x was a merge
puzzle and was rejected under 4.3(a) as resembling other apps. Rather than
argue, we rebuilt the app as a different game in a different genre. The merge
board, its modes and its engine have been deleted from the project; the app now
contains a 3D idle world-builder.

**To see what is original here in about twenty seconds:**
1. Launch and skip the four intro cards.
2. Tap **Ballast** in the tray at the bottom, then tap any tile near the top of
   the planet (near the bright axis shaft).
3. Watch the **Wobble** gauge climb, and watch the pale shaft separate from the
   bright one — that pale shaft is where the world's mass wants it to spin.
4. Over the next minute the planet visibly turns, and the pale frost ring
   crawls across the surface. Tiles change color as their climate changes.

That behaviour is a real rigid-body result — a body that dissipates energy ends
up spinning about its maximum-inertia axis, which is why concentrated mass
migrates to the equator. The eigen-decomposition that computes it is in
`OrbitKit/AxisDynamics.swift`, and the insolation model is the standard
rotation-averaged formula in `OrbitKit/Climate.swift`.

Every asset is generated by tools in this repository, not licensed or bought:
the tiling and all meshes are computed at runtime (`PlanetMesh.swift`), the
sound set is synthesized (`Tools/gensounds.swift`), the app icon is drawn by
`Tools/genicon.py`, and the color palettes are hand-derived in OKLCH. We are
happy to provide the full git history, the balance-simulation reports and our
design documents as evidence of original authorship.
