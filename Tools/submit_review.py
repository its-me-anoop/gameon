#!/usr/bin/env python3
"""Attaches the latest VALID build to Gravitile v1.0 and submits for review."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from ascapi import APP_ID, request

VID = "9a76a036-4ca2-4400-8028-cff63d063795"
BUILD_NUMBER = sys.argv[1] if len(sys.argv) > 1 else "4"


def main():
    # Find the requested build
    status, builds = request(
        "GET", f"/v1/builds?filter[app]={APP_ID}&limit=10&fields[builds]=version,processingState"
    )
    build = next(
        (b for b in builds["data"]
         if b["attributes"]["version"] == BUILD_NUMBER
         and b["attributes"]["processingState"] == "VALID"),
        None,
    )
    if not build:
        print(f"build {BUILD_NUMBER} not VALID yet"); sys.exit(1)
    print(f"attaching build {BUILD_NUMBER} ({build['id']})")

    status, out = request("PATCH", f"/v1/appStoreVersions/{VID}", {
        "data": {"type": "appStoreVersions", "id": VID,
                 "relationships": {"build": {"data": {"type": "builds", "id": build["id"]}}}}
    })
    assert status == 200, out
    print("build attached")

    # Reuse an open submission if one exists, else create one.
    status, subs = request(
        "GET", f"/v1/reviewSubmissions?filter[app]={APP_ID}&filter[state]=READY_FOR_REVIEW&limit=5"
    )
    if status == 200 and subs.get("data"):
        sub_id = subs["data"][0]["id"]
        print(f"reusing open submission {sub_id}")
    else:
        status, out = request("POST", "/v1/reviewSubmissions", {
            "data": {"type": "reviewSubmissions", "attributes": {"platform": "IOS"},
                     "relationships": {"app": {"data": {"type": "apps", "id": APP_ID}}}}
        })
        assert status == 201, out
        sub_id = out["data"]["id"]
        print(f"created submission {sub_id}")

    # Add the version item unless it's already in the submission.
    status, items = request("GET", f"/v1/reviewSubmissions/{sub_id}/items")
    if status == 200 and items.get("data"):
        print("submission already has items")
    else:
        status, out = request("POST", "/v1/reviewSubmissionItems", {
            "data": {"type": "reviewSubmissionItems",
                     "relationships": {
                         "reviewSubmission": {"data": {"type": "reviewSubmissions", "id": sub_id}},
                         "appStoreVersion": {"data": {"type": "appStoreVersions", "id": VID}},
                     }}
        })
        if status != 201:
            print(json.dumps(out, indent=2)[:4000]); sys.exit(1)
        print("version item added")

    status, out = request("PATCH", f"/v1/reviewSubmissions/{sub_id}", {
        "data": {"type": "reviewSubmissions", "id": sub_id, "attributes": {"submitted": True}}
    })
    if status != 200:
        print(json.dumps(out, indent=2)[:4000]); sys.exit(1)
    attrs = out["data"]["attributes"]
    print(f"SUBMITTED: state={attrs.get('state')} at {attrs.get('submittedDate')}")


if __name__ == "__main__":
    main()
