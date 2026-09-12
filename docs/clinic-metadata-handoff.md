# Clinic beta metadata handoff

The clinic metadata handoff is **complete**, with exact readback at **08:51:39 UTC on 12 September 2026**. The beta description and reviewer instructions were updated before archive promotion; the testing notes were applied to verified 3.1 (15). Existing prices, products, leaderboards, contacts, agreements, tester groups and review submissions were preserved. The commands below remain read-only by default and are retained as the reproducible recipe.

The final paginated readback verifies Apple build `57f80045-8962-44b7-87e6-4b444beaedbb` as 3.1 (15), `VALID`, unexpired and `IN_BETA_TESTING`, with explicit membership in Internal group `a1098f67-cfde-4eaa-8626-e250f881596a`. Description, reviewer notes and en-US build localization `7275a9cc-e570-4b57-8a34-1abafe653edc` match their committed text files. See [the delivery record](idle-clinic-release.md) and [provider readback](../build/qa-clinic/ec09-final-delivery-readback.json). Build 15 is now used and must not be uploaded again.

| Operation | Endpoint | Fields and committed source |
| --- | --- | --- |
| Replace en-US beta description | `PATCH /v1/betaAppLocalizations/3b4b5dbe-e68b-4298-ae55-e70c59c8a285` | `description` from `docs/testflight-clinic-description.txt` |
| Replace beta reviewer instructions | `PATCH /v1/betaAppReviewDetails/6786840477` | `notes` from `docs/testflight-clinic-review-notes.txt`; preserve contacts and existing `demoAccountRequired=false` |
| Attach exact build testing notes | `POST /v1/betaBuildLocalizations`, or `PATCH /v1/betaBuildLocalizations/{existing en-US ID}` | `whatsNew` from `docs/testflight-clinic-what-to-test.txt`; bind only to verified 3.1 (15) |

Do not run `configure_lifeline_services.py --apply` for this release: that helper still targets the previous train metadata, weekly board and collection copy.

## Description and reviewer instructions

This block defaults to a read-only comparison. Set `CLINIC_APPLY_METADATA=1` on the Python command only when the accepted archive is ready for promotion. It patches only changed text, then requires exact readback. Text files are read as UTF-8 with outer whitespace stripped; no raw shell interpolation or private contact output is used.

```sh
python3 - <<'PY'
import os, sys
from pathlib import Path
sys.path.insert(0, 'Tools')
from ascapi import request

def get(path):
    status, result = request('GET', path)
    assert status == 200, (path, status)
    return result['data']

app = get('/v1/apps/6786840477')
assert app['attributes']['bundleId'] == 'com.flutterly.gravitile'
locales = get('/v1/apps/6786840477/betaAppLocalizations?limit=200')
locale = next(r for r in locales if r['attributes']['locale'] == 'en-US')
assert locale['id'] == '3b4b5dbe-e68b-4298-ae55-e70c59c8a285'
review = get('/v1/apps/6786840477/betaAppReviewDetail')
assert review['id'] == '6786840477'
assert review['attributes']['demoAccountRequired'] is False
changes = [
    ('betaAppLocalizations', locale, 'description', 'docs/testflight-clinic-description.txt'),
    ('betaAppReviewDetails', review, 'notes', 'docs/testflight-clinic-review-notes.txt'),
]
apply = os.environ.get('CLINIC_APPLY_METADATA') == '1'
for kind, current, field, filename in changes:
    text = Path(filename).read_text(encoding='utf-8').strip()
    assert text and len(text) <= 4000
    path = f'/v1/{kind}/{current["id"]}'
    if current['attributes'].get(field) == text:
        print('Already matches:', path, field)
        continue
    print('APPLY' if apply else 'PLANNED', 'PATCH', path, field)
    if apply:
        status, _ = request('PATCH', path, {'data': {
            'type': kind, 'id': current['id'], 'attributes': {field: text}}})
        assert status in (200, 204), (path, status)
        assert get(path)['attributes'][field] == text, 'Readback differs'
        print('Verified:', path, field)
PY
```

