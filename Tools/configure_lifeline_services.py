#!/usr/bin/env python3
"""Plan Little Lifeline metadata changes; read-only unless --apply is explicit.

Only the Lifeline weekly board, en-US editable app/beta text, and existing Plus
copy are in scope. Never changes prices, tips, agreements, contacts, review
submission, tester groups or notifications. Re-running compares current state.
"""
import argparse
import json
from datetime import datetime, timedelta, timezone
from pathlib import Path

from ascapi import APP_ID, BUNDLE_ID, request

ROOT = Path(__file__).resolve().parent.parent
WEEKLY_ID = 'grv3.lifeline.weekly.v1'
PLUS_ID = 'com.flutterly.gravitile.plus'
APP_NAME = 'Little Lifeline'
SUBTITLE = 'A little care. A moving world.'
COLLECTION_NAME = 'Founder’s Carriage Collection'
COLLECTION_DESCRIPTION = 'Sunrise, Coastal and Heritage. Cosmetic train finishes.'
EDITABLE_STATES = {'REJECTED', 'DEVELOPER_REJECTED', 'PREPARE_FOR_SUBMISSION', 'METADATA_REJECTED'}


def fetch(path):
    status, result = request('GET', path)
    if status != 200:
        # Do not echo response bodies: contact information is outside the report's scope.
        raise RuntimeError(f'GET {path}: HTTP {status}')
    return result['data']


def all_resources(path):
    values = []
    while path:
        status, result = request('GET', path)
        if status != 200:
            raise RuntimeError(f'GET {path}: HTTP {status}')
        values.extend(result.get('data', []))
        path = result.get('links', {}).get('next')
        if path:
            prefix = 'https://api.appstoreconnect.apple.com'
            if not path.startswith(prefix + '/'):
                raise RuntimeError('Unexpected pagination origin.')
            path = path[len(prefix):]
    return values


def next_monday(now):
    return (now + timedelta(days=7 - now.weekday())).replace(hour=0, minute=0, second=0, microsecond=0)


def parse_start(value):
    try:
        instant = datetime.strptime(value, '%Y-%m-%dT%H:%M:%SZ').replace(tzinfo=timezone.utc)
    except ValueError as error:
        raise ValueError('Weekly start must be an ISO UTC timestamp such as 2026-09-14T00:00:00Z.') from error
    if instant.weekday() != 0 or (instant.hour, instant.minute, instant.second) != (0, 0, 0):
        raise ValueError('Weekly start must be Monday at exactly 00:00 UTC.')
    return instant


def relation(kind, identifier):
    return {'data': {'type': kind, 'id': identifier}}


def text(name):
    return (ROOT / 'docs' / name).read_text().strip()


def patch(actions, kind, resource, path, attributes, label):
    changed = {key: value for key, value in attributes.items() if resource['attributes'].get(key) != value}
    if changed:
        actions.append({'label': label, 'method': 'PATCH', 'path': path,
                        'data': {'type': kind, 'id': resource['id'], 'attributes': changed}})


