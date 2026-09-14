#!/usr/bin/env python3
"""Replaces only the App Store "What's New" text for one Little Lifeline localization.

The copy comes exclusively from docs/appstore/listing.json; nothing is authored
here. The script locates the App Store version whose versionString matches the
requested version, selects the requested locale, PATCHes `whatsNew` alone and
reads the field back. It never attaches builds, never uploads to TestFlight and
never creates or touches a review submission.

Credentials are resolved by ascapi (ASC_KEY_ID, ASC_ISSUER_ID, ASC_KEY_PATH on
CI). Output is limited to identifiers, lengths and API error bodies; no key
material is ever printed.
"""
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from ascapi import APP_ID, request

REPO = Path(__file__).resolve().parent.parent
DEFAULT_LISTING = REPO / "docs/appstore/listing.json"
MAX_WHATS_NEW = 4000


class PatchError(RuntimeError):
    pass


def require(condition, message):
    if not condition:
        raise PatchError(message)


def get(path):
    status, result = request("GET", path)
    require(status == 200, f"GET {path} failed with {status}: {json.dumps(result)[:2000]}")
    return result["data"]


def load_whats_new(listing_path, version):
    listing = json.loads(Path(listing_path).read_text(encoding="utf-8"))
    require(listing.get("version") == version,
            f"listing.json is for version {listing.get('version')!r}, not {version!r}. Refusing to patch.")
    text = listing.get("whatsNew")
    require(isinstance(text, str) and text.strip(), "listing.json has no whatsNew text.")
    require(len(text) <= MAX_WHATS_NEW, f"whatsNew exceeds {MAX_WHATS_NEW} characters.")
    return text


def find_version(version):
    versions = get(f"/v1/apps/{APP_ID}/appStoreVersions"
                   f"?filter[versionString]={version}&filter[platform]=IOS&limit=200")
    matches = [v for v in versions if v["attributes"].get("versionString") == version]
    require(len(matches) == 1,
            f"Expected exactly one iOS App Store version {version}, found {len(matches)}.")
    return matches[0]


def find_localization(version_id, locale):
    localizations = get(f"/v1/appStoreVersions/{version_id}/appStoreVersionLocalizations?limit=200")
    matches = [l for l in localizations if l["attributes"].get("locale") == locale]
    available = sorted(l["attributes"].get("locale", "?") for l in localizations)
    require(len(matches) == 1, f"Locale {locale} not found once; available: {available}")
    return matches[0]


def patch_whats_new(localization_id, text):
    status, out = request("PATCH", f"/v1/appStoreVersionLocalizations/{localization_id}", {
        "data": {
            "type": "appStoreVersionLocalizations",
            "id": localization_id,
            "attributes": {"whatsNew": text},
        }
    })
    require(status in (200, 204),
            f"PATCH appStoreVersionLocalizations/{localization_id} failed with {status}: "
            f"{json.dumps(out)[:2000]}")
    saved = get(f"/v1/appStoreVersionLocalizations/{localization_id}")
    require(saved["attributes"].get("whatsNew") == text, "What's New readback differs from listing.json.")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--version", required=True, help="App Store versionString, e.g. 3.3")
    parser.add_argument("--locale", default="en-US", help="App Store localization, e.g. en-US")
    parser.add_argument("--listing", default=str(DEFAULT_LISTING), help="Path to listing.json")
    args = parser.parse_args(argv)

    text = load_whats_new(args.listing, args.version)
    version = find_version(args.version)
    state = version["attributes"].get("appVersionState") or version["attributes"].get("appStoreState")
    print(f"version: {version['id']} ({args.version}, {state})")

    localization = find_localization(version["id"], args.locale)
    print(f"localization: {localization['id']} ({args.locale})")

    if localization["attributes"].get("whatsNew") == text:
        print(f"whatsNew already matches listing.json ({len(text)} characters); nothing to patch.")
        return 0

    patch_whats_new(localization["id"], text)
    print(f"whatsNew length: {len(text)}")
    print("WHATS NEW PATCHED AND VERIFIED. No review submission was created.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except PatchError as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(1)
