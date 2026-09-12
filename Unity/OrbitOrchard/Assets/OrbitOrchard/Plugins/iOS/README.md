# Apple services for the Unity clinic

Unity owns Little Lifeline's full-screen clinic, renderer, input and interface. This plugin provides StoreKit 2 ownership restoration, haptics, system reduced motion and UIKit window sizing. Existing leaderboard and purchase entry points remain compatible with preserved earlier content; the new clinic beta does not expose a shop or rankings.

`AppleServices` automatically creates one persistent GameObject named **AppleServices** before the first scene. The callback target is its preserved `OnNativeMessage(string)` method. Do not rename this object or create another object with that name. Subscribe to `StateChanged` to display the real result of an explicit restoration.

## Clinic API

`OrbitOrchard.Services.AppleServices` exposes:

- `ScreenWidthPoints`: UIKit key-window width in points, with scene/screen fallback. Query when the viewport changes and set a ConstantPixelSize UI Toolkit panel's scale to `Screen.width / ScreenWidthPoints`. Editor/non-iOS returns `Screen.width`; no DPI estimate is used.
- `IsReduceMotionEnabled`: mirrors iOS accessibility and emits `StateChanged` when changed. Combine with the clinic's saved reduced-motion preference.
- `PlayHaptic(kind)`: 0 light, 1 medium, 2 warning. Call only when the player's preference enables haptics.
- `RestorePurchases()`, `IsPassOwned`, `IsRestoring`, `StoreStatus`, `PurchaseState`: restore only from a player action, then show verified results. No local profile flag can grant ownership.
- `Products`, `IsStoreLoading`, `ProductState`, `Purchase(productId)`: preserved compatibility API. Apple supplies localized price/title/description; the clinic has no new purchase flow.

Editor and non-iOS builds report Apple services unavailable; they do not simulate a purchase or fabricate a rank. The persistent transaction observer and foreground refresh retain verified ownership and track revocations. Existing product IDs `com.flutterly.gravitile.plus` and `.tip.small`, `.tip.medium`, `.tip.large` remain unchanged. No clinic money or tutorial step is imported from the previous game.

## Previous ranking data remains isolated

`OrchardAppleBridge` constructs Game Center lazily, only after an explicit `UseLifelineLeaderboards`, authentication or leaderboard action. Clinic startup and foreground callbacks refresh StoreKit without constructing Game Center, reading/pruning its queue, or starting the 30-second score retry task. Previous saved queue bytes remain untouched.

The preserved weekly feature uses `grv3.lifeline.weekly.v1`, `little-lifeline.game-center.pending.v1`, a fixed four-minute scenario and a UTC Monday key. Its prior release configuration starts 14 September 2026, 00:00 UTC with seven-day occurrences. The clinic never submits cash, treatment counts, or campaign scores there. Legacy arcade modes are rejected. Keep this service inactive unless a compatible ranked feature is deliberately exposed again.

Clinic persistence is owned by `IdleClinic.Services.ClinicProfileStore`, independent of Apple accounts. It uses its own save identity and migrates only preferences from the older campaign.

## Export and verification

The player is IL2CPP, bundle **com.flutterly.gravitile**, minimum iOS **18.0**. Root build configuration owns version/build, display name, scene and icon.

`AppleBuildPostprocessor.cs` copies the five `Native~` source files into `Libraries/OrbitOrchardApple` and compiles them once in **UnityFramework**. It configures Swift 6, generated Objective-C headers, runtime embedding, StoreKit/GameKit frameworks and the app's existing capabilities. The application privacy manifest merges UserDefaults reason `CA92.1` and app-container file metadata reason `C617.1`, preserving Unity declarations.

`OrchardAppController.mm` adopts restored native 1.x application scenes whose SwiftUI delegate predates the Unity player. It updates the public `UIScene.delegate` property to `UnityScene`, leaving the session, Documents, preferences and verified ownership intact. If the old delegate class is unavailable, adoption requires the old SwiftUI restoration activity marker. Normal Unity scenes and unrelated delegates are unchanged. Connection notification installs the delegate before normal foreground handling; a guarded foreground fallback initializes Unity if necessary. The native log records the preserved session identifier when adoption occurs.

Run `python3 -B -m unittest discover -s Tools/tests -p test_native_scene_migration.py -v` from the repository root for the adapter's executable logic checks. These use public-API doubles on macOS, so a preserved native-to-Unity install still needs first-launch, background/resume and relaunch validation on iOS. [Apple documents delegate replacement](https://developer.apple.com/documentation/uikit/uiscene/delegate) and [connection callbacks for restored scenes](https://developer.apple.com/documentation/uikit/uiscenedelegate/scene(_:willconnectto:options:)).

`OrchardApplePlugin.mm` exports **thirteen** `OO_` functions. UIKit width is synchronous and main-thread safe; other service calls dispatch asynchronously. String arguments are copied before dispatch, and JSON snapshots arrive through `UnitySendMessage` on a later Unity frame. UI must wait for real service state instead of treating a returning call as purchase success.

```sh
bash Unity/OrbitOrchard/Assets/OrbitOrchard/Plugins/iOS/validate-native.sh
```

This check passed Swift 6 compilation, generated-header ObjC++ compilation, test-stub dynamic linkage and all thirteen expected symbols. It does not exercise a real Unity player, UIKit dimensions on a device, a StoreKit account, or Game Center. The temporary callback stub is never copied into an app. Actual clinic Unity import/tests, iOS runtime, signed archive and TestFlight verification are tracked in `docs/qa/idle-clinic.md` and the release handoff.

References: [Unity native callbacks](https://docs.unity3d.com/6000.0/Documentation/Manual/ios-native-plugin-call-back.html), [Game Center capability](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/iOS.Xcode.ProjectCapabilityManager.AddGameCenter.html), [native files in an Xcode target](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/iOS.Xcode.PBXProject.AddFileToBuild.html), [Apple required reasons](https://developer.apple.com/documentation/bundleresources/app-privacy-configuration/nsprivacyaccessedapitypes/nsprivacyaccessedapitype).