def plan(start):
    assert len(APP_NAME) <= 30 and len(SUBTITLE) <= 30
    assert len(COLLECTION_NAME) <= 30 and len(COLLECTION_DESCRIPTION) <= 55
    app = fetch(f'/v1/apps/{APP_ID}')
    if app['attributes']['bundleId'] != BUNDLE_ID:
        raise RuntimeError('Unexpected app identity; stopping before preparing changes.')
    infos = all_resources(f'/v1/apps/{APP_ID}/appInfos?limit=50')
    editable = [value for value in infos if value['attributes'].get('appStoreState') in EDITABLE_STATES]
    if len(editable) != 1:
        raise RuntimeError('Expected exactly one editable app-info record; inspect it before configuring.')
    info = editable[0]
    localizations = all_resources(f"/v1/appInfos/{info['id']}/appInfoLocalizations?limit=50")
    app_locale = next(value for value in localizations if value['attributes']['locale'] == 'en-US')
    beta_locales = all_resources(f'/v1/apps/{APP_ID}/betaAppLocalizations?limit=50')
    beta = next(value for value in beta_locales if value['attributes']['locale'] == 'en-US')
    review = fetch(f'/v1/apps/{APP_ID}/betaAppReviewDetail')
    products = all_resources(f'/v1/apps/{APP_ID}/inAppPurchasesV2?limit=50')
    plus = next(value for value in products if value['attributes']['productId'] == PLUS_ID)
    if plus['attributes']['inAppPurchaseType'] != 'NON_CONSUMABLE':
        raise RuntimeError('Existing Plus product is not a non-consumable; do not replace it.')
    plus_locales = all_resources(f"/v2/inAppPurchases/{plus['id']}/inAppPurchaseLocalizations?limit=50")
    plus_locale = next(value for value in plus_locales if value['attributes']['locale'] == 'en-US')
    detail = fetch(f'/v1/apps/{APP_ID}/gameCenterDetail')
    boards = all_resources(f"/v1/gameCenterDetails/{detail['id']}/gameCenterLeaderboards?limit=200")
    board = next((value for value in boards if value['attributes']['vendorIdentifier'] == WEEKLY_ID), None)
    board_attributes = {'defaultFormatter': 'INTEGER', 'referenceName': 'Little Lifeline · Weekly Call',
                        'vendorIdentifier': WEEKLY_ID, 'submissionType': 'BEST_SCORE', 'scoreSortType': 'DESC',
                        'scoreRangeStart': '0', 'scoreRangeEnd': '2147483647',
                        'recurrenceStartDate': start.strftime('%Y-%m-%dT%H:%M:%SZ'),
                        'recurrenceDuration': 'PT168H', 'recurrenceRule': 'FREQ=DAILY;INTERVAL=7'}
    actions = []
    if board is None:
        actions.append({'label': 'Create isolated weekly leaderboard', 'method': 'POST',
                        'path': '/v1/gameCenterLeaderboards', 'saveAs': 'weekly',
                        'data': {'type': 'gameCenterLeaderboards', 'attributes': board_attributes,
                                 'relationships': {'gameCenterDetail': relation('gameCenterDetails', detail['id'])}}})
        actions.append({'label': 'Localize weekly leaderboard', 'method': 'POST',
                        'path': '/v1/gameCenterLeaderboardLocalizations',
                        'data': {'type': 'gameCenterLeaderboardLocalizations',
                                 'attributes': {'locale': 'en-US', 'name': 'Little Lifeline · Weekly Call'},
                                 'relationships': {'gameCenterLeaderboard': relation('gameCenterLeaderboards', '$weekly')}}})
    else:
        # Existing recurrence is an established identity, not a setting to reset on each run.
        parse_start(board['attributes']['recurrenceStartDate'])
        expected = {key: value for key, value in board_attributes.items() if key != 'recurrenceStartDate'}
        for key, value in expected.items():
            if str(board['attributes'].get(key)) != value:
                raise RuntimeError(f'Existing Lifeline board differs at {key}; review manually rather than resetting results.')
        board_locales = all_resources(f"/v1/gameCenterLeaderboards/{board['id']}/localizations?limit=50")
        locale = next((value for value in board_locales if value['attributes']['locale'] == 'en-US'), None)
        if locale is None:
            actions.append({'label': 'Localize weekly leaderboard', 'method': 'POST',
                            'path': '/v1/gameCenterLeaderboardLocalizations',
                            'data': {'type': 'gameCenterLeaderboardLocalizations',
                                     'attributes': {'locale': 'en-US', 'name': 'Little Lifeline · Weekly Call'},
                                     'relationships': {'gameCenterLeaderboard': relation('gameCenterLeaderboards', board['id'])}}})
        else:
            patch(actions, 'gameCenterLeaderboardLocalizations', locale,
                  f"/v1/gameCenterLeaderboardLocalizations/{locale['id']}",
                  {'name': 'Little Lifeline · Weekly Call'}, 'Localize weekly leaderboard')
    patch(actions, 'appInfoLocalizations', app_locale, f"/v1/appInfoLocalizations/{app_locale['id']}",
          {'name': APP_NAME, 'subtitle': SUBTITLE}, 'Update editable app identity')
    patch(actions, 'betaAppLocalizations', beta, f"/v1/betaAppLocalizations/{beta['id']}",
          {'description': text('testflight-lifeline-description.txt')}, 'Update TestFlight description')
    patch(actions, 'betaAppReviewDetails', review, f"/v1/betaAppReviewDetails/{review['id']}",
          {'demoAccountRequired': False, 'notes': text('testflight-lifeline-review-notes.txt')}, 'Update beta reviewer instructions')
    patch(actions, 'inAppPurchases', plus, f"/v2/inAppPurchases/{plus['id']}",
          {'name': 'Little Lifeline Founder’s Collection',
           'reviewNote': 'The top-right gear opens the depot; its palette icon (Finishes) opens the Founder’s Carriage Collection. '
                         'The existing com.flutterly.gravitile.plus non-consumable unlocks Sunrise, Coastal and Heritage cosmetic finishes. '
                         'Prior verified Plus purchases retain access. Restore purchases is in the depot and collection screen. '
                         'Care, income, all campaign content and the weekly challenge remain free and equal; finishes provide no power. '
                         'Optional tips support development, grant no content, and are not charitable donations.'},
          'Update existing Plus reference and reviewer instructions')
    patch(actions, 'inAppPurchaseLocalizations', plus_locale, f"/v1/inAppPurchaseLocalizations/{plus_locale['id']}",
          {'name': COLLECTION_NAME, 'description': COLLECTION_DESCRIPTION}, 'Update collection display copy')
    state = {'appId': APP_ID, 'currentAppName': app['attributes']['name'],
             'plusState': plus['attributes']['state'], 'familySharingPreserved': plus['attributes']['familySharable'],
             'weeklyExists': board is not None,
             'weeklyStartUTC': board['attributes']['recurrenceStartDate'] if board else board_attributes['recurrenceStartDate']}
    return state, actions


