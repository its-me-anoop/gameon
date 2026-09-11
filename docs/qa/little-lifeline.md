# Little Lifeline — verification record

## Current state

The user selected the hospital-on-rails concept on 11 September 2026 after a review of popular idle games, management interfaces and leaderboard design. [Selected brief](../concepts/little-lifeline.md). Unity 6000.3.24f1 with iOS support is installed and licensed. The Unity game now runs in the Editor with the portrait interface; final regression checks and device release work remain. It has not been uploaded to TestFlight.

The generated concept image is art direction, not an image of the running Unity game.

## Required evidence

| Area | Status |
| --- | --- |
| Deterministic care simulation and economy | 31 current Core cases passed in the latest Editor run; device behavior NOT ASSESSED |
| Save recovery and offline accounting | 18 profile cases passed in the latest Editor run; process-kill/resume runtime checks remain |
| Original Blender models and Unity world | Imported and observed in portrait Editor gameplay; renderer source review complete |
| Unity import and test suite | Initial 97/97 passed; subsequent 131-case run had 129 passes and two failures; fixes written, final rerun pending |
| Actual phone-format gameplay and input | Observed by root through CUA in portrait Unity Editor; physical iPhone NOT RUN |
| Crew/layout decisions and first-town progression | NOT ASSESSED in runtime |
| Resume, process-kill recovery and offline reward collection | NOT RUN in runtime |
| Weekly challenge and real Game Center | NOT RUN |
| Real StoreKit purchase and restore | NOT RUN |
| iOS performance and accessibility pass | NOT RUN |
| Fresh device export and signed archive | NOT RUN |
| TestFlight processing and internal availability | NOT RUN |

Keep source checks, Unity tests, observed gameplay and live Apple-service/release proof separate. The previous native and orchard prototypes are historical evidence only.

## Renderer and original assets

[LifelineWorld](../../Unity/OrbitOrchard/Assets/LittleLifeline/Runtime/Presentation/LifelineWorld.cs) owns the camera, render texture and carriage selection. [LifelineActors](../../Unity/OrbitOrchard/Assets/LittleLifeline/Runtime/Presentation/LifelineActors.cs) moves pooled residents and crew from simulation endpoints and care phases. [LifelineTownView](../../Unity/OrbitOrchard/Assets/LittleLifeline/Runtime/Presentation/LifelineTownView.cs) caches each destination and switches the two restoration landmarks using `ProjectCompletionMask`, preserving which project was completed when traveling.

The current world contains a locomotive, four modular carriage slots, a platform and station, distinct consultation/scanning/recovery equipment, and articulated residents and crew. Willowbank has a river, station garden and village school; Copperhill has rock terraces, a workshop and clock tower; Seabrook has a bay, seaside clinic and lighthouse boardwalk. Focus hides foreground trees and town scenery and raises the selected room above the lower action dock.

The original [Blender generator](../../Tools/create_lifeline_assets.py) produced 14 FBX assets totaling **1,514,536 bytes**. These are geometry and materials, without image textures. The editable Blender scene and rendered asset preview are authoring evidence, not gameplay screenshots. All 14 Unity model importers currently have CPU Read/Write disabled. The app icon is a separate opaque 1024×1024 Blender render.

## Observed portrait gameplay

Root reported these observations from actual CUA interaction with the running Unity Editor on 12 September 2026:

- The larger train fills the portrait game composition under the compact HUD and icon navigation.
- Traveling to Copperhill visibly changes the destination scenery.
- Selecting a room brings it into focus above the compact action dock.
- A recovery resident lies correctly on the bed.
- Previewing the Sunrise finish visibly changes the train model.

These observations are Editor runtime evidence. They do not establish physical-device frame rate, memory use, thermal behavior, VoiceOver support, StoreKit entitlement verification or real Game Center connectivity. Source-level support for all three towns and individual project bits is covered separately by tests; only the runtime observations above are claimed here.

## Test chronology and remaining rerun

The initial [Editor result](../../build/lifeline-tests.xml), completed at 22:45 UTC on 11 September, passed **97/97** cases. Of those, 47 were Little Lifeline cases: 19 Core, 18 profile and 10 renderer. The remaining 50 exercised the retained Orbit Orchard components. This run preceded the final town, portrait and care-pose changes.

The subsequent [Editor result](../../build/lifeline-tests-final.xml), completed at 23:04 UTC on 11 September, ran **131 cases: 129 passed, two failed**. One failure was the compact-caption HUD assertion handled by the UI owner. The renderer failure was `StationEntranceFacesTheOverviewCamera`: the narrower portrait camera had moved east of the station while its entrance still faced west. The station now rotates +90 degrees toward the camera; the original positive-facing invariant remains unchanged.

The renderer source review added two actor-reuse cases and one render-texture recovery case. The current [LifelineWorldTests](../../Unity/OrbitOrchard/Assets/LittleLifeline/Tests/Editor/LifelineWorldTests.cs) contains **30 renderer cases**, including portrait projection and picking, focus clearance, destination landmarks, independent restoration bits, travel persistence, treatment poses, pooled actor replacement and lost render-texture recovery. Core, renderer and these 30 test cases compile against the installed Unity 6000.3.24f1 assemblies. **The final Editor rerun after the station and lifecycle fixes is pending.**

## Mobile renderer source review

The steady `Render` path does not create meshes or clone materials, use LINQ, or build temporary scene hierarchies. Models, material roles and the three primitive meshes are shared; room interiors are cached after first use; the town hierarchy is created once. Actor collections and scratch storage are reused. New actor identities allocate a short debug name when assigned, and the pool can grow to the highest simultaneously needed roster; this is event-driven allocation, not a claim of zero garbage collection.

Two lifecycle issues found during review were corrected:

- Removed actors are retired before replacements are acquired. Replacing a full roster now reuses those actors instead of temporarily doubling the pool. Separate patient and crew identity tests cover this behavior.
- A same-size render texture whose GPU surface has been lost is recreated through `IsCreated()`/`Create()` while retaining the same UI image reference. A regression explicitly releases and restores that surface.

The render texture retains its aspect ratio, caps its longest edge at 1536 pixels, requests supported 2× MSAA, and releases the old surface when dimensions change or the world is destroyed. Owned material instances are disposed with the world. Source review found no further serious geometry or allocation issue; actual allocation counters, draw calls, GPU/CPU time, memory pressure, background graphics recovery and sustained iPhone performance remain **NOT MEASURED**.

## Release candidate verification, 12 September 2026

The complete Unity EditMode rerun passed **134/134** with zero failures or skips, completed 11 September 23:06:53 UTC (12 September in London). Evidence: `build/lifeline-tests-release.xml`. Both failures described above are resolved; the station-facing assertion was preserved. The three added renderer lifecycle tests also passed.

The standalone source checker compiled 13 assemblies and passed 66 managed tests; evidence: `build/qa-lifeline/source-check/summary.txt`. These overlap parts of the Editor suite and are not 66 extra product features. The root then corrected the overview bottleneck icon to update live with its queue and next selected department. The source-freeze rerun is recorded separately below.
