"""Offline tests for the What's New patcher using a recorded fake ASC API."""
import importlib.util
import json
import sys
import tempfile
import types
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]

APP_ID = '6786840477'
VERSION_ID = 'version-uuid'
LOC_ID = 'loc-uuid'


def load_module(fake_request):
    """Import patch_whatsnew against a stub ascapi so no key or network is needed."""
    stub = types.ModuleType('ascapi')
    stub.APP_ID = APP_ID
    stub.request = fake_request
    previous = sys.modules.get('ascapi')
    sys.modules['ascapi'] = stub
    try:
        spec = importlib.util.spec_from_file_location('patch_whatsnew', TOOLS / 'patch_whatsnew.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
    finally:
        if previous is None:
            del sys.modules['ascapi']
        else:
            sys.modules['ascapi'] = previous
    return module


class FakeApi:
    def __init__(self, current_whats_new='old copy', versions=None, locales=('en-US', 'de-DE'), patch_status=200):
        self.whats_new = current_whats_new
        self.versions = ['3.3'] if versions is None else versions
        self.locales = locales
        self.patch_status = patch_status
        self.calls = []

    def __call__(self, method, path, body=None, **_):
        self.calls.append((method, path, body))
        if method == 'GET' and path.startswith(f'/v1/apps/{APP_ID}/appStoreVersions'):
            return 200, {'data': [
                {'id': f'{VERSION_ID}-{v}', 'type': 'appStoreVersions',
                 'attributes': {'versionString': v, 'platform': 'IOS', 'appVersionState': 'PREPARE_FOR_SUBMISSION'}}
                for v in self.versions]}
        if method == 'GET' and path.startswith(f'/v1/appStoreVersions/{VERSION_ID}-3.3/appStoreVersionLocalizations'):
            return 200, {'data': [self.localization(locale) for locale in self.locales]}
        if method == 'GET' and path == f'/v1/appStoreVersionLocalizations/{LOC_ID}-en-US':
            return 200, {'data': self.localization('en-US')}
        if method == 'PATCH' and path == f'/v1/appStoreVersionLocalizations/{LOC_ID}-en-US':
            if self.patch_status == 200:
                self.whats_new = body['data']['attributes']['whatsNew']
                return 200, {'data': self.localization('en-US')}
            return self.patch_status, {'errors': [{'status': str(self.patch_status), 'title': 'rejected'}]}
        raise AssertionError(f'unexpected request {method} {path}')

    def localization(self, locale):
        return {'id': f'{LOC_ID}-{locale}', 'type': 'appStoreVersionLocalizations',
                'attributes': {'locale': locale, 'whatsNew': self.whats_new if locale == 'en-US' else None}}


class PatchWhatsNewTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.listing = Path(self.temporary.name) / 'listing.json'
        self.text = 'Fresh release notes.\n\nSecond paragraph.'
        self.write_listing(version='3.3', whats_new=self.text)

    def write_listing(self, **fields):
        data = {'locale': 'en-US', 'name': 'Little Lifeline', 'version': fields.get('version', '3.3')}
        if 'whats_new' in fields:
            data['whatsNew'] = fields['whats_new']
        self.listing.write_text(json.dumps(data), encoding='utf-8')

    def run_main(self, api, *extra):
        module = load_module(api)
        return module, module.main(['--version', '3.3', '--listing', str(self.listing), *extra])

    def test_patches_only_whats_new_and_reads_back(self):
        api = FakeApi()
        _, code = self.run_main(api)
        self.assertEqual(code, 0)
        patches = [call for call in api.calls if call[0] == 'PATCH']
        self.assertEqual(len(patches), 1)
        body = patches[0][2]['data']
        self.assertEqual(body['type'], 'appStoreVersionLocalizations')
        self.assertEqual(body['id'], f'{LOC_ID}-en-US')
        self.assertEqual(body['attributes'], {'whatsNew': self.text})
        self.assertNotIn('relationships', body)
        self.assertEqual(api.whats_new, self.text)
        methods = {call[0] for call in api.calls}
        self.assertEqual(methods, {'GET', 'PATCH'}, 'Only GET and PATCH are allowed; nothing is submitted.')
        for method, path, _ in api.calls:
            self.assertNotIn('reviewSubmissions', path)
            self.assertNotIn('builds', path)

    def test_skips_patch_when_already_matching(self):
        api = FakeApi(current_whats_new=self.text)
        _, code = self.run_main(api)
        self.assertEqual(code, 0)
        self.assertFalse([call for call in api.calls if call[0] == 'PATCH'])

    def test_refuses_version_mismatch(self):
        self.write_listing(version='3.2', whats_new=self.text)
        api = FakeApi()
        module = load_module(api)
        with self.assertRaises(module.PatchError):
            module.main(['--version', '3.3', '--listing', str(self.listing)])
        self.assertEqual(api.calls, [], 'No API call may happen before the listing is validated.')

    def test_refuses_missing_whats_new(self):
        self.write_listing(version='3.3')
        api = FakeApi()
        module = load_module(api)
        with self.assertRaises(module.PatchError):
            module.main(['--version', '3.3', '--listing', str(self.listing)])
        self.assertEqual(api.calls, [])

    def test_requires_exactly_one_matching_version(self):
        api = FakeApi(versions=['3.2'])
        module = load_module(api)
        with self.assertRaises(module.PatchError):
            module.main(['--version', '3.3', '--listing', str(self.listing)])
        self.assertFalse([call for call in api.calls if call[0] == 'PATCH'])

    def test_requires_requested_locale(self):
        api = FakeApi(locales=('de-DE',))
        module = load_module(api)
        with self.assertRaises(module.PatchError):
            module.main(['--version', '3.3', '--listing', str(self.listing), '--locale', 'en-US'])
        self.assertFalse([call for call in api.calls if call[0] == 'PATCH'])

    def test_reports_rejected_patch(self):
        api = FakeApi(patch_status=409)
        module = load_module(api)
        with self.assertRaises(module.PatchError):
            module.main(['--version', '3.3', '--listing', str(self.listing)])


if __name__ == '__main__':
    unittest.main()
