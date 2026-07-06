#!/usr/bin/env python3
"""Creates Gravitile's Game Center configuration: 3 leaderboards and 8
achievements (with generated badge images) via the ASC API."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from ascapi import APP_ID, request, upload_asset, md5

BADGES = Path("/tmp/gravitile-badges")

LEADERBOARDS = [
    ("Endless Best", "grv.endless.best", None),
    ("Daily Score", "grv.daily.score", None),
    ("Biggest Tile", "grv.best.tile", None),
    # v1.1
    ("Zen Biggest Tile", "grv.zen.tile", None),
    ("Sprint Best", "grv.sprint.best", None),
    # Weekly recurring: fresh occurrence every Monday 00:00 UTC, so new
    # players get a realistic shot at placing (see v1.1 design spec §F).
    # ASC quirks: start must be in the future, durations need time
    # components (P7D rejected), and weekly is spelled FREQ=DAILY;INTERVAL=7.
    ("Daily Weekly", "grv.daily.weekly", {
        "recurrenceStartDate": "2026-07-13T00:00:00Z",  # a Monday
        "recurrenceDuration": "PT168H",
        "recurrenceRule": "FREQ=DAILY;INTERVAL=7",
    }),
]

ACHIEVEMENTS = [
    ("First Merge", "grv.first.merge", 10, "first-merge",
     "Merge two tiles.", "You made your first merge."),
    ("First Cascade", "grv.first.cascade", 10, "first-cascade",
     "Trigger a cascade during the tumble.", "The tumble worked for you."),
    ("256 Tile", "grv.tile.256", 25, "tile-256",
     "Build a 256 tile.", "You built a 256 tile."),
    ("512 Tile", "grv.tile.512", 50, "tile-512",
     "Build a 512 tile.", "You built a 512 tile."),
    ("1024 Tile", "grv.tile.1024", 75, "tile-1024",
     "Build a 1024 tile.", "You built a 1024 tile."),
    ("2048 Tile", "grv.tile.2048", 100, "tile-2048",
     "Build the legendary 2048 tile.", "Legendary. A 2048 tile."),
    ("Week Streak", "grv.streak.7", 50, "streak-7",
     "Complete 7 daily puzzles in a row.", "Seven dailies straight."),
    ("Month Streak", "grv.streak.30", 100, "streak-30",
     "Complete 30 daily puzzles in a row.", "Thirty dailies straight. Unstoppable."),
]


def main():
    # Game Center detail (enable GC for the app)
    status, out = request("GET", f"/v1/apps/{APP_ID}/gameCenterDetail")
    if status == 200 and out.get("data"):
        detail_id = out["data"]["id"]
        print(f"gameCenterDetail exists: {detail_id}")
    else:
        status, out = request("POST", "/v1/gameCenterDetails", {
            "data": {
                "type": "gameCenterDetails",
                "relationships": {"app": {"data": {"type": "apps", "id": APP_ID}}},
            }
        })
        assert status == 201, out
        detail_id = out["data"]["id"]
        print(f"gameCenterDetail created: {detail_id}")

    # Leaderboards
    status, existing = request("GET", f"/v1/gameCenterDetails/{detail_id}/gameCenterLeaderboards?limit=50")
    have = {l["attributes"]["vendorIdentifier"] for l in existing.get("data", [])} if status == 200 else set()
    for name, vendor, recurrence in LEADERBOARDS:
        if vendor in have:
            print(f"leaderboard {vendor}: exists")
            continue
        attributes = {
            "defaultFormatter": "INTEGER",
            "referenceName": name,
            "vendorIdentifier": vendor,
            "submissionType": "BEST_SCORE",
            "scoreSortType": "DESC",
        }
        if recurrence:
            attributes.update(recurrence)
        status, out = request("POST", "/v1/gameCenterLeaderboards", {
            "data": {
                "type": "gameCenterLeaderboards",
                "attributes": attributes,
                "relationships": {"gameCenterDetail": {
                    "data": {"type": "gameCenterDetails", "id": detail_id}
                }},
            }
        })
        assert status == 201, out
        lb_id = out["data"]["id"]
        status, out = request("POST", "/v1/gameCenterLeaderboardLocalizations", {
            "data": {
                "type": "gameCenterLeaderboardLocalizations",
                "attributes": {"locale": "en-US", "name": name},
                "relationships": {"gameCenterLeaderboard": {
                    "data": {"type": "gameCenterLeaderboards", "id": lb_id}
                }},
            }
        })
        assert status == 201, out
        print(f"leaderboard {vendor}: created + localized")

    # Achievements
    status, existing = request("GET", f"/v1/gameCenterDetails/{detail_id}/gameCenterAchievements?limit=50")
    have = {a["attributes"]["vendorIdentifier"] for a in existing.get("data", [])} if status == 200 else set()
    for name, vendor, points, badge, before, after in ACHIEVEMENTS:
        if vendor in have:
            print(f"achievement {vendor}: exists")
            continue
        status, out = request("POST", "/v1/gameCenterAchievements", {
            "data": {
                "type": "gameCenterAchievements",
                "attributes": {
                    "referenceName": name,
                    "vendorIdentifier": vendor,
                    "points": points,
                    "repeatable": False,
                    "showBeforeEarned": True,
                },
                "relationships": {"gameCenterDetail": {
                    "data": {"type": "gameCenterDetails", "id": detail_id}
                }},
            }
        })
        assert status == 201, out
        ach_id = out["data"]["id"]

        status, out = request("POST", "/v1/gameCenterAchievementLocalizations", {
            "data": {
                "type": "gameCenterAchievementLocalizations",
                "attributes": {
                    "locale": "en-US",
                    "name": name,
                    "beforeEarnedDescription": before,
                    "afterEarnedDescription": after,
                },
                "relationships": {"gameCenterAchievement": {
                    "data": {"type": "gameCenterAchievements", "id": ach_id}
                }},
            }
        })
        assert status == 201, out
        loc_id = out["data"]["id"]

        image = (BADGES / f"{badge}.png").read_bytes()
        status, out = request("POST", "/v1/gameCenterAchievementImages", {
            "data": {
                "type": "gameCenterAchievementImages",
                "attributes": {"fileName": f"{badge}.png", "fileSize": len(image)},
                "relationships": {"gameCenterAchievementLocalization": {
                    "data": {"type": "gameCenterAchievementLocalizations", "id": loc_id}
                }},
            }
        })
        assert status == 201, out
        img_id = out["data"]["id"]
        upload_asset(out["data"]["attributes"]["uploadOperations"], image)
        status, out = request("PATCH", f"/v1/gameCenterAchievementImages/{img_id}", {
            "data": {
                "type": "gameCenterAchievementImages",
                "id": img_id,
                "attributes": {"uploaded": True, "sourceFileChecksum": md5(image)},
            }
        })
        print(f"achievement {vendor}: created + localized + image ({status})")

    print("GAME CENTER DONE")


if __name__ == "__main__":
    main()
