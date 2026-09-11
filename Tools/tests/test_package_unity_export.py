"""Transfer validation tests using synthetic ZIP fixtures, not Unity build evidence."""
import contextlib
import hashlib
import importlib.util
import io
import json
import platform
import plistlib
import stat
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('package_unity_export', Path(__file__).resolve().parents[1] / 'package_unity_export.py')
PACKAGE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PACKAGE)


class ExportValidationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = 'a' * 40
        self.tree = 'b' * 40
        self.report = self.root / 'tests.xml'
        self.report.write_text('<test-run result="Passed" total="3" passed="3" failed="0" skipped="0">'
            '<test-case fullname="LittleLifeline.Core.Tests.LifelineSimulationTests.Example" result="Passed"/>'
            '<test-case fullname="LittleLifeline.Tests.LifelineProfileTests.Example" result="Passed"/>'
            '<test-case fullname="LittleLifeline.Tests.LifelineWorldTests.Example" result="Passed"/>'
            '</test-run>')

    def archive(self, *, extra=None, change=None):
        contents = {name: ('fixture:' + name).encode() for name in PACKAGE.CRITICAL_FILES}
        contents['Info.plist'] = plistlib.dumps({'CFBundleDisplayName': 'Little Lifeline'})
        contents['orbit-orchard-unity-build.json'] = json.dumps({'product': PACKAGE.PRODUCT, 'scenePath': PACKAGE.SCENE}).encode()
        contents['orbit-orchard-unity-tests.xml'] = self.report.read_bytes()
        metadata = {
            'format': 1, 'product': PACKAGE.PRODUCT, 'scenePath': PACKAGE.SCENE, 'sourceCommit': self.source, 'unitySourceTree': self.tree,
            'bundleIdentifier': 'com.flutterly.gravitile', 'hostArchitecture': platform.machine(),
            'unityTests': PACKAGE.read_tests(self.report),
            'criticalFiles': {name: hashlib.sha256(contents[name]).hexdigest() for name in PACKAGE.CRITICAL_FILES},
        }
        if change:
            change(metadata, contents)
        archive = self.root / 'export.zip'
        with zipfile.ZipFile(archive, 'w') as package:
            for name, data in contents.items():
                package.writestr(name, data)
            package.writestr(PACKAGE.MANIFEST, json.dumps(metadata))
            if extra:
                package.writestr(*extra)
        return archive

    def unpack(self, archive, **overrides):
        with patch.object(PACKAGE, 'git', return_value=self.tree), contextlib.redirect_stdout(io.StringIO()):
            PACKAGE.unpack(archive, self.root / 'unpacked',
                           overrides.get('checksum', PACKAGE.sha256(archive)),
                           overrides.get('source', self.source))

    def test_valid_export_preserves_executable_mode(self):
        binary = zipfile.ZipInfo('tools/il2cpp')
        binary.create_system = 3
        binary.external_attr = (stat.S_IFREG | 0o755) << 16
        self.unpack(self.archive(extra=(binary, b'fixture tool')))
        self.assertEqual(stat.S_IMODE((self.root / 'unpacked/tools/il2cpp').stat().st_mode), 0o755)

    def test_wrong_checksum_rejected_before_extraction(self):
        with self.assertRaisesRegex(ValueError, 'SHA256 mismatch'):
            self.unpack(self.archive(), checksum='0' * 64)
        self.assertFalse((self.root / 'unpacked').exists())

    def test_wrong_source_rejected(self):
        with self.assertRaisesRegex(ValueError, 'source commit'):
            self.unpack(self.archive(), source='c' * 40)

    def test_path_traversal_rejected_before_any_extraction(self):
        with self.assertRaisesRegex(ValueError, 'Unsafe archive path'):
            self.unpack(self.archive(extra=('../escape', b'unsafe')))
        self.assertEqual(list((self.root / 'unpacked').iterdir()), [])

    def test_symlink_rejected(self):
        link = zipfile.ZipInfo('linked-source')
        link.create_system = 3
        link.external_attr = (stat.S_IFLNK | 0o777) << 16
        with self.assertRaisesRegex(ValueError, 'Unsafe archive entry'):
            self.unpack(self.archive(extra=(link, b'/outside')))

    def test_modified_native_source_rejected(self):
        def modify(metadata, contents):
            contents['Libraries/OrbitOrchardApple/OrchardApplePlugin.mm'] = b'changed'
        with self.assertRaisesRegex(ValueError, 'Critical native export file changed'):
            self.unpack(self.archive(change=modify))

    def test_modified_test_report_rejected(self):
        def modify(metadata, contents):
            contents['orbit-orchard-unity-tests.xml'] = contents['orbit-orchard-unity-tests.xml'].replace(b'total="3"', b'total="4"')
        with self.assertRaisesRegex(ValueError, 'test report does not match'):
            self.unpack(self.archive(change=modify))

    def test_zero_executed_tests_rejected(self):
        self.report.write_text('<test-run result="Passed" total="0" passed="0" failed="0"/>')
        with self.assertRaisesRegex(ValueError, 'executed passing tests'):
            PACKAGE.read_tests(self.report)

    def test_previous_game_export_rejected(self):
        def modify(metadata, contents):
            metadata['product'] = 'orbit-orchard'
        with self.assertRaisesRegex(ValueError, 'Little Lifeline product'):
            self.unpack(self.archive(change=modify))

    def test_wrong_game_display_name_rejected(self):
        with self.assertRaisesRegex(ValueError, 'display name'):
            PACKAGE.validate_product({'CFBundleDisplayName': 'Orbit Orchard'},
                                     {'product': PACKAGE.PRODUCT, 'scenePath': PACKAGE.SCENE})

    def test_wrong_scene_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Little Lifeline scene'):
            PACKAGE.validate_product({'CFBundleDisplayName': 'Little Lifeline'},
                                     {'product': PACKAGE.PRODUCT, 'scenePath': 'Assets/OldGame.unity'})

    def test_previous_game_tests_are_insufficient(self):
        self.report.write_text('<test-run result="Passed" total="3" passed="3" failed="0"/>')
        with self.assertRaisesRegex(ValueError, 'Little Lifeline tests'):
            PACKAGE.read_tests(self.report)

    def test_existing_package_sidecars_are_preserved(self):
        output = self.root / 'candidate.zip'
        for artifact in (output, output.with_suffix('.zip.sha256'), output.with_suffix('.manifest.json')):
            with self.subTest(artifact=artifact.name):
                artifact.write_text('existing candidate evidence')
                with self.assertRaisesRegex(ValueError, 'already exists'):
                    PACKAGE.ensure_new_output(output)
                self.assertEqual(artifact.read_text(), 'existing candidate evidence')
                artifact.unlink()
        output.symlink_to(self.root / 'not-present')
        with self.assertRaisesRegex(ValueError, 'already exists'):
            PACKAGE.ensure_new_output(output)

    def test_signing_material_cannot_be_packaged(self):
        (self.root / 'AuthKey_example.p8').write_text('fixture signing material')
        with self.assertRaisesRegex(ValueError, 'Signing material'):
            PACKAGE.scan_export(self.root)


if __name__ == '__main__':
    unittest.main()
