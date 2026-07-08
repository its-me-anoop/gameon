#!/usr/bin/env python3
"""Pushes Gravitile v1.3 metadata, review notes, and screenshots via the ASC
API, recovering from the 4.3(a) rejection of 1.0 (8).

Steps (all idempotent — safe to re-run after a partial failure):
  1. Remove REJECTED/REMOVED items from the open review submission so the
     version becomes editable again (aborts if other item states remain).
  2. Rename the rejected appStoreVersion 1.0 -> 1.3.
  3. Push en-US copy (description / promo / keywords) from the v1.3 listing.
  4. Replace App Review notes with the v1.3 walkthrough + 4.3(a) response.
  5. Rebuild screenshot sets from flattened JPEGs (ASC rejects alpha PNGs —
     the v1.0 lesson): fresh APP_IPHONE_67 (8), rebuilt APP_IPAD_PRO_3GEN_129
     (5), new APP_WATCH_SERIES_10 (1, required now that build 11 bundles the
     watch app). Every set is polled to COMPLETE before moving on, and the
     stale APP_IPHONE_65 set is deleted only after its replacement verifies.

Build attach + submit stays in submit_review.py (run with build number 11).
"""
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from ascapi import request, upload_asset, md5

REPO = Path(__file__).resolve().parent.parent
SHOTS = REPO / "docs/appstore/screenshots"

VERSION_ID = "9a76a036-4ca2-4400-8028-cff63d063795"
LOC_ID = "91be8477-5b3e-4d39-98cf-1fd7147ee73e"
REVIEW_DETAIL_ID = "097e070a-7035-499e-a0b0-bb75f90e9f24"
SUBMISSION_ID = "fd880674-4641-4e42-9d36-843002440e91"

DESCRIPTION = """Gravitile is a merge puzzle with a twist you can feel: after every swipe, gravity turns 90 degrees — and the whole board tumbles.

Swipe to slide and merge numbered tiles. Then watch the tumble: tiles fall toward the new gravity, and matching tiles crash together on their own, chaining cascades that multiply your score. The best players don't just plan the swipe — they plan the fall.

HOW IT PLAYS
• Swipe to merge — equal tiles fuse and double, classic and instant
• Gravity rotates after every move — the compass shows what's coming
• Cascades chain automatically as tiles tumble, multiplying points ×2, ×3, ×4…
• The pressure builds the longer you survive. Every game has an ending.

FIVE WAYS TO PLAY
• Endless — chase your best score through rising pressure
• The Daily — one seeded puzzle a day, identical for every player on Earth, 40 moves. Share your result and keep your streak alive (one missed day a week is forgiven).
• Math Pop — the learning mode. Tiles carry small numbers, and two tiles that ADD UP to the target pop together: make 5, make 10 … make 16. Every pop shows its equation ("3 + 7 = 10"), and tiles wear the Cuisenaire rod colors used in real classrooms. Number bonds are the backbone of early arithmetic — here they're also how you score. Gentle enough for young learners, sneakily fun for grown-ups.
• Zen — no clock, no pressure. Just the tumble.
• Sprint — 60 moves. Post your biggest score.

FIVE COLOR WORLDS
Ember's navy night, Tidepool's ocean deeps, Meadow's cream daylight, Aurora's polar dusk, Sorbet's candy brights — every palette hand-tuned for contrast, including two full light themes. Pick yours in Settings.

POWERUPS & HURDLES
• Stasis — earn a Hold at 256, 512 or 1024, then freeze gravity for one move
• Boulders — iced tiles that never merge; crack them free with adjacent merges

ON APPLE WATCH
The tumbling-merge game, playable on your wrist — swipe, tumble, merge. Your watch game is its own little world, ready anywhere.

MADE WITH CARE
• One free undo per game — experiment without fear
• Game Center leaderboards and achievements — including a weekly Daily board
• A calm ambient soundtrack, with haptics and sound tuned to every cascade
• Beautiful rendered share cards for your best runs
• Works fully offline. No account. No ads. No tracking. Ever.

GRAVITILE PLUS (one-time purchase)
• Replay any past daily from the archive
• Unlimited undo
• Support one independent developer

The rules take ten seconds to learn. The tumble takes a lifetime to master."""

PROMO = "Gravity rotates under every merge. Now with Math Pop — a playful number-bonds learning mode — and five hand-tuned color themes. No ads. No tracking."

KEYWORDS = "merge,puzzle,daily,gravity,cascade,zen,math,education,learn,numbers,tile,logic,brain,offline"

