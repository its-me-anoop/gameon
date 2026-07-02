# Gravitile — App Store Publishing Runbook

Everything in the repo is submission-ready. The steps below are the ones that
**require the Apple Developer account holder** (membership: $99/year). Each
step says exactly what to click or run. Budget ~90 minutes end to end, plus
App Review wait (typically 24–48h).

## 0. Prerequisites

- Apple Developer Program membership active for your Apple ID
- Xcode signed into that Apple ID (Xcode ▸ Settings ▸ Accounts)
- This repo cloned; `brew install xcodegen` if not present; run `./Tools/generate.sh`

## 1. Identifiers & capabilities (developer.apple.com)

1. Certificates, Identifiers & Profiles ▸ Identifiers ▸ **+**
2. App ID, type App, bundle ID **explicit**: `com.flutterly.gravitile`
   (change in `project.yml` first if you prefer another — search-replace all
   `com.flutterly.gravitile` occurrences including product IDs)
3. Capabilities: enable **Game Center** and **In-App Purchase**. Save.

## 2. App record (App Store Connect)

1. Apps ▸ **+** ▸ New App: iOS, name **Gravitile — Tumbling Merge**
   (fallbacks if taken: "Gravitile: Tumbling Merge", "Gravitile Puzzle"),
   primary language English (U.S.), bundle ID from step 1, SKU `gravitile-ios-001`.
2. Fill App Information / pricing / availability from
   [docs/appstore/listing.md](appstore/listing.md) (category, price Free).

## 3. In-App Purchases

Create the four products exactly as tabled in
[docs/appstore/listing.md §In-App Purchases](appstore/listing.md) — IDs must
match `StoreService.swift` character-for-character. Copy display names and
descriptions from `Gravitile/Gravitile.storekit`. Submit each IAP with the
first app version.

## 4. Game Center

App Store Connect ▸ your app ▸ Services ▸ Game Center: create the 3
leaderboards and 8 achievements from
[docs/appstore/listing.md §Game Center](appstore/listing.md). IDs must match
`GameCenterService.swift`.

## 5. Privacy

1. App Privacy ▸ answer **No** to data collection (see listing.md rationale).
2. Privacy policy URL: publish `docs/appstore/privacy-policy.md` — easiest is
   GitHub Pages on this repo, or paste into any static host. Set the URL in
   App Information.

## 6. Signing & upload

```bash
./Tools/generate.sh                      # regenerate project if needed
# Set your team ID in ExportOptions.plist (Xcode ▸ Settings ▸ Accounts shows it)
xcodebuild archive \
  -project Gravitile.xcodeproj -scheme Gravitile \
  -destination "generic/platform=iOS" \
  -archivePath build/Gravitile.xcarchive \
  -allowProvisioningUpdates \
  DEVELOPMENT_TEAM=YOUR_TEAM_ID

xcodebuild -exportArchive \
  -archivePath build/Gravitile.xcarchive \
  -exportOptionsPlist ExportOptions.plist \
  -allowProvisioningUpdates
```

The export uploads straight to App Store Connect (method `app-store-connect`,
destination `upload`). Alternative: open the archive in Xcode Organizer and
click Distribute App.

## 7. TestFlight sanity pass (do not skip)

StoreKit purchases could not be end-to-end verified in this environment
(see GravitileTests/StoreTests.swift). On a TestFlight build, verify:

- [ ] Paywall shows real localized price (Settings ▸ Unlock Plus)
- [ ] Sandbox purchase of Plus unlocks: unlimited undo label, archive rows
- [ ] Restore Purchases works after delete + reinstall
- [ ] A tip purchase thanks you and does NOT unlock Plus
- [ ] Game Center login banner appears; a finished game posts to leaderboards
- [ ] Daily share sheet posts the emoji card
- [ ] Haptics + sound on device (simulator can't verify feel)

## 8. Version metadata & submission

1. Screenshots: upload `docs/appstore/screenshots/6.9-inch/` (1290×2796).
   Consider re-capturing 02-game.png after playing a strong game by hand —
   marketing loves big tiles.
2. Description / keywords / promotional text / What's New: copy from
   [docs/appstore/listing.md](appstore/listing.md).
3. Age rating questionnaire: all None → 4+.
4. Export compliance: already answered by `ITSAppUsesNonExemptEncryption=false`.
5. Review notes: paste from listing.md. Add the IAPs and submit for review.

## 9. Post-launch

- Tag the release: `git tag v1.0.0 && git push --tags`
- Watch crash reports in App Store Connect ▸ TestFlight/Xcode Organizer
- The daily puzzle needs no server ops — seeds derive from the UTC date
- v1.1 candidates: themes for Plus, iPad layout, localization (engine is
  already locale-independent), widgets for daily streak

## Known environment caveats (for future CI/dev machines)

- Xcode 27 beta simulators: SKTestSession doesn't serve local products —
  4 store tests are `.disabled` with this reason; re-enable on stable Xcode.
- `Tools/generate.sh` (not bare `xcodegen`) is required so the StoreKit
  configuration lands in the scheme's TestAction.