## Per-build What to Test

After upload, require exact 3.1 (15), `processingState=VALID`, `expired=false`, `internalBuildState=IN_BETA_TESTING`, and membership in the existing Internal group. The relevant read endpoints are:

- `GET /v1/apps/6786840477/buildUploads?limit=200` while processing.
- `GET /v1/builds?filter[app]=6786840477&include=preReleaseVersion&limit=200` for the build ID and version relationship.
- `GET /v1/builds/{build ID}/buildBetaDetail` for internal availability.
- `GET /v1/betaGroups/a1098f67-cfde-4eaa-8626-e250f881596a/builds?limit=200` for membership.

Follow `links.next` when present. Transport success alone does not establish availability. Replace the placeholder below with the verified build UUID. This block also defaults to read-only; prefix the Python command with `CLINIC_APPLY_METADATA=1` to perform only the planned localization operation.

```sh
CLINIC_BUILD_ID='VERIFIED_BUILD_15_UUID' python3 - <<'PY'
import os, sys, uuid
from pathlib import Path
sys.path.insert(0, 'Tools')
from ascapi import request

def get(path):
    status, result = request('GET', path)
    assert status == 200, (path, status)
    return result['data']

build_id = str(uuid.UUID(os.environ['CLINIC_BUILD_ID']))
build = get(f'/v1/builds/{build_id}?include=app,preReleaseVersion')
assert build['relationships']['app']['data']['id'] == '6786840477'
assert build['attributes']['version'] == '15'
assert build['attributes']['processingState'] == 'VALID'
assert build['attributes']['expired'] is False
version_id = build['relationships']['preReleaseVersion']['data']['id']
version = get(f'/v1/preReleaseVersions/{version_id}')
assert version['attributes']['version'] == '3.1'
detail = get(f'/v1/builds/{build_id}/buildBetaDetail')
assert detail['attributes']['internalBuildState'] == 'IN_BETA_TESTING'
locales = get(f'/v1/builds/{build_id}/betaBuildLocalizations?limit=200')
existing = next((r for r in locales if r['attributes']['locale'] == 'en-US'), None)
text = Path('docs/testflight-clinic-what-to-test.txt').read_text(encoding='utf-8').strip()
assert text and len(text) <= 4000
if existing and existing['attributes'].get('whatsNew') == text:
    print('Testing notes already match:', existing['id'])
    raise SystemExit(0)
if existing:
    method, path = 'PATCH', f'/v1/betaBuildLocalizations/{existing["id"]}'
    data = {'type': 'betaBuildLocalizations', 'id': existing['id'], 'attributes': {'whatsNew': text}}
else:
    method, path = 'POST', '/v1/betaBuildLocalizations'
    data = {'type': 'betaBuildLocalizations', 'attributes': {'locale': 'en-US', 'whatsNew': text},
            'relationships': {'build': {'data': {'type': 'builds', 'id': build_id}}}}
print('APPLY' if os.environ.get('CLINIC_APPLY_METADATA') == '1' else 'PLANNED', method, path)
if os.environ.get('CLINIC_APPLY_METADATA') == '1':
    status, _ = request(method, path, {'data': data})
    assert status in (200, 201, 204), (path, status)
    saved = get(f'/v1/builds/{build_id}/betaBuildLocalizations?limit=200')
    locale = next(r for r in saved if r['attributes']['locale'] == 'en-US')
    assert locale['attributes']['whatsNew'] == text, 'Testing-note readback differs'
    print('Exact testing notes verified:', locale['id'])
PY
```

Record the source SHA, signed archive checksum/run, upload run, Apple build UUID, internal membership and localization readbacks in the final release record. These operations do not submit App Store or external beta review and do not send tester messages.