def resolve(value, created):
    if isinstance(value, str) and value.startswith('$'):
        return created[value[1:]]
    if isinstance(value, dict):
        return {key: resolve(item, created) for key, item in value.items()}
    if isinstance(value, list):
        return [resolve(item, created) for item in value]
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--weekly-start', help='New board only: future Monday, YYYY-MM-DDT00:00:00Z; defaults to next Monday')
    args = parser.parse_args()
    now = datetime.now(timezone.utc)
    start = parse_start(args.weekly_start) if args.weekly_start else next_monday(now)
    state, actions = plan(start)
    if args.apply and not state['weeklyExists'] and start <= datetime.now(timezone.utc):
        parser.error('A new recurring leaderboard requires a future Monday; no writes performed.')
    print(json.dumps({'mode': 'apply' if args.apply else 'read-only', 'state': state, 'actions': actions}, indent=2, ensure_ascii=False))
    if not args.apply:
        return
    created = {}
    for action in actions:
        data = resolve(action['data'], created)
        status, result = request(action['method'], action['path'], {'data': data})
        if status not in (200, 201, 204):
            raise RuntimeError(f"{action['label']}: HTTP {status}; stop and re-run the read-only plan to inspect partial application.")
        if action.get('saveAs'):
            created[action['saveAs']] = result['data']['id']
        print(json.dumps({'applied': action['label'], 'status': status}))
    state, remaining = plan(start)
    print(json.dumps({'readback': state, 'remainingActions': remaining}, indent=2, ensure_ascii=False))
    if remaining:
        raise RuntimeError('Some requested metadata was not confirmed by readback.')
    print('Scoped metadata verified. No app/beta review was submitted and no tester was notified.')


if __name__ == '__main__':
    main()