REVIEW_NOTES = """Gravitile is a merge puzzle where gravity rotates 90 degrees after every move. Fully offline, no account, no ads, no analytics, no third-party SDKs.

ABOUT THE PRIOR 4.3(a) REJECTION (version 1.0, build 8)
Every line of code and every asset is original, first-party work written for this app: the deterministic tumbling-gravity engine (a custom Swift package — gravity rotates 90 degrees after each move and cascades resolve on seeded random streams), the synthesized audio, the OKLCH-derived color palettes, and the fully custom SwiftUI interface. Nothing is templated, purchased, or repackaged; complete git history and design documents are available on request. This build (1.3) also adds substantial content we believe no similar app offers — described below. If a specific similarity to another app concerns the review team, we would genuinely appreciate a pointer so we can address it directly.

WHAT'S NEW IN 1.3
1. Math Pop — an arithmetic-learning mode (Home -> "Math Pop", NEW badge). Tiles carry small numbers; two tiles merge only when they ADD UP to the stage target ("Make 5" -> "Make 10" -> ... -> "Make 16"). Each completed bond pops off the board showing its equation ("3 + 7 = 10"), and tiles use the Cuisenaire rod colors taught in real classrooms. Free for everyone.
2. Five color themes (Settings -> Theme): Ember, Tidepool, Meadow, Aurora, Sorbet — including two full light themes.
3. Also added since the reviewed build: Zen and Sprint modes, a standalone Apple Watch game, the Stasis hold powerup, iced "boulder" tiles, rendered share cards, and detailed statistics.

FLOWS TO TEST
1. Home -> Play Endless: swipe to merge tiles; after each swipe gravity rotates (the compass above the board shows current -> next) and tiles tumble, auto-merging in cascades.
2. Home -> Math Pop: swipe to slide; tiles that sum to the target pop with their equation; after six bonds the target advances and the board resets fresh.
3. Home -> Daily: one seeded 40-move puzzle per UTC day, identical for all players.
4. Settings -> Theme: switch between the five palettes live.
5. Undo: one free undo per game (button bottom-left).

IN-APP PURCHASES
- Gravitile Plus (non-consumable, $2.99): unlocks past daily puzzles (Daily -> Archive rows) and unlimited undo. Reachable via Settings -> Unlock Plus, or any locked archive row.
- Three optional tips (consumables) under Settings -> Tip jar. They unlock no content; a thank-you alert is the only effect.

GAME CENTER
Optional. Leaderboards and achievements cover the classic modes; the game is fully playable without signing in. Math Pop intentionally submits nothing.

APPLE WATCH
Standalone watchOS game (runs independently, no phone or account required) — swipe on the board to play.

PRIVACY
No data collected. Everything is stored on-device. The daily puzzle needs no server — it is seeded from the UTC date.

Happy to answer anything at the contact email."""

IPHONE_SHOTS = [SHOTS / "6.9-inch" / n for n in [
    "01-home.jpg", "02-mathpop.jpg", "03-game.jpg", "04-themes.jpg",
    "05-sprint.jpg", "06-daily.jpg", "07-stats.jpg", "08-gameover.jpg",
]]
IPAD_SHOTS = [SHOTS / "ipad-13" / n for n in [
    "01-home.jpg", "02-mathpop.jpg", "03-game.jpg", "04-daily.jpg", "05-stats.jpg",
]]
WATCH_SHOTS = [SHOTS / "watch" / "01-game.jpg"]


def step(label):
    print(f"\n=== {label}")


def clear_rejected_items():
    status, items = request("GET", f"/v1/reviewSubmissions/{SUBMISSION_ID}/items")
    assert status == 200, items
    blockers = []
    for item in items.get("data", []):
        state = item["attributes"].get("state")
        if state in ("REJECTED", "REMOVED"):
            st, _ = request("DELETE", f"/v1/reviewSubmissionItems/{item['id']}")
            print(f"removed {state} item -> {st}")
        else:
            blockers.append(state)
    if blockers:
        sys.exit(f"submission has items in states {blockers}; the version is "
                 "locked — resolve those in ASC before re-running")


def rename_version():
    status, out = request("PATCH", f"/v1/appStoreVersions/{VERSION_ID}", {
        "data": {"type": "appStoreVersions", "id": VERSION_ID,
                 "attributes": {"versionString": "1.3"}}
    })
    assert status == 200, out
    print("versionString ->", out["data"]["attributes"]["versionString"])


def push_copy():
    for name, value, limit in [("description", DESCRIPTION, 4000),
                               ("promotionalText", PROMO, 170),
                               ("keywords", KEYWORDS, 100)]:
        assert len(value) <= limit, f"{name} too long: {len(value)} > {limit}"
    status, out = request("PATCH", f"/v1/appStoreVersionLocalizations/{LOC_ID}", {
        "data": {"type": "appStoreVersionLocalizations", "id": LOC_ID,
                 "attributes": {"description": DESCRIPTION,
                                "promotionalText": PROMO,
                                "keywords": KEYWORDS}}
    })
    assert status == 200, out
    print("copy set (description %d chars, promo %d, keywords %d)"
          % (len(DESCRIPTION), len(PROMO), len(KEYWORDS)))


def push_review_notes():
    assert len(REVIEW_NOTES) <= 4000, f"notes too long: {len(REVIEW_NOTES)}"
    status, out = request("PATCH", f"/v1/appStoreReviewDetails/{REVIEW_DETAIL_ID}", {
        "data": {"type": "appStoreReviewDetails", "id": REVIEW_DETAIL_ID,
                 "attributes": {"notes": REVIEW_NOTES}}
    })
    assert status == 200, out
    print("review notes set (%d chars)" % len(REVIEW_NOTES))


