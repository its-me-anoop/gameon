#!/usr/bin/env python3
"""Sets Gravitile categories, age rating (4+), and free pricing via ASC API."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from ascapi import APP_ID, request


def main():
    # --- Categories: Games > Puzzle (+ Board) ---
    status, infos = request("GET", f"/v1/apps/{APP_ID}/appInfos")
    assert status == 200, infos
    editable = [i for i in infos["data"]
                if i["attributes"].get("appStoreState") in ("PREPARE_FOR_SUBMISSION", "DEVELOPER_REJECTED", None)]
    info_id = (editable or infos["data"])[0]["id"]
    print(f"appInfo: {info_id}")

    status, out = request("PATCH", f"/v1/appInfos/{info_id}", {
        "data": {
            "type": "appInfos",
            "id": info_id,
            "relationships": {
                "primaryCategory": {"data": {"type": "appCategories", "id": "GAMES"}},
                "primarySubcategoryOne": {"data": {"type": "appCategories", "id": "GAMES_PUZZLE"}},
                "primarySubcategoryTwo": {"data": {"type": "appCategories", "id": "GAMES_BOARD"}},
            },
        }
    })
    print(f"categories: {status}")
    if status != 200:
        print(json.dumps(out, indent=2)[:1500])

    # --- Age rating: everything none → 4+ ---
    status, decl = request("GET", f"/v1/appInfos/{info_id}/ageRatingDeclaration")
    assert status == 200, decl
    decl_id = decl["data"]["id"]
    attrs = {
        "alcoholTobaccoOrDrugUseOrReferences": "NONE",
        "contests": "NONE",
        "gamblingSimulated": "NONE",
        "horrorOrFearThemes": "NONE",
        "matureOrSuggestiveThemes": "NONE",
        "medicalOrTreatmentInformation": "NONE",
        "profanityOrCrudeHumor": "NONE",
        "sexualContentGraphicAndNudity": "NONE",
        "sexualContentOrNudity": "NONE",
        "violenceCartoonOrFantasy": "NONE",
        "violenceRealistic": "NONE",
        "violenceRealisticProlongedGraphicOrSadistic": "NONE",
        "gambling": False,
        "unrestrictedWebAccess": False,
        "lootBox": False,
    }
    status, out = request("PATCH", f"/v1/ageRatingDeclarations/{decl_id}", {
        "data": {"type": "ageRatingDeclarations", "id": decl_id, "attributes": attrs}
    })
    print(f"age rating: {status}")
    if status != 200:
        print(json.dumps(out, indent=2)[:2000])

    # --- Pricing: free everywhere, base territory USA ---
    status, points = request(
        "GET",
        f"/v1/apps/{APP_ID}/appPricePoints?filter[territory]=USA&limit=1"
    )
    assert status == 200, points
    free_point = points["data"][0]
    assert free_point["attributes"]["customerPrice"] == "0.0", free_point
    point_id = free_point["id"]

    status, out = request("POST", "/v1/appPriceSchedules", {
        "data": {
            "type": "appPriceSchedules",
            "relationships": {
                "app": {"data": {"type": "apps", "id": APP_ID}},
                "baseTerritory": {"data": {"type": "territories", "id": "USA"}},
                "manualPrices": {"data": [{"type": "appPrices", "id": "${price0}"}]},
            },
        },
        "included": [{
            "type": "appPrices",
            "id": "${price0}",
            "attributes": {"startDate": None},
            "relationships": {
                "appPricePoint": {"data": {"type": "appPricePoints", "id": point_id}}
            },
        }],
    })
    print(f"pricing: {status}")
    if status not in (200, 201):
        print(json.dumps(out, indent=2)[:2000])

    print("PRICING DONE")


if __name__ == "__main__":
    main()
