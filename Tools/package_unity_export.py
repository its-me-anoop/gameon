#!/usr/bin/env python3
"""Inspect/package a self-contained Unity iOS export; no upload or provider writes.

A final package requires a clean Unity source tree and a passing Unity NUnit report.
The default operation is a read-only size and disk-space preflight. Use --write to zip.
"""
import argparse
import hashlib
import json
import os
import platform
import plistlib
import re
import shutil
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath

REPO = Path(__file__).resolve().parent.parent
MANIFEST = 'orbit-orchard-export.json'
PRODUCT = 'idle-clinic'
PRODUCT_NAME = 'Little Lifeline'
SCENE = 'Assets/IdleClinic/Scenes/Clinic.unity'
MAX_ASSET_BYTES = 2 * 1024 ** 3
CRITICAL_FILES = ['Unity-iPhone.xcodeproj/project.pbxproj', 'Info.plist', 'PrivacyInfo.xcprivacy',
                  'orbit-orchard-unity-build.json',
                  'Libraries/OrbitOrchardApple/OrchardApplePlugin.mm',
                  'Libraries/OrbitOrchardApple/OrchardAppleBridge.swift',
                  'Libraries/OrbitOrchardApple/OrchardStoreService.swift',
                  'Libraries/OrbitOrchardApple/OrchardGameCenterService.swift']


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def git(*args):
    return subprocess.check_output(['git', '-C', str(REPO), *args], text=True).strip()


def read_tests(path):
    result = ET.parse(path).getroot()
    if result.tag != 'test-run' or result.get('result', '').lower() != 'passed':
        raise ValueError('Unity NUnit test-run must have result=Passed.')
    total = int(result.get('total', '0'))
    failed = int(result.get('failed', '0'))
    passed = int(result.get('passed', '0'))
    if total == 0 or failed != 0 or passed == 0:
        raise ValueError('The Unity test report must contain executed passing tests and zero failures.')
    if int(result.get('skipped', '0')) != 0 or passed != total:
        raise ValueError('The final Unity report must pass every test with zero skipped or inconclusive cases.')
    fixtures = ('IdleClinic.Tests.ClinicSimulationTests.',
                'IdleClinic.Tests.ClinicProfileTests.', 'IdleClinic.Tests.ClinicWorldTests.',
                'IdleClinic.Tests.ClinicHUDTests.', 'IdleClinic.Tests.ClinicPerformanceTests.',
                'IdleClinic.Tests.ClinicExpansionTests.', 'IdleClinic.Tests.ClinicMigrationTests.',
                'IdleClinic.Tests.ClinicParkingFlowTests.', 'IdleClinic.Tests.ClinicWalkingTests.',
                'IdleClinic.Tests.ClinicArchitectureTests.', 'IdleClinic.Tests.ClinicParkingWorldTests.',
                'IdleClinic.Tests.ClinicPassingTests.')
    cases = list(result.iter('test-case'))
    if len(cases) != total or any(case.get('result', '').lower() != 'passed' for case in cases):
        raise ValueError('Every reported Unity case must be present and passed.')
    for fixture in fixtures:
        executed = [case for case in cases if case.get('fullname', '').startswith(fixture)]
        if not executed or any(case.get('result', '').lower() != 'passed' for case in executed):
            raise ValueError('Unity report must execute all included Idle Clinic tests: ' + fixture)
    return {'total': total, 'passed': passed, 'failed': failed,
            'skipped': int(result.get('skipped', '0')), 'sha256': sha256(path)}


def safe_relative(name):
    path = PurePosixPath(name)
    if path.is_absolute() or '..' in path.parts or '\\' in name or not path.parts:
        raise ValueError('Unsafe archive path: ' + name)
    return path


def scan_export(root):
    files = []
    for folder, directories, names in os.walk(root):
        for name in directories + names:
            path = Path(folder) / name
            if path.is_symlink():
                raise ValueError(f'Export contains a symlink; export with copied sources: {path.relative_to(root)}')
        for name in names:
            path = Path(folder) / name
            if path.suffix.lower() in {'.p8', '.p12', '.mobileprovision'} or name == '.env':
                raise ValueError(f'Signing material must not be packaged: {path.relative_to(root)}')
            if name == '.DS_Store' or path.relative_to(root).parts[0] == 'build':
                continue
            if path.relative_to(root).as_posix() in {MANIFEST, 'orbit-orchard-unity-tests.xml'}:
                raise ValueError('Export already contains package metadata; create a fresh Unity export.')
            files.append(path)
    return sorted(files)