def get_sets():
    status, sets = request("GET", f"/v1/appStoreVersionLocalizations/{LOC_ID}/appScreenshotSets")
    assert status == 200, sets
    return {s["attributes"]["screenshotDisplayType"]: s["id"] for s in sets["data"]}


def ensure_empty_set(display_type):
    sets = get_sets()
    if display_type in sets:
        set_id = sets[display_type]
        st, shots = request("GET", f"/v1/appScreenshotSets/{set_id}/appScreenshots?limit=15")
        assert st == 200, shots
        for sh in shots.get("data", []):
            st2, out = request("DELETE", f"/v1/appScreenshots/{sh['id']}")
            assert st2 in (200, 204), out
        print(f"emptied existing {display_type} set ({len(shots.get('data', []))} old shots)")
        return set_id
    status, out = request("POST", "/v1/appScreenshotSets", {
        "data": {"type": "appScreenshotSets",
                 "attributes": {"screenshotDisplayType": display_type},
                 "relationships": {"appStoreVersionLocalization": {
                     "data": {"type": "appStoreVersionLocalizations", "id": LOC_ID}}}}
    })
    assert status == 201, out
    print(f"created {display_type} set")
    return out["data"]["id"]


def upload_shots(set_id, files):
    for shot in files:
        file_bytes = shot.read_bytes()
        status, out = request("POST", "/v1/appScreenshots", {
            "data": {"type": "appScreenshots",
                     "attributes": {"fileName": shot.name, "fileSize": len(file_bytes)},
                     "relationships": {"appScreenshotSet": {
                         "data": {"type": "appScreenshotSets", "id": set_id}}}}
        })
        assert status == 201, out
        shot_id = out["data"]["id"]
        upload_asset(out["data"]["attributes"]["uploadOperations"], file_bytes)
        status, out = request("PATCH", f"/v1/appScreenshots/{shot_id}", {
            "data": {"type": "appScreenshots", "id": shot_id,
                     "attributes": {"uploaded": True, "sourceFileChecksum": md5(file_bytes)}}
        })
        assert status == 200, out
        print(f"  uploaded {shot.name}")
    wait_for_processing(set_id, len(files))


def wait_for_processing(set_id, expected, timeout=300):
    """Screenshot validation is asynchronous; a shot only counts once its
    assetDeliveryState reaches COMPLETE. FAILED (bad dims, alpha, checksum)
    aborts loudly instead of letting a broken set reach submission."""
    deadline = time.time() + timeout
    while True:
        st, shots = request(
            "GET",
            f"/v1/appScreenshotSets/{set_id}/appScreenshots"
            "?limit=15&fields[appScreenshots]=fileName,assetDeliveryState"
        )
        assert st == 200, shots
        states = {}
        for sh in shots.get("data", []):
            state = (sh["attributes"].get("assetDeliveryState") or {}).get("state")
            states[sh["attributes"].get("fileName")] = state
        failed = [f for f, s in states.items() if s == "FAILED"]
        assert not failed, f"asset processing FAILED for {failed}: {shots}"
        complete = [f for f, s in states.items() if s == "COMPLETE"]
        if len(complete) == expected and len(states) == expected:
            print(f"  all {expected} shots COMPLETE")
            return
        if time.time() > deadline:
            sys.exit(f"timed out waiting for processing; states: {states}")
        time.sleep(5)


def main():
    for f in IPHONE_SHOTS + IPAD_SHOTS + WATCH_SHOTS:
        assert f.exists(), f"missing screenshot {f}"

    step("1. clear rejected submission items")
    clear_rejected_items()

    step("2. rename version 1.0 -> 1.3")
    rename_version()

    step("3. push v1.3 copy")
    push_copy()

    step("4. push review notes")
    push_review_notes()

    step("5a. iPhone 6.9-inch set (APP_IPHONE_67)")
    upload_shots(ensure_empty_set("APP_IPHONE_67"), IPHONE_SHOTS)

    step("5b. delete stale APP_IPHONE_65 set (replacement verified above)")
    sets = get_sets()
    if "APP_IPHONE_65" in sets:
        st, out = request("DELETE", f"/v1/appScreenshotSets/{sets['APP_IPHONE_65']}")
        assert st in (200, 204), out
        print(f"deleted APP_IPHONE_65 -> {st}")
    else:
        print("already gone")

    step("5c. iPad 13-inch set (APP_IPAD_PRO_3GEN_129)")
    upload_shots(ensure_empty_set("APP_IPAD_PRO_3GEN_129"), IPAD_SHOTS)

    step("5d. watch set (APP_WATCH_SERIES_10)")
    upload_shots(ensure_empty_set("APP_WATCH_SERIES_10"), WATCH_SHOTS)

    print("\nPUBLISH v1.3 DONE — next: Tools/submit_review.py 11")


if __name__ == "__main__":
    main()
