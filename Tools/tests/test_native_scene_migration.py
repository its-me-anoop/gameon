"""Execute the production scene adapter with public-API doubles, without touching app data.

UIKit lifecycle ordering and the real restored iPad session still require app runtime QA.
"""
import pathlib
import subprocess
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Unity/OrbitOrchard/Assets/OrbitOrchard/Plugins/iOS/Native~/OrchardAppController.mm'
FIXTURE = pathlib.Path(__file__).with_name('native_scene_migration')


class NativeSceneMigrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory(prefix='clinic-scene-tests-')
        cls.addClassCleanup(cls.temp.cleanup)
        cls.binary = pathlib.Path(cls.temp.name) / 'scene-tests'
        result = subprocess.run(['xcrun', 'clang++', '-fobjc-arc', '-std=c++17', '-framework', 'Foundation',
                                 '-I', str(FIXTURE), str(SOURCE), str(FIXTURE / 'SceneMigrationTests.mm'),
                                 '-o', str(cls.binary)], capture_output=True, text=True)
        if result.returncode:
            raise AssertionError(result.stderr)

    def run_case(self, case):
        result = subprocess.run([str(self.binary), case], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.strip(), 'PASS ' + case)


for _case in ('configured-legacy', 'live-legacy', 'unresolved-legacy', 'current-unity',
              'other-delegate', 'already-overridden', 'other-role', 'nonwindow', 'unknown-missing',
              'foreground-fallback', 'lifecycle'):
    setattr(NativeSceneMigrationTests, 'test_' + _case.replace('-', '_'),
            lambda self, case=_case: self.run_case(case))

if __name__ == '__main__':
    unittest.main()
