#!/usr/bin/env python3
"""Pushes Gravitile v1.0 App Store metadata + screenshots via the ASC API."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from ascapi import APP_ID, request, upload_asset, md5

REPO = Path(__file__).resolve().parent.parent
SHOTS = sorted((REPO / "docs/appstore/screenshots/6.5-inch").glob("*.jpg"))

DESCRIPTION = """Gravitile is a merge puzzle with a twist you can feel: after every swipe, gravity turns 90 degrees — and the whole board tumbles.

Swipe to slide and merge numbered tiles. Then watch the tumble: tiles fall toward the new gravity, and matching tiles crash together on their own, chaining cascades that multiply your score. The best players don't just plan the swipe — they plan the fall.

HOW IT PLAYS
• Swipe to merge — equal tiles fuse and double, classic and instant
• Gravity rotates after every move — the compass shows what's coming
• Cascades chain automatically as tiles tumble, multiplying points ×2, ×3, ×4…
• The pressure builds the longer you survive. Every game has an ending.

TWO WAYS TO PLAY
• Endless — chase your best score through rising pressure
• The Daily — one seeded puzzle a day, identical for every player on Earth, 40 moves. Share your result as an emoji card and keep your streak alive (one missed day a week is forgiven).

MADE WITH CARE
• One free undo per game — experiment without fear
• Game Center leaderboards and achievements
• Haptics and sound tuned to every cascade
• Works fully offline. No account. No ads. No tracking. Ever.

GRAVITILE PLUS (one-time purchase)
• Replay any past daily from the archive
• Unlimited undo
• Support one independent developer

The rules take ten seconds to learn. The tumble takes a lifetime to master."""

KEYWORDS = "merge,2048,puzzle,daily,gravity,cascade,number,tile,logic,brain,streak,offline"
PROMO = "Merge tiles while gravity rotates beneath you. Chain cascades, chase the daily puzzle, keep your streak alive. No ads. No tracking. Just the tumble."
SUPPORT_URL = "https://github.com/its-me-anoop/gameon/issues"


def main():
    # 1. Find the 1.0 version and its en-US localization.
    status, versions = request("GET", f"/v1/apps/{APP_ID}/appStoreVersions?filter[appStoreState]=PREPARE_FOR_SUBMISSION")
    assert status == 200, versions
    version_id = versions["data"][0]["id"]
    print(f"version: {version_id}")

    status, locs = request("GET", f"/v1/appStoreVersions/{version_id}/appStoreVersionLocalizations")
    assert status == 200, locs
    loc_id = next(l["id"] for l in locs["data"] if l["attributes"]["locale"] == "en-US")
    print(f"localization: {loc_id}")

    # 2. Set copy.
    status, out = request("PATCH", f"/v1/appStoreVersionLocalizations/{loc_id}", {
        "data": {
            "type": "appStoreVersionLocalizations",
            "id": loc_id,
            "attributes": {
                "description": DESCRIPTION,
                "keywords": KEYWORDS,
                "promotionalText": PROMO,
                "supportUrl": SUPPORT_URL,
            },
        }
    })
    assert status == 200, out
    print("copy set")

    # 3. Screenshot set (iPhone 6.5").
    status, sets = request("GET", f"/v1/appStoreVersionLocalizations/{loc_id}/appScreenshotSets")
    assert status == 200, sets
    existing = {s["attributes"]["screenshotDisplayType"]: s["id"] for s in sets["data"]}
    if "APP_IPHONE_65" in existing:
        set_id = existing["APP_IPHONE_65"]
    else:
        status, out = request("POST", "/v1/appScreenshotSets", {
            "data": {
                "type": "appScreenshotSets",
                "attributes": {"screenshotDisplayType": "APP_IPHONE_65"},
                "relationships": {"appStoreVersionLocalization": {
                    "data": {"type": "appStoreVersionLocalizations", "id": loc_id}
                }},
            }
        })
        assert status == 201, out
        set_id = out["data"]["id"]
    print(f"screenshot set: {set_id}")

    # 4. Upload each screenshot: reserve → upload chunks → commit with checksum.
    for shot in SHOTS:
        file_bytes = shot.read_bytes()
        status, out = request("POST", "/v1/appScreenshots", {
            "data": {
                "type": "appScreenshots",
                "attributes": {"fileName": shot.name, "fileSize": len(file_bytes)},
                "relationships": {"appScreenshotSet": {
                    "data": {"type": "appScreenshotSets", "id": set_id}
                }},
            }
        })
        assert status == 201, out
        shot_id = out["data"]["id"]
        upload_asset(out["data"]["attributes"]["uploadOperations"], file_bytes)
        status, out = request("PATCH", f"/v1/appScreenshots/{shot_id}", {
            "data": {
                "type": "appScreenshots",
                "id": shot_id,
                "attributes": {"uploaded": True, "sourceFileChecksum": md5(file_bytes)},
            }
        })
        assert status == 200, out
        print(f"uploaded {shot.name}")

    print("METADATA DONE")


if __name__ == "__main__":
    main()
