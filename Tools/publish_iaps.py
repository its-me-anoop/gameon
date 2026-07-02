#!/usr/bin/env python3
"""Creates Gravitile's 4 in-app purchases with localization, pricing,
availability, and review screenshot via the ASC API."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from ascapi import APP_ID, request, upload_asset, md5

SCREENSHOT = Path("/tmp/gravitile-shots/paywall.png")

PRODUCTS = [
    {
        "productId": "com.flutterly.gravitile.plus",
        "referenceName": "Gravitile Plus",
        "type": "NON_CONSUMABLE",
        "familySharable": True,
        "displayName": "Gravitile Plus",
        "description": "Daily archive, unlimited undo, and more.",
        "price": "2.99",
    },
    {
        "productId": "com.flutterly.gravitile.tip.small",
        "referenceName": "Nice Tip",
        "type": "CONSUMABLE",
        "familySharable": False,
        "displayName": "Nice Tip",
        "description": "A small thank-you to the developer.",
        "price": "0.99",
    },
    {
        "productId": "com.flutterly.gravitile.tip.medium",
        "referenceName": "Generous Tip",
        "type": "CONSUMABLE",
        "familySharable": False,
        "displayName": "Generous Tip",
        "description": "A generous thank-you to the developer.",
        "price": "2.99",
    },
    {
        "productId": "com.flutterly.gravitile.tip.large",
        "referenceName": "Heroic Tip",
        "type": "CONSUMABLE",
        "familySharable": False,
        "displayName": "Heroic Tip",
        "description": "A heroic thank-you to the developer.",
        "price": "9.99",
    },
]

REVIEW_NOTE = (
    "Purchases are reachable in-app via Settings -> Unlock Plus (paywall) and "
    "Settings -> Tip jar. Gravitile Plus unlocks the daily puzzle archive and "
    "unlimited undo; tips are voluntary support with no content."
)


def existing_iaps():
    status, out = request("GET", f"/v1/apps/{APP_ID}/inAppPurchasesV2?limit=50")
    assert status == 200, out
    return {d["attributes"]["productId"]: d["id"] for d in out["data"]}


def find_price_point(iap_id: str, price: str) -> str:
    url = f"/v2/inAppPurchases/{iap_id}/pricePoints?filter[territory]=USA&limit=200"
    while url:
        status, out = request("GET", url)
        assert status == 200, out
        for p in out["data"]:
            if p["attributes"]["customerPrice"] == price:
                return p["id"]
        nxt = out.get("links", {}).get("next")
        url = nxt.replace("https://api.appstoreconnect.apple.com", "") if nxt else None
    raise RuntimeError(f"no USA price point for {price}")


def main():
    have = existing_iaps()
    shot_bytes = SCREENSHOT.read_bytes() if SCREENSHOT.exists() else None

    for product in PRODUCTS:
        pid = product["productId"]
        if pid in have:
            iap_id = have[pid]
            print(f"{pid}: exists ({iap_id})")
        else:
            status, out = request("POST", "/v2/inAppPurchases", {
                "data": {
                    "type": "inAppPurchases",
                    "attributes": {
                        "name": product["referenceName"],
                        "productId": pid,
                        "inAppPurchaseType": product["type"],
                        "familySharable": product["familySharable"],
                        "reviewNote": REVIEW_NOTE,
                    },
                    "relationships": {"app": {"data": {"type": "apps", "id": APP_ID}}},
                }
            })
            assert status == 201, out
            iap_id = out["data"]["id"]
            print(f"{pid}: created ({iap_id})")

        # Localization (en-US display name + description)
        status, locs = request("GET", f"/v2/inAppPurchases/{iap_id}/inAppPurchaseLocalizations")
        if status == 200 and not any(l["attributes"]["locale"] == "en-US" for l in locs.get("data", [])):
            status, out = request("POST", "/v1/inAppPurchaseLocalizations", {
                "data": {
                    "type": "inAppPurchaseLocalizations",
                    "attributes": {
                        "locale": "en-US",
                        "name": product["displayName"],
                        "description": product["description"],
                    },
                    "relationships": {"inAppPurchaseV2": {
                        "data": {"type": "inAppPurchases", "id": iap_id}
                    }},
                }
            })
            assert status == 201, out
            print(f"  localization set")

        # Price schedule
        point = find_price_point(iap_id, product["price"])
        status, out = request("POST", "/v1/inAppPurchasePriceSchedules", {
            "data": {
                "type": "inAppPurchasePriceSchedules",
                "relationships": {
                    "inAppPurchase": {"data": {"type": "inAppPurchases", "id": iap_id}},
                    "baseTerritory": {"data": {"type": "territories", "id": "USA"}},
                    "manualPrices": {"data": [{"type": "inAppPurchasePrices", "id": "${p0}"}]},
                },
            },
            "included": [{
                "type": "inAppPurchasePrices",
                "id": "${p0}",
                "attributes": {"startDate": None},
                "relationships": {
                    "inAppPurchasePricePoint": {"data": {"type": "inAppPurchasePricePoints", "id": point}},
                    "inAppPurchaseV2": {"data": {"type": "inAppPurchases", "id": iap_id}},
                },
            }],
        })
        print(f"  price {product['price']}: {status}")
        if status not in (200, 201):
            print(json.dumps(out, indent=2)[:1200])

        # Availability: all territories, incl. future ones
        status, terr = request("GET", "/v1/territories?limit=200")
        all_terr = [{"type": "territories", "id": t["id"]} for t in terr["data"]]
        status, out = request("POST", "/v1/inAppPurchaseAvailabilities", {
            "data": {
                "type": "inAppPurchaseAvailabilities",
                "attributes": {"availableInNewTerritories": True},
                "relationships": {
                    "inAppPurchase": {"data": {"type": "inAppPurchases", "id": iap_id}},
                    "availableTerritories": {"data": all_terr},
                },
            }
        })
        print(f"  availability: {status}")

        # Review screenshot
        if shot_bytes:
            status, existing_shot = request("GET", f"/v2/inAppPurchases/{iap_id}/appStoreReviewScreenshot")
            if status == 200 and existing_shot.get("data"):
                print("  review screenshot exists")
            else:
                status, out = request("POST", "/v1/inAppPurchaseAppStoreReviewScreenshots", {
                    "data": {
                        "type": "inAppPurchaseAppStoreReviewScreenshots",
                        "attributes": {"fileName": "paywall.png", "fileSize": len(shot_bytes)},
                        "relationships": {"inAppPurchaseV2": {
                            "data": {"type": "inAppPurchases", "id": iap_id}
                        }},
                    }
                })
                assert status == 201, out
                shot_id = out["data"]["id"]
                upload_asset(out["data"]["attributes"]["uploadOperations"], shot_bytes)
                status, out = request("PATCH", f"/v1/inAppPurchaseAppStoreReviewScreenshots/{shot_id}", {
                    "data": {
                        "type": "inAppPurchaseAppStoreReviewScreenshots",
                        "id": shot_id,
                        "attributes": {"uploaded": True, "sourceFileChecksum": md5(shot_bytes)},
                    }
                })
                print(f"  review screenshot: {status}")

    print("IAPS DONE")


if __name__ == "__main__":
    main()