def unpack(archive, destination, expected_sha, expected_source):
    if sha256(archive) != expected_sha:
        raise ValueError('Export archive SHA256 mismatch.')
    destination = Path(destination).resolve()
    if destination.exists() and any(destination.iterdir()):
        raise ValueError('Unpack destination must be empty.')
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as package:
        names = package.namelist()
        if len(names) != len(set(names)):
            raise ValueError('Duplicate archive entries are not supported.')
        if MANIFEST not in names:
            raise ValueError('Export provenance manifest is missing.')
        metadata = json.loads(package.read(MANIFEST))
        if metadata.get('product') != PRODUCT or metadata.get('scenePath') != SCENE:
            raise ValueError('Expected the Idle Clinic product and scene; legacy exports cannot ship.')
        if metadata.get('developmentBuild') is not False:
            raise ValueError('Development or unclassified exports cannot ship to TestFlight.')
        if metadata.get('iosSdk') != 'device':
            raise ValueError('Only an explicit Device SDK export can ship to TestFlight.')
        if metadata['sourceCommit'] != expected_source:
            raise ValueError('Export source commit does not match the requested commit.')
        if metadata['unitySourceTree'] != git('rev-parse', expected_source + ':Unity/OrbitOrchard'):
            raise ValueError('Unity source tree does not match the checked-out candidate.')
        if metadata['bundleIdentifier'] != 'com.flutterly.gravitile' or metadata['unityTests']['failed'] != 0:
            raise ValueError('Unexpected bundle identity or failed Unity tests.')
        if metadata['hostArchitecture'] != platform.machine():
            raise ValueError('Runner architecture does not match exported IL2CPP host tools.')
        if metadata.get('format') != 1 or set(metadata['criticalFiles']) != set(CRITICAL_FILES):
            raise ValueError('Export provenance format or critical file inventory is invalid.')
        total = sum(entry.file_size for entry in package.infolist())
        if shutil.disk_usage(destination).free < total + 4 * 1024 ** 3:
            raise ValueError('Runner needs the unpacked export size plus 4 GiB free for this extraction.')
        for entry in package.infolist():
            safe_relative(entry.filename)
            mode = entry.external_attr >> 16
            if stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR):
                raise ValueError('Unsafe archive entry: ' + entry.filename)
        for relative in metadata['criticalFiles']:
            safe_relative(relative)
        for entry in package.infolist():
            mode = entry.external_attr >> 16
            package.extract(entry, destination)
            if not entry.is_dir() and mode:
                os.chmod(destination / entry.filename, stat.S_IMODE(mode))
    if not (destination / 'Unity-iPhone.xcodeproj/project.pbxproj').is_file():
        raise ValueError('Expected Unity-iPhone Xcode project is missing.')
    for relative, expected in metadata['criticalFiles'].items():
        if sha256(destination / relative) != expected:
            raise ValueError('Critical native export file changed: ' + relative)
    with (destination / 'Info.plist').open('rb') as handle:
        info = plistlib.load(handle)
    provenance = json.loads((destination / 'orbit-orchard-unity-build.json').read_text())
    validate_product(info, provenance)
    validate_project_sdk(destination)
    if read_tests(destination / 'orbit-orchard-unity-tests.xml') != metadata['unityTests']:
        raise ValueError('The packaged Unity test report does not match the provenance manifest.')
    print(json.dumps(metadata, indent=2))


def validate_product(info, provenance):
    if info.get('CFBundleDisplayName') != PRODUCT_NAME:
        raise ValueError('Export display name must be Little Lifeline.')
    if provenance.get('product') != PRODUCT or provenance.get('scenePath') != SCENE:
        raise ValueError('Unity success provenance must identify the Idle Clinic scene.')
    if provenance.get('developmentBuild') is not False:
        raise ValueError('Development or unclassified exports cannot ship to TestFlight.')
    if provenance.get('iosSdk') != 'device':
        raise ValueError('Only an explicit Device SDK export can ship to TestFlight.')
    if provenance.get('buildSucceeded') is not True:
        raise ValueError('The Unity export did not complete successfully.')


def validate_project_sdk(root):
    project = (Path(root) / 'Unity-iPhone.xcodeproj/project.pbxproj').read_text()
    sdks = set(re.findall(r'\bSDKROOT\s*=\s*"?([A-Za-z0-9.]+)"?\s*;', project))
    if sdks != {'iphoneos'} or '--target-is-simulator' in project:
        raise ValueError('The generated Xcode project must target only the Device SDK (iphoneos).')


