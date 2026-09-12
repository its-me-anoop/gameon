import argparse
import base64
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
from pathlib import Path
import plistlib
import shutil
import stat
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import clinic_archive as C


class ArchiveTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.archive = self.root / 'unsigned' / C.ARCHIVE
        self.app = self.archive / 'Products/Applications/LittleLifeline.app'
        self.app.mkdir(parents=True)
        self.info = {'CFBundleIdentifier': C.BUNDLE, 'CFBundleDisplayName': 'Little Lifeline',
                     'CFBundleExecutable': 'LittleLifeline', 'CFBundleShortVersionString': '3.3',
                     'CFBundleVersion': '19', 'DTPlatformName': 'iphoneos',
                     'BuildMachineOSBuild': '25G83', 'DTXcodeBuild': '17F113'}
        (self.app / 'Info.plist').write_bytes(plistlib.dumps(self.info))
        (self.app / 'LittleLifeline').write_bytes(b'\xcf\xfa\xed\xfeunsigned app')
        framework = self.app / 'Frameworks/UnityFramework.framework'
        framework.mkdir(parents=True)
        (framework / 'Info.plist').write_bytes(plistlib.dumps({'CFBundleExecutable': 'UnityFramework'}))
        (framework / 'UnityFramework').write_bytes(b'\xcf\xfa\xed\xfeunsigned framework')
        (self.app / 'PrivacyInfo.xcprivacy').write_bytes(plistlib.dumps({'NSPrivacyAccessedAPITypes': [
            {'NSPrivacyAccessedAPIType': 'NSPrivacyAccessedAPICategoryUserDefaults',
             'NSPrivacyAccessedAPITypeReasons': ['CA92.1']},
            {'NSPrivacyAccessedAPIType': 'NSPrivacyAccessedAPICategoryFileTimestamp',
             'NSPrivacyAccessedAPITypeReasons': ['C617.1']}]}))
        (self.app / 'Data').mkdir()
        (self.app / 'Data/level').write_bytes(b'game content')
        (self.archive / 'dSYMs').mkdir()
        (self.archive / 'dSYMs/dwarf').write_bytes(b'unchanged dwarf symbols')
        self.outer = {'ArchiveVersion': 2, 'CreationDate': datetime(2026, 9, 12),
                      'Name': 'Little Lifeline', 'SchemeName': 'Unity-iPhone',
                      'ApplicationProperties': {'ApplicationPath': 'Applications/LittleLifeline.app',
                                                'Architectures': ['arm64'], **{key: self.info[key] for key in
                                                ('CFBundleIdentifier', 'CFBundleShortVersionString', 'CFBundleVersion')}}}
        (self.archive / 'Info.plist').write_bytes(plistlib.dumps(self.outer))
        self.source = 'a' * 40
        self.export = 'b' * 64
        self.metadata = {'format': 1, 'sourceCommit': self.source, 'product': 'idle-clinic',
                         'scenePath': 'Assets/IdleClinic/Scenes/Clinic.unity', 'developmentBuild': False,
                         'iosSdk': 'device', 'bundleIdentifier': C.BUNDLE, 'marketingVersion': '3.3',
                         'buildNumber': '19', 'unityTests': {'total': 3, 'passed': 3, 'failed': 0, 'skipped': 0},
                         'exportSha256': self.export, 'signingState': 'unsigned', 'workflowRun': '123',
                         'buildMachineOS': '25G83', 'xcodeBuild': '17F113'}
        self.package = self.root / C.UNSIGNED_ZIP
        C.zip_tree(self.archive, self.package)
        self.metadata.update(unsignedArchiveSha256=C.sha256(self.package), inventory=C.inventory(self.archive))
        self.manifest = self.root / 'unsigned-archive-manifest.json'
        C.json_write(self.manifest, self.metadata)
        self.identity = 'Apple Development: Test Person (EXAMPLE123)'
        self.cert = b'public certificate fixture'
        self.device = 'registered-device-fixture'
        self.profile = {'UUID': 'profile-fixture', 'Name': 'Fixture QA profile', 'TeamIdentifier': [C.TEAM],
                        'ExpirationDate': datetime.now(timezone.utc).replace(tzinfo=None) + timedelta(days=2),
                        'DeveloperCertificates': [self.cert], 'ProvisionedDevices': [self.device],
                        'Entitlements': {**C.app_entitlements(), 'keychain-access-groups': [C.TEAM + '.*']}}
        self.profile_path = self.root / 'fixture.mobileprovision'
        self.profile_path.write_bytes(b'CMS profile fixture')
        self.args = argparse.Namespace(unsigned=self.package, manifest=self.manifest,
                    unsigned_sha256=C.sha256(self.package), manifest_sha256=C.sha256(self.manifest),
                    unsigned_run_id='123', source=self.source, export_sha256=self.export,
                    signer_sha256=C.digest_bytes(self.cert), device_sha256=C.digest_bytes(self.device.encode()),
                    identity=self.identity, profile=self.profile_path, output=self.root / 'output', write=False)
        self.calls = []
        self.run_patch = patch.object(C, 'run', side_effect=self.fake_run)
        self.run_patch.start()
        self.addCleanup(self.run_patch.stop)

    def fake_run(self, *args):
        self.calls.append(args)
        if args[:3] == ('security', 'cms', '-D'):
            return plistlib.dumps(self.profile)
        if args[:2] == ('security', 'find-identity'):
            fingerprint = hashlib.sha1(self.cert).hexdigest().upper()
            return f'  1) {fingerprint} "{self.identity}"\n'.encode()
        if args[:2] == ('xcrun', 'nm'):
            return '\n'.join('_OO_' + symbol for symbol in C.BRIDGE).encode()
        raise AssertionError('Unexpected tool execution: ' + str(args))

    def fake_compare(self, before, after):
        self.assertEqual(before, after)
        return {'unsignedSha256': C.digest_bytes(before), 'signedSha256': C.digest_bytes(after),
                'payloadSha256': C.digest_bytes(before), 'uuid': 'fixture', 'architecture': 'arm64'}

    def make_signed(self):
        signed = self.root / 'signed' / C.ARCHIVE
        shutil.copytree(self.archive, signed)
        app = signed / self.app.relative_to(self.archive)
        shutil.copyfile(self.profile_path, app / 'embedded.mobileprovision')
        outer = copy.deepcopy(self.outer)
        outer['ApplicationProperties'].update(SigningIdentity=self.identity, Team=C.TEAM)
        (signed / 'Info.plist').write_bytes(plistlib.dumps(outer))
        for folder in (app, app / 'Frameworks/UnityFramework.framework'):
            (folder / '_CodeSignature').mkdir()
            (folder / '_CodeSignature/CodeResources').write_bytes(b'signature fixture')
        return signed

    def make_transfer(self, signed):
        package = self.root / C.SIGNED_ZIP
        C.zip_tree(signed, package)
        with patch.object(C, 'compare_macho', side_effect=self.fake_compare):
            binary = C.compare_archives(self.archive, signed)
        receipt = {'format': 1, 'sourceCommit': self.source, 'unsignedWorkflowRun': '123',
                   'unsignedArchiveSha256': C.sha256(self.package), 'unsignedManifestSha256': C.sha256(self.manifest),
                   'signingCertificateSha256': self.args.signer_sha256, 'deviceSha256': self.args.device_sha256,
                   'archiveSha256': C.sha256(package), 'profileSha256': C.sha256(self.profile_path),
                   'profileUUID': self.profile['UUID'], 'signingIdentity': self.identity, 'binaryIdentities': binary}
        receipt_path = self.root / C.RECEIPT
        C.json_write(receipt_path, receipt)
        transfer = self.root / C.TRANSFER
        with zipfile.ZipFile(transfer, 'x') as output:
            output.write(package, C.SIGNED_ZIP)
            output.write(receipt_path, C.RECEIPT)
        self.args.transfer = transfer
        self.args.transfer_sha256 = C.sha256(transfer)
        self.args.workflow_run = '456'

    def test_sign_preflight_does_not_sign_or_create_output(self):
        with patch.object(C, 'inspect_macho', return_value={}), patch.object(C, 'compare_macho', side_effect=self.fake_compare):
            report = C.sign_local(self.args)
        self.assertEqual(report['unsignedWorkflowRun'], '123')
        self.assertFalse(self.args.output.exists())
        self.assertFalse(any(call[0] == 'codesign' for call in self.calls))
        self.assertEqual(C.sha256(self.package), self.args.unsigned_sha256)

    def test_wrong_pinned_input_fails_before_tool_calls(self):
        self.args.unsigned_sha256 = 'c' * 64
        with self.assertRaisesRegex(ValueError, 'Pinned unsigned ZIP'):
            C.sign_local(self.args)
        self.assertFalse(self.calls)
        self.assertFalse(self.args.output.exists())

    def test_changed_manifest_pin_fails_before_tools(self):
        self.args.manifest_sha256 = 'c' * 64
        with self.assertRaisesRegex(ValueError, 'Pinned unsigned manifest'):
            C.sign_local(self.args)
        self.assertFalse(self.calls)

    def test_wrong_source_and_run_fail_closed(self):
        for field, value in [('source', 'c' * 40), ('unsigned_run_id', '456')]:
            args = copy.copy(self.args)
            setattr(args, field, value)
            with self.assertRaises(ValueError):
                C.check_unsigned(args, self.root / ('extract-' + field))

    def test_profile_rejects_other_device_other_signer_expiry_and_team(self):
        cases = [({'ProvisionedDevices': ['other']}, 'physical device'),
                 ({'DeveloperCertificates': [b'other']}, 'pinned signer'),
                 ({'ExpirationDate': datetime(2020, 1, 1)}, 'Expired'),
                 ({'TeamIdentifier': ['OTHERTEAM']}, 'team')]
        for values, message in cases:
            with self.subTest(values=values):
                original = self.profile
                self.profile = {**original, **values}
                with self.assertRaisesRegex(ValueError, message):
                    C.read_profile(self.profile_path, self.args.signer_sha256, self.args.device_sha256)
                self.profile = original

    def test_only_signing_metadata_and_files_are_allowed(self):
        signed = self.make_signed()
        with patch.object(C, 'compare_macho', side_effect=self.fake_compare):
            report = C.compare_archives(self.archive, signed)
        self.assertEqual(len(report), 2)

    def test_resource_plist_dsym_and_archive_version_changes_are_rejected(self):
        signed = self.make_signed()
        for relative in ('Products/Applications/LittleLifeline.app/Info.plist',
                         'Products/Applications/LittleLifeline.app/Data/level', 'dSYMs/dwarf'):
            with self.subTest(relative=relative):
                path = signed / relative
                old = path.read_bytes()
                path.write_bytes(old + b'changed')
                with patch.object(C, 'compare_macho', side_effect=self.fake_compare), self.assertRaises(ValueError):
                    C.compare_archives(self.archive, signed)
                path.write_bytes(old)
        outer = plistlib.loads((signed / 'Info.plist').read_bytes())
        outer['ApplicationProperties']['CFBundleVersion'] = '20'
        (signed / 'Info.plist').write_bytes(plistlib.dumps(outer))
        with self.assertRaisesRegex(ValueError, 'Archive metadata'):
            C.compare_archives(self.archive, signed)

    def test_unexpected_new_signature_named_resource_is_rejected(self):
        signed = self.make_signed()
        (signed / 'Products/Applications/LittleLifeline.app/_CodeSignature/evil').write_bytes(b'extra')
        with self.assertRaisesRegex(ValueError, 'Unexpected added'):
            C.compare_archives(self.archive, signed)

    def test_adoption_preflight_verifies_signature_and_active_profile_without_output(self):
        self.make_transfer(self.make_signed())
        with patch.object(C, 'compare_macho', side_effect=self.fake_compare), \
                patch.object(C, 'verify_signature') as verify, \
                patch.object(C, 'live_profile', return_value={'state': 'ACTIVE'}) as provider:
            result = C.adopt(self.args)
        verify.assert_called_once()
        provider.assert_called_once()
        self.assertEqual(result['signingState'], 'signed-local')
        self.assertFalse(self.args.output.exists())

    def test_adoption_writes_existing_promotion_contract_and_never_overwrites(self):
        self.make_transfer(self.make_signed())
        self.args.write = True
        with patch.object(C, 'compare_macho', side_effect=self.fake_compare), \
                patch.object(C, 'verify_signature'), patch.object(C, 'live_profile', return_value={'state': 'ACTIVE'}):
            C.adopt(self.args)
            metadata = C.json_read(self.args.output / 'archive-manifest.json')
            self.assertEqual(metadata['workflowRun'], '456')
            self.assertEqual(metadata['archiveSha256'], C.sha256(self.args.output / C.SIGNED_ZIP))
            self.assertEqual(metadata['unsignedWorkflowRun'], '123')
            promoted = C.promotion_metadata(self.args.output, self.source, '456')
            self.assertEqual(promoted['signingState'], 'signed-local')
            with self.assertRaisesRegex(ValueError, 'Output already exists'):
                C.adopt(self.args)

    def test_promotion_rejects_missing_adoption_receipt_and_wrong_run(self):
        self.make_transfer(self.make_signed())
        self.args.write = True
        with patch.object(C, 'compare_macho', side_effect=self.fake_compare), \
                patch.object(C, 'verify_signature'), patch.object(C, 'live_profile', return_value={'state': 'ACTIVE'}):
            C.adopt(self.args)
        with self.assertRaisesRegex(ValueError, 'different run'):
            C.promotion_metadata(self.args.output, self.source, '789')
        (self.args.output / 'adoption-receipt.json').unlink()
        with self.assertRaises(OSError):
            C.promotion_metadata(self.args.output, self.source, '456')

    def test_promotion_rejects_receipt_tampering_and_unsigned_state(self):
        self.make_transfer(self.make_signed())
        self.args.write = True
        with patch.object(C, 'compare_macho', side_effect=self.fake_compare), \
                patch.object(C, 'verify_signature'), patch.object(C, 'live_profile', return_value={'state': 'ACTIVE'}):
            C.adopt(self.args)
        (self.args.output / C.RECEIPT).write_text('{}')
        with self.assertRaisesRegex(ValueError, 'Signing receipt checksum'):
            C.promotion_metadata(self.args.output, self.source, '456')
        path = self.args.output / 'archive-manifest.json'
        metadata = C.json_read(path)
        metadata['signingState'] = 'unsigned'
        path.write_text(json.dumps(metadata))
        with self.assertRaisesRegex(ValueError, 'cannot promote'):
            C.promotion_metadata(self.args.output, self.source, '456')

    def test_restore_preflight_checks_actual_app_signature_without_output(self):
        self.make_transfer(self.make_signed())
        self.args.write = True
        with patch.object(C, 'compare_macho', side_effect=self.fake_compare), \
                patch.object(C, 'verify_signature'), patch.object(C, 'live_profile', return_value={'state': 'ACTIVE'}):
            C.adopt(self.args)
        restore = argparse.Namespace(transfer_directory=self.args.output, source=self.source,
                                     archive_run_id='456', output=self.root / 'restore-output', write=False)
        with patch.object(C, 'verify_signature') as verify:
            C.restore_signed(restore)
        verify.assert_called_once()
        self.assertFalse(restore.output.exists())

    def test_adoption_does_not_write_after_signature_or_profile_failure(self):
        self.make_transfer(self.make_signed())
        self.args.write = True
        with patch.object(C, 'compare_macho', side_effect=self.fake_compare), \
                patch.object(C, 'verify_signature', side_effect=ValueError('bad signature')), \
                patch.object(C, 'live_profile') as provider:
            with self.assertRaisesRegex(ValueError, 'bad signature'):
                C.adopt(self.args)
        provider.assert_not_called()
        self.assertFalse(self.args.output.exists())

    def test_zip_traversal_symlinks_duplicates_and_case_collisions_rejected(self):
        cases = [('traversal', ['../escape']), ('absolute', ['/escape']),
                 ('collision', ['A', 'a']), ('canonical', ['a/./b']), ('duplicate', ['x', 'x'])]
        for label, names in cases:
            with self.subTest(label=label):
                path = self.root / (label + '.zip')
                with zipfile.ZipFile(path, 'x') as output:
                    for name in names:
                        output.writestr(name, b'data')
                with self.assertRaises(ValueError):
                    C.unpack(path, self.root / ('extract-' + label))
        path = self.root / 'symlink.zip'
        with zipfile.ZipFile(path, 'x') as output:
            info = zipfile.ZipInfo('link')
            info.external_attr = (stat.S_IFLNK | 0o777) << 16
            output.writestr(info, '../escape')
        with self.assertRaisesRegex(ValueError, 'symlink'):
            C.unpack(path, self.root / 'extract-symlink')

    def test_nonreleased_build_stamp_and_missing_game_bridge_rejected(self):
        self.info['BuildMachineOSBuild'] = '26A5307f'
        (self.app / 'Info.plist').write_bytes(plistlib.dumps(self.info))
        with self.assertRaisesRegex(ValueError, 'Unreleased'):
            C.verify_app(self.archive, self.metadata)
        self.info['BuildMachineOSBuild'] = '25G83'
        (self.app / 'Info.plist').write_bytes(plistlib.dumps(self.info))
        with patch.object(C, 'run', return_value=b'_OO_Initialize'), self.assertRaisesRegex(ValueError, 'Missing bridge'):
            C.verify_app(self.archive, self.metadata)

    def provider_fixture(self):
        return {'data': [{'id': 'profile-id', 'attributes': {'profileState': 'ACTIVE',
                'profileType': 'IOS_APP_DEVELOPMENT', 'uuid': self.profile['UUID'],
                'profileContent': base64.b64encode(self.profile_path.read_bytes()).decode()},
                'relationships': {'certificates': {'data': [{'type': 'certificates', 'id': 'certificate-id'}]},
                                  'devices': {'data': [{'type': 'devices', 'id': 'device-id'}]}}}],
                'included': [{'type': 'certificates', 'id': 'certificate-id',
                              'attributes': {'certificateContent': base64.b64encode(self.cert).decode(),
                                             'certificateType': 'DEVELOPMENT',
                                             'expirationDate': (datetime.now(timezone.utc) + timedelta(days=2)).isoformat()}},
                             {'type': 'devices', 'id': 'device-id',
                              'attributes': {'udid': self.device, 'status': 'ENABLED'}}]}

    def test_profile_readback_authenticates_with_get_and_binds_exact_profile(self):
        response = io.StringIO(json.dumps(self.provider_fixture()))
        with patch.dict(C.os.environ, {'ASC_KEY_ID': 'FIXTUREKEY', 'ASC_ISSUER_ID': 'fixture-issuer'}), \
                patch.object(C.subprocess, 'run', return_value=SimpleNamespace(returncode=0,
                              stdout=bytes.fromhex('3006020101020102'))) as openssl, \
                patch.object(C.urllib.request, 'urlopen', return_value=response) as api:
            report = C.live_profile(self.profile_path, self.profile,
                                    self.args.signer_sha256, self.args.device_sha256)
        self.assertEqual(report['state'], 'ACTIVE')
        request = api.call_args.args[0]
        self.assertEqual(request.method, 'GET')
        self.assertIsNone(request.data)
        self.assertEqual(C.urllib.parse.parse_qs(C.urllib.parse.urlparse(request.full_url).query)['filter[name]'],
                         [self.profile['Name']])
        self.assertEqual(openssl.call_args.args[0][:3], ['openssl', 'dgst', '-sha256'])

    def test_profile_readback_rejects_inactive_changed_profile_and_disabled_device(self):
        for change in ('inactive', 'content', 'certificate', 'expired-certificate', 'device'):
            with self.subTest(change=change):
                response = self.provider_fixture()
                if change == 'inactive':
                    response['data'][0]['attributes']['profileState'] = 'INVALID'
                elif change == 'content':
                    response['data'][0]['attributes']['profileContent'] = base64.b64encode(b'other CMS').decode()
                elif change == 'certificate':
                    response['included'][0]['attributes']['certificateContent'] = base64.b64encode(b'other cert').decode()
                elif change == 'expired-certificate':
                    response['included'][0]['attributes']['expirationDate'] = '2020-01-01T00:00:00Z'
                else:
                    response['included'][1]['attributes']['status'] = 'DISABLED'
                with patch.dict(C.os.environ, {'ASC_KEY_ID': 'FIXTUREKEY', 'ASC_ISSUER_ID': 'fixture-issuer'}), \
                        patch.object(C.subprocess, 'run', return_value=SimpleNamespace(returncode=0,
                                      stdout=bytes.fromhex('3006020101020102'))), \
                        patch.object(C.urllib.request, 'urlopen', return_value=io.StringIO(json.dumps(response))), \
                        self.assertRaises(ValueError):
                    C.live_profile(self.profile_path, self.profile,
                                   self.args.signer_sha256, self.args.device_sha256)


if __name__ == '__main__':
    unittest.main()
