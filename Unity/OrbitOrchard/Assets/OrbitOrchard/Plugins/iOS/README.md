# Apple services for the Unity game

Unity owns Little Lifeline's full-screen game, renderer, input, and interface. This plugin provides StoreKit 2, Game Center, device haptics and system accessibility preferences; it does not embed the previous SwiftUI game or scene.

`AppleServices` automatically creates one persistent Unity GameObject named **AppleServices** before the first scene. The native callback target is its preserved `OnNativeMessage(string)` method. Do not rename the object or create another object with that name. Subscribe to `StateChanged` to refresh shop and rankings views.

```csharp
using OrbitOrchard.Services;

var apple = AppleServices.Instance;
apple.LoadProducts();
// Present Apple's returned price; do not hardcode a price or fake availability.
foreach (var product in apple.Products)
    UnityEngine.Debug.Log(product.title + " " + product.price);

// Only call these in response to the corresponding player action.
apple.Purchase(AppleServices.PassProductId);
apple.RestorePurchases();
apple.AuthenticateGameCenter();
apple.ShowWeeklyLeaderboard();

// Submit only after finishing the fixed four-minute challenge.
// Capture its UTC Monday key when the challenge starts.
apple.SubmitWeeklyScore(score: 9000099L, weekKey: "2026-09-14");
```

## C# contract

`OrbitOrchard.Services.AppleServices` exposes:

- `IsPassOwned`, `IsStoreLoading`, `IsPurchasing`, `IsRestoring`.
- `IsReduceMotionEnabled` mirrors iOS accessibility settings and updates through `StateChanged` when the system preference changes. Combine it with the game's reduced-motion preference.
- `PlayHaptic(kind)`: 0 light, 1 medium, 2 warning. Call only when the player's haptics setting is enabled.
- `Products`: `IReadOnlyList<AppleProduct>` with `id`, `title`, `description`, `price`, and `isConsumable`.
- `ProductState`: idle/loading/available/unavailable/failed.
- `PurchaseState`: idle/purchasing/pending/purchased/cancelled/failed.
- `IsGameCenterAuthenticated`, `IsAuthenticating`, `PlayerDisplayName`, `PendingScoreCount`.
- `GameCenterState`: signedOut/authenticating/authenticated/unavailable.
- `Status` for the latest action, plus `StoreStatus`, `GameCenterStatus`, (`DailyAvailability` is retained empty for compatibility).
- `Initialize`, `LoadProducts`, `Purchase(productId)`, `RestorePurchases`, `AuthenticateGameCenter`, `UseLifelineLeaderboards()`, `ShowWeeklyLeaderboard()`, `SubmitWeeklyScore(long score, string weekKey)`, and `RetryScores`.

Editor and non-iOS builds report Apple services unavailable. They never simulate a successful purchase or fabricate a rank. Local gameplay remains independent of these services. The UI should gate cosmetic selections from `IsPassOwned` and reapply the free theme if an entitlement is revoked.

## Entitlement and score rules

The existing non-consumable `com.flutterly.gravitile.plus` grants the Founder's Carriage Collection, preserving previous Plus purchases. It unlocks Sunrise, Coastal and Heritage finishes. The home hospital and weekly challenge receive no income or scoring advantage. Tips use the existing `.tip.small`, `.tip.medium`, and `.tip.large` IDs and grant no entitlement. Only verified transactions deliver access; pending approval does not. Restoration requires a player action. A transaction observer keeps refunds, revocation, and later approvals in sync.

The new weekly leaderboard identifier is `grv3.lifeline.weekly.v1`. Its App Store Connect configuration is **not yet created**. The prepared first opening is Monday 14 September 2026, 00:00 UTC; the native status explains this date before opening and pre-opening results remain local. It requires a recurring seven-day duration and recurrence starting Monday 00:00 UTC, descending integer scores, retaining each player's best score. The encoded score prioritizes completed visits, then reduces the score by the waiting ticks of those completed visits. Root must verify the provider readback before release. An unavailable or incorrectly timed board reports unavailable; no fallback board or ranking is fabricated.

Authentication only starts from a player action. `little-lifeline.game-center.pending.v1` stores weekly results separately from all earlier games. Old arcade queues remain unread and unchanged; classic, daily and practice submissions are rejected in this build. The queue retries after authentication, foregrounding, and every 30 seconds while active. A result claims the first authenticated account once and never moves to another account. An expired result stays in local profile history but leaves the submission queue. Submission loads and verifies the exact recurring board's Monday UTC start and seven-day duration before using that occurrence.

Campaign saves, offline time and local history are owned by `LittleLifeline.Services.LifelineProfileStore`. They do not depend on Game Center. Rankings use client-simulated results and are not a server-authoritative anti-cheat system.

## iOS export

Export an IL2CPP iOS player with bundle ID **com.flutterly.gravitile** and minimum iOS **18.0**. The root Unity build configuration owns bundle ID, version/build, orientation, signing team, and assets.

`Editor/AppleBuildPostprocessor.cs` copies `Native~` sources into the generated project's `Libraries/OrbitOrchardApple` directory and adds them once to **UnityFramework**. The ignored `Native~` directory prevents automatic asset/plugin import from compiling a second copy. The postprocessor enables Swift 6, generated Objective-C headers, Swift runtime embedding in the app, StoreKit/GameKit frameworks, and the app target's Game Center/In-App Purchase capabilities. It preserves an existing entitlements file when available. The app target receives a `PrivacyInfo.xcprivacy` manifest with `CA92.1` for app-local preferences and score queues; existing entries and Unity framework manifests are preserved.

`OrchardApplePlugin.mm` exports eleven `OO_` C functions. It copies incoming UTF-8 strings before dispatching calls to the main thread. `OrchardAppleBridge.swift` emits complete JSON snapshots through the Objective-C block and `UnitySendMessage`. Callbacks arrive asynchronously on a later Unity frame, so the UI must react to state rather than assume a purchase completed when the C call returned.

The previous native app's release workflow is not an export path for this Unity game. Build and validate the Unity player, export it, then archive that generated Xcode project. Do not upload the preserved native candidate.

## Verification status

- Swift 6 compilation and Objective-C header generation: passed.
- Objective-C++ compilation using the actual generated Swift header: passed.
- Native dynamic-library link with a test-only `UnitySendMessage` stub: passed; all eleven `OO_` exports are present.
- Native weekly occurrence and queue-model checks: 16 passed, including UTC boundaries, duration and account identity roundtrip.
- Little Lifeline Core, persistence services and all 18 profile test cases: source compilation passed against installed Unity 6000.3.24f1 assemblies. Unity's actual serialization/file recovery tests are a separate root-run check.
- Little Lifeline Unity export, IL2CPP linkage to the real Unity runtime, signed device purchases/authentication, provider configuration and Unity TestFlight upload: not run in this service validation.

The link stub is only a temporary validation artifact outside the project. It is never compiled into the app. `validate-native.sh` repeats the native compiler/link checks without signing or touching provider records.

Unity references: [native callbacks](https://docs.unity3d.com/6000.0/Documentation/Manual/ios-native-plugin-call-back.html), [Game Center capability](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/iOS.Xcode.ProjectCapabilityManager.AddGameCenter.html), and [adding native files to a target](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/iOS.Xcode.PBXProject.AddFileToBuild.html).

Privacy references: [Apple approved UserDefaults reasons](https://developer.apple.com/documentation/bundleresources/app-privacy-configuration/nsprivacyaccessedapitypes/nsprivacyaccessedapitype), [Unity application and engine manifests](https://docs.unity3d.com/6000.0/Documentation/Manual/apple-privacy-manifest-policy.html).