def ensure_new_output(output):
    output = Path(output)
    artifacts = (output, output.with_suffix(output.suffix + '.sha256'), output.with_suffix('.manifest.json'))
    existing = [path for path in artifacts if path.exists() or path.is_symlink()]
    if existing:
        raise ValueError('Output artifact already exists; choose a new package path instead of overwriting: '
                         + ', '.join(str(path) for path in existing))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('export', nargs='?', type=Path, default=REPO / 'Unity/OrbitOrchard/Builds/iOS')
    parser.add_argument('--output', type=Path, default=REPO / 'build/unity-transfer/orbit-orchard-ios.zip')
    parser.add_argument('--test-results', type=Path)
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--unpack', type=Path, help='Unpack a previously packaged zip instead.')
    parser.add_argument('--destination', type=Path)
    parser.add_argument('--expected-sha256')
    parser.add_argument('--expected-source')
    args = parser.parse_args()
    if args.unpack:
        if not all([args.destination, args.expected_sha256, args.expected_source]):
            parser.error('--unpack needs --destination, --expected-sha256 and --expected-source')
        unpack(args.unpack, args.destination, args.expected_sha256, args.expected_source)
        return
    root = args.export.resolve()
    if not (root / 'Unity-iPhone.xcodeproj/project.pbxproj').is_file():
        raise ValueError('No Unity iOS export exists at ' + str(root))
    files = scan_export(root)
    size = sum(path.stat().st_size for path in files)
    output = args.output.resolve()
    parent = output.parent
    while not parent.exists():
        parent = parent.parent
    free = shutil.disk_usage(parent).free
    print(json.dumps({'exportBytes': size, 'fileCount': len(files), 'freeBytes': free,
                      'conservativeExtraSpaceRequiredBytes': size + 512 * 1024 ** 2,
                      'output': str(output)}, indent=2))
    if not args.write:
        print('Read-only preflight. No files written.')
        return
    if not args.test_results:
        parser.error('--write requires --test-results from the final Unity source candidate')
    ensure_new_output(args.output)
    ensure_new_output(output)
    if free < size + 512 * 1024 ** 2:
        raise ValueError('Insufficient free space for a conservative ZIP allocation.')
    paths = ['Unity/OrbitOrchard', 'Tools/package_unity_export.py', 'Tools/select_released_xcode.sh',
             'Tools/tests/test_package_unity_export.py', '.github/workflows/release.yml']
    if git('status', '--porcelain', '--untracked-files=all', '--', *paths):
        raise ValueError('Commit the complete Unity source, metadata and release tools before final export/package.')
    tests = read_tests(args.test_results)
    with (root / 'Info.plist').open('rb') as handle:
        info = plistlib.load(handle)
    version_file = REPO / 'Unity/OrbitOrchard/ProjectSettings/ProjectVersion.txt'
    version = next(line.split(':', 1)[1].strip() for line in version_file.read_text().splitlines() if line.startswith('m_EditorVersion:'))
    provenance = json.loads((root / 'orbit-orchard-unity-build.json').read_text())
    validate_product(info, provenance)
    validate_project_sdk(root)
    if provenance.get('sourceCommit') != git('rev-parse', 'HEAD') or provenance.get('sourceDirty') is not False:
        raise ValueError('Export came from a dirty or different source commit. Re-export the committed candidate.')
    if provenance.get('buildSucceeded') is not True or provenance.get('unityVersion') != version:
        raise ValueError('Unity export success/version provenance is invalid.')
    metadata = {'format': 1, 'product': PRODUCT, 'scenePath': SCENE, 'developmentBuild': False, 'iosSdk': 'device', 'sourceCommit': git('rev-parse', 'HEAD'),
                'unitySourceTree': git('rev-parse', 'HEAD:Unity/OrbitOrchard'),
                'unityVersion': version, 'hostArchitecture': platform.machine(),
                'bundleIdentifier': 'com.flutterly.gravitile',
                'marketingVersion': info.get('CFBundleShortVersionString'),
                'buildNumber': str(info.get('CFBundleVersion')),
                'createdUTC': datetime.now(timezone.utc).isoformat(),
                'unpackedBytes': size, 'unityTests': tests,
                'criticalFiles': {relative: sha256(root / relative) for relative in CRITICAL_FILES}}
    if info.get('CFBundleIdentifier') not in ('com.flutterly.gravitile', '$(PRODUCT_BUNDLE_IDENTIFIER)'):
        raise ValueError('Unity export bundle identifier does not match Gravitile.')
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as package:
        for path in files:
            package.write(path, path.relative_to(root).as_posix())
        package.writestr(MANIFEST, json.dumps(metadata, indent=2) + '\n')
        package.write(args.test_results, 'orbit-orchard-unity-tests.xml')
    digest = sha256(output)
    output.with_suffix(output.suffix + '.sha256').write_text(digest + '  ' + output.name + '\n')
    output.with_suffix('.manifest.json').write_text(json.dumps(metadata, indent=2) + '\n')
    print(json.dumps({'package': str(output), 'bytes': output.stat().st_size, 'sha256': digest,
                      'sourceCommit': metadata['sourceCommit']}, indent=2))
    if output.stat().st_size >= MAX_ASSET_BYTES:
        raise ValueError('ZIP exceeds GitHub single-release-asset limit; use a smaller copied-source export.')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, ET.ParseError, zipfile.BadZipFile) as error:
        print('Unity export package failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
