# Clinic macOS camera input

`IdleClinic.Services.ClinicPlatformInput.Initialize()` installs one **local** AppKit scroll monitor. `IsPreciseScroll` reflects `NSEvent.hasPreciseScrollingDeltas` for the latest event before Unity receives it. This keeps a precise scrolling gesture in pan mode even when its delta changes magnitude. `HasNativeScrollSource` reports successful native installation. The monitor returns every event unchanged, observes no other applications and requests no input-monitoring or accessibility permission.

Call `Initialize` while creating the clinic UI, before its first wheel event. Use precise scrolling or Shift + wheel for pan; use discrete scrolling for zoom. Unsupported platforms return false, leaving the Shift modifier fallback to the UI. The iOS player uses touchscreen gestures and does not import this plugin.

The committed 92 KiB bundle contains arm64 and x86_64 slices and is ad-hoc signed. Its importer enables only macOS Editor and macOS standalone. Rebuild after editing `Source~/ClinicPlatformInput.mm`:

```sh
bash Unity/OrbitOrchard/Assets/IdleClinic/Plugins/macOS/build-plugin.sh
bash Unity/OrbitOrchard/Assets/IdleClinic/Plugins/macOS/validate-native.sh
```

Restart Unity before using a replacement of an already loaded native bundle. Unity keeps native plugins loaded for the process lifetime. `ClinicPlatformPluginLifecycle` removes the monitor before managed assembly reload and Editor exit; installation is idempotent, so later clinic setup can reinstall it. Application shutdown also removes the monitor.

Native tests load the actual bundle and dispatch synthetic precise/discrete `NSEvent` objects in their own AppKit process. They verify classification changes, repeated initialization, shutdown and reinitialization. These tests passed on the development Mac. Physical trackpad/mouse behavior inside the Unity Game view remains an independent runtime check.

References: [Apple local event monitors](https://developer.apple.com/documentation/appkit/nsevent/addlocalmonitorforevents(matching:handler:)), [Apple scroll precision](https://developer.apple.com/documentation/appkit/nsevent/hasprecisescrollingdeltas), [Unity macOS native bundles](https://docs.unity3d.com/6000.3/Documentation/Manual/plug-ins-for-desktop.html).
