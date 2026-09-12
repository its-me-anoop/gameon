#!/usr/bin/env python3
"""Seal, locally sign, and adopt the same compiled Clinic archive.

Commands default to preflight. --write is required for persistent output/signing.
No command creates certificates, exports private keys, or writes to a provider.
"""
import argparse
import base64
import ctypes
import hashlib
import json
import os
import plistlib
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import time
import urllib.parse
import urllib.request
import zipfile
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath

from clinic_macho import compare_macho, inspect_macho, is_macho

TEAM = 'K6623R3GP5'
BUNDLE = 'com.flutterly.gravitile'
ARCHIVE = 'OrbitOrchard.xcarchive'
UNSIGNED_ZIP = 'OrbitOrchard.unsigned.xcarchive.zip'
SIGNED_ZIP = 'OrbitOrchard.xcarchive.zip'
RECEIPT = 'signing-receipt.json'
TRANSFER = 'clinic-signed-transfer.zip'
BRIDGE = ('Initialize', 'LoadProducts', 'Purchase', 'RestorePurchases',
          'AuthenticateGameCenter', 'ShowLeaderboard', 'SubmitScore', 'RetryScores',
          'Haptic', 'UseLifelineLeaderboards', 'ShowWeeklyLeaderboard',
          'ScreenWidthPoints', 'ThermalState')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha256(path):
    with Path(path).open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def digest_bytes(data):
    return hashlib.sha256(data).hexdigest()


def json_read(path):
    return json.loads(Path(path).read_text())


def json_write(path, value):
    with Path(path).open('x') as handle:
        json.dump(value, handle, indent=2, sort_keys=True)
        handle.write('\n')


def run(*args):
    result = subprocess.run([str(arg) for arg in args], capture_output=True, check=False)
    # Do not relay tool diagnostics that might contain credential values.
    require(result.returncode == 0, f'{Path(str(args[0])).name} failed (exit {result.returncode}).')
    return result.stdout


def files(root):
    root = Path(root)
    output = {}
    for path in sorted(root.rglob('*')):
        require(not path.is_symlink(), 'Symlinks are outside the Clinic archive contract.')
        require(path.is_file() or path.is_dir(), 'Special archive file is unsupported.')
        if path.is_file():
            output[path.relative_to(root).as_posix()] = path
    return output


def inventory(root):
    return {name: {'sha256': sha256(path), 'mode': stat.S_IMODE(path.stat().st_mode)}
            for name, path in files(root).items()}


def unpack(package_path, destination, root_name=None, exact_files=None):
    """Validate every path and type before extracting a private ZIP."""
    destination = Path(destination)
    with zipfile.ZipFile(package_path) as package:
        entries = package.infolist()
        names = [entry.filename for entry in entries]
        require(len(names) == len(set(names)), 'Duplicate ZIP entries.')
        canonical = set()
        for entry in entries:
            name = entry.filename
            path = PurePosixPath(name)
            require(name and not path.is_absolute() and '..' not in path.parts
                    and '\\' not in name and '\x00' not in name
                    and path.as_posix() == name.rstrip('/'), 'Unsafe ZIP path.')
            require(path.as_posix().casefold() not in canonical, 'Colliding ZIP paths.')
            canonical.add(path.as_posix().casefold())
            require(stat.S_IFMT(entry.external_attr >> 16) in (0, stat.S_IFREG, stat.S_IFDIR),
                    'ZIP symlink or special file is unsupported.')
            if root_name:
                require(path.parts[0] == root_name, 'Unexpected ZIP root.')
        if exact_files is not None:
            require(set(names) == set(exact_files), 'Unexpected transfer contents.')
        total = sum(entry.file_size for entry in entries)
        require(total < 12 * 1024 ** 3, 'Archive exceeds the bounded extraction size.')
        require(shutil.disk_usage(destination.parent).free > total + 256 * 1024 ** 2,
                'Insufficient extraction space.')
        destination.mkdir(exist_ok=False)
        for entry in entries:
            package.extract(entry, destination)
            if not entry.is_dir():
                mode = stat.S_IMODE(entry.external_attr >> 16) or 0o644
                require(not mode & 0o7000, 'Special permission bits are unsupported.')
                (destination / entry.filename).chmod(mode)
    return destination / root_name if root_name else destination


def zip_tree(root, destination):
    with zipfile.ZipFile(destination, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as package:
        for name, path in files(root).items():
            package.write(path, f'{Path(root).name}/{name}')


def verify_metadata(metadata, source, export_sha):
    require(re.fullmatch(r'[0-9a-f]{40}', source) is not None, 'Full source SHA required.')
    require(re.fullmatch(r'[0-9a-f]{64}', export_sha) is not None, 'Export SHA256 required.')
    expected = {'sourceCommit': source, 'product': 'idle-clinic', 'developmentBuild': False,
                'iosSdk': 'device', 'scenePath': 'Assets/IdleClinic/Scenes/Clinic.unity',
                'bundleIdentifier': BUNDLE}
    for key, value in expected.items():
        require(metadata.get(key) == value, f'Wrong archive metadata: {key}.')
    if 'exportSha256' in metadata:
        require(metadata['exportSha256'] == export_sha, 'Export provenance mismatch.')
    require(metadata.get('format') == 1, 'Unknown export manifest format.')
    tests = metadata.get('unityTests', {})
    require(tests.get('failed') == 0 and tests.get('skipped') == 0
            and tests.get('passed', 0) > 0 and tests.get('passed') == tests.get('total'),
            'Final Unity tests must all pass.')


def verify_app(archive, metadata):
    archive = Path(archive)
    apps = list((archive / 'Products/Applications').glob('*.app'))
    require(len(apps) == 1, 'Exactly one app is required.')
    app = apps[0]
    info = plistlib.loads((app / 'Info.plist').read_bytes())
    for key, value in {'CFBundleIdentifier': BUNDLE, 'CFBundleDisplayName': 'Little Lifeline',
                       'CFBundleShortVersionString': metadata['marketingVersion'],
                       'DTPlatformName': 'iphoneos'}.items():
        require(info.get(key) == value, f'Wrong app identity: {key}.')
    require(str(info.get('CFBundleVersion')) == metadata['buildNumber'], 'Build drift.')
    for key in ('BuildMachineOSBuild', 'DTXcodeBuild'):
        require(isinstance(info.get(key), str) and re.fullmatch(r'[0-9]+[A-Z][0-9]+', info[key]),
                f'Unreleased or missing build stamp: {key}.')
    framework = app / 'Frameworks/UnityFramework.framework/UnityFramework'
    require(framework.is_file(), 'Unity player is missing.')
    symbols = run('xcrun', 'nm', '-gU', framework).decode()
    for symbol in BRIDGE:
        require('_OO_' + symbol in symbols, 'Missing bridge export: ' + symbol)
    privacy = plistlib.loads((app / 'PrivacyInfo.xcprivacy').read_bytes())
    entries = privacy.get('NSPrivacyAccessedAPITypes', [])
    for category, reason in (('UserDefaults', 'CA92.1'), ('FileTimestamp', 'C617.1')):
        require(any(entry.get('NSPrivacyAccessedAPIType') == 'NSPrivacyAccessedAPICategory' + category
                    and reason in entry.get('NSPrivacyAccessedAPITypeReasons', []) for entry in entries),
                'Missing privacy reason: ' + category)
    require(not list(app.rglob('*.appex')), 'App extensions require a separate signing design.')
    outer = plistlib.loads((archive / 'Info.plist').read_bytes())
    properties = outer.get('ApplicationProperties', {})
    require(outer.get('ArchiveVersion') == 2, 'Unknown Xcode archive format.')
    require(properties.get('ApplicationPath') == app.relative_to(archive / 'Products').as_posix(),
            'Archive application path mismatch.')
    for key in ('CFBundleIdentifier', 'CFBundleShortVersionString', 'CFBundleVersion'):
        require(str(properties.get(key)) == str(info.get(key)), 'Archive app metadata mismatch: ' + key)
    require(properties.get('Architectures') == ['arm64'], 'Expected only arm64.')
    return app, info


def check_unsigned(args, destination, *, pinned=True):
    if pinned:
        require(sha256(args.unsigned) == args.unsigned_sha256, 'Pinned unsigned ZIP mismatch.')
        require(sha256(args.manifest) == args.manifest_sha256, 'Pinned unsigned manifest mismatch.')
    metadata = json_read(args.manifest)
    verify_metadata(metadata, args.source, args.export_sha256)
    require(metadata.get('signingState') == 'unsigned', 'Only unsigned source artifacts are accepted.')
    require(metadata.get('workflowRun') == str(args.unsigned_run_id), 'Unsigned run mismatch.')
    require(metadata.get('unsignedArchiveSha256') == sha256(args.unsigned), 'Unsigned checksum mismatch.')
    archive = unpack(args.unsigned, destination, ARCHIVE)
    require(inventory(archive) == metadata.get('inventory'), 'Unsigned inventory mismatch.')
    app, info = verify_app(archive, metadata)
    require(not list(app.rglob('embedded.mobileprovision')), 'Unsigned source contains provisioning.')
    require(info['BuildMachineOSBuild'] == metadata.get('buildMachineOS')
            and info['DTXcodeBuild'] == metadata.get('xcodeBuild'), 'Unsigned build stamps mismatch.')
    return archive, metadata, app


def read_profile(path, certificate_sha256, device_sha256):
    require(re.fullmatch(r'[0-9a-f]{64}', certificate_sha256) is not None, 'Signer SHA256 required.')
    require(re.fullmatch(r'[0-9a-f]{64}', device_sha256) is not None, 'Device SHA256 required.')
    profile = plistlib.loads(run('security', 'cms', '-D', '-i', path))
    expires = profile.get('ExpirationDate')
    require(isinstance(expires, datetime)
            and expires.replace(tzinfo=timezone.utc) > datetime.now(timezone.utc), 'Expired profile.')
    require(profile.get('TeamIdentifier') == [TEAM], 'Wrong profile team.')
    require(certificate_sha256 in [digest_bytes(cert) for cert in profile.get('DeveloperCertificates', [])],
            'Profile does not authorize the pinned signer.')
    require(device_sha256 in [digest_bytes(device.encode()) for device in profile.get('ProvisionedDevices', [])],
            'Profile does not authorize the pinned physical device.')
    ent = profile.get('Entitlements', {})
    require(ent.get('application-identifier') == TEAM + '.' + BUNDLE
            and ent.get('com.apple.developer.team-identifier') == TEAM
            and ent.get('get-task-allow') is True
            and ent.get('com.apple.developer.game-center') is True, 'Wrong development entitlements.')
    groups = ent.get('keychain-access-groups', [])
    require(TEAM + '.*' in groups or TEAM + '.' + BUNDLE in groups, 'Wrong keychain group authorization.')
    require(not profile.get('ProvisionsAllDevices', False), 'Expected a registered-device profile.')
    return profile


def app_entitlements():
    return {'application-identifier': TEAM + '.' + BUNDLE,
            'com.apple.developer.team-identifier': TEAM, 'get-task-allow': True,
            'com.apple.developer.game-center': True, 'keychain-access-groups': [TEAM + '.' + BUNDLE]}


def code_paths(app):
    paths = []
    for path in files(app).values():
        with path.open('rb') as handle:
            if is_macho(handle.read(4)):
                paths.append(path)
    executable = app / plistlib.loads((app / 'Info.plist').read_bytes())['CFBundleExecutable']
    require(executable in paths, 'Main Mach-O executable is missing.')
    return paths, executable


def file_is_macho(path):
    with Path(path).open('rb') as handle:
        return is_macho(handle.read(4))


def verify_signature(app, signer_sha256):
    run('codesign', '--verify', '--deep', '--strict', app)
    ent = plistlib.loads(run('codesign', '-d', '--entitlements', ':-', app))
    require(ent == app_entitlements(), 'Signed app entitlements differ from the bounded contract.')
    paths, main = code_paths(app)
    with tempfile.TemporaryDirectory(prefix='clinic-public-cert-') as temporary:
        for index, path in enumerate(paths):
            run('codesign', '--verify', '--strict', path)
            prefix = Path(temporary) / f'certificate-{index}-'
            run('codesign', '-d', f'--extract-certificates={prefix}', path)
            require(sha256(str(prefix) + '0') == signer_sha256, 'Nested code uses a different signer.')
            if path != main:
                raw = run('codesign', '-d', '--entitlements', ':-', path).strip()
                require(not raw or plistlib.loads(raw) == {}, 'Nested code has unexpected entitlements.')


def compare_archives(unsigned, signed):
    """Bind the signed artifact to all original bytes, allowing only signing changes."""
    before, after = files(unsigned), files(signed)
    app_names = [name for name in before if name.startswith('Products/Applications/')
                 and name.count('/') == 3 and name.endswith('.app/Info.plist')]
    require(len(app_names) == 1, 'Cannot identify a unique source app.')
    app_prefix = app_names[0].removesuffix('Info.plist')
    allowed_added = {app_prefix + 'embedded.mobileprovision'}
    bundle_prefixes = {app_prefix}
    for name in before:
        if name.startswith(app_prefix) and name.endswith('.framework/Info.plist'):
            bundle_prefixes.add(name.removesuffix('Info.plist'))
    signature_files = {prefix + '_CodeSignature/CodeResources' for prefix in bundle_prefixes}
    require(set(before) <= set(after), 'A source resource was removed.')
    require(set(after) - set(before) <= allowed_added | signature_files, 'Unexpected added archive resource.')
    identities = {}
    for name, path in before.items():
        target = after[name]
        require(stat.S_IMODE(path.stat().st_mode) == stat.S_IMODE(target.stat().st_mode),
                'Resource permissions changed: ' + name)
        if name == 'Info.plist':
            a, b = plistlib.loads(path.read_bytes()), plistlib.loads(target.read_bytes())
            for value in (a, b):
                props = value.get('ApplicationProperties', {})
                for key in ('SigningIdentity', 'Team'):
                    props.pop(key, None)
            require(a == b, 'Archive metadata changed beyond signing identity/team.')
        elif name in signature_files:
            continue
        elif name.startswith(app_prefix) and file_is_macho(path):
            identities[name] = compare_macho(path.read_bytes(), target.read_bytes())
        else:
            require(sha256(path) == sha256(target), 'Resource/build stamp/dSYM changed: ' + name)
    require(identities, 'No compiled executable identities were verified.')
    return identities


def live_profile(profile_path, profile, signer_sha256, device_sha256):
    """Authenticated GET only; bind embedded CMS bytes to Apple's active profile."""
    key_id, issuer = os.environ.get('ASC_KEY_ID'), os.environ.get('ASC_ISSUER_ID')
    require(key_id and issuer, 'ASC_KEY_ID and ASC_ISSUER_ID are required for adoption profile readback.')
    key = Path.home() / '.appstoreconnect/private_keys' / f'AuthKey_{key_id}.p8'
    encode = lambda value: base64.urlsafe_b64encode(value).decode().rstrip('=')
    now = int(time.time())
    message = (encode(json.dumps({'alg': 'ES256', 'kid': key_id, 'typ': 'JWT'}).encode()) + '.'
               + encode(json.dumps({'iss': issuer, 'iat': now, 'exp': now + 300,
                                    'aud': 'appstoreconnect-v1'}).encode())).encode()
    signed = subprocess.run(['openssl', 'dgst', '-sha256', '-sign', str(key)],
                            input=message, capture_output=True, check=False)
    require(signed.returncode == 0, 'Could not authenticate read-only profile verification.')
    der = signed.stdout
    require(len(der) >= 8 and der[0] == 0x30 and der[1] == len(der) - 2 and der[2] == 2,
            'Unexpected ECDSA signature encoding.')
    first_len = der[3]
    second = 4 + first_len
    require(second + 2 < len(der) and der[second] == 2
            and second + 2 + der[second + 1] == len(der), 'Malformed ECDSA signature.')
    r = int.from_bytes(der[4:second], 'big').to_bytes(32, 'big')
    s = int.from_bytes(der[second + 2:], 'big').to_bytes(32, 'big')
    token = message.decode() + '.' + encode(r + s)
    query = urllib.parse.urlencode({'filter[name]': profile['Name'], 'include': 'certificates,devices'})
    request = urllib.request.Request('https://api.appstoreconnect.apple.com/v1/profiles?' + query,
                                     headers={'Authorization': 'Bearer ' + token}, method='GET')
    with urllib.request.urlopen(request, timeout=60) as response:
        result = json.load(response)
    candidates = result.get('data', [])
    require(len(candidates) == 1, 'Active profile readback did not identify one profile.')
    item = candidates[0]
    attrs = item.get('attributes', {})
    require(attrs.get('profileState') == 'ACTIVE' and attrs.get('profileType') == 'IOS_APP_DEVELOPMENT'
            and attrs.get('uuid') == profile['UUID'], 'Profile is not active development provisioning.')
    require(base64.b64decode(attrs.get('profileContent', ''), validate=True) == Path(profile_path).read_bytes(),
            'Embedded profile differs from the active Apple profile.')
    included = {(value['type'], value['id']): value['attributes'] for value in result.get('included', [])}
    certs = item.get('relationships', {}).get('certificates', {}).get('data', [])
    matching = [included[(c['type'], c['id'])] for c in certs
                if digest_bytes(base64.b64decode(included[(c['type'], c['id'])]['certificateContent'])) == signer_sha256]
    require(len(matching) == 1 and matching[0].get('certificateType') == 'DEVELOPMENT'
            and datetime.fromisoformat(matching[0]['expirationDate'].replace('Z', '+00:00')) > datetime.now(timezone.utc),
            'Pinned certificate is not active development signing on the Apple profile.')
    devices = item.get('relationships', {}).get('devices', {}).get('data', [])
    require(any(included[(d['type'], d['id'])].get('status') == 'ENABLED'
                and digest_bytes(included[(d['type'], d['id'])]['udid'].encode()) == device_sha256
                for d in devices), 'Pinned test device is not enabled on the Apple profile.')
    return {'profileId': item['id'], 'profileUUID': profile['UUID'], 'state': 'ACTIVE',
            'checkedAt': datetime.now(timezone.utc).isoformat()}


def output_directory(path):
    require(not path.exists() and not path.is_symlink(), 'Output already exists; choose a new path.')
    path.mkdir(parents=True, exist_ok=False)


def clone_copyfile(source, destination):
    """Clone one regular file on macOS; never fall back to copying its data."""
    require(sys.platform == 'darwin', '--clone-copies requires macOS clonefile support.')
    source, destination = Path(source), Path(destination)
    require(stat.S_ISREG(source.lstat().st_mode), 'Clone source must be a regular file.')
    require(not destination.exists() and not destination.is_symlink(), 'Clone destination already exists.')
    require(source.stat().st_dev == destination.parent.stat().st_dev,
            'Clone source and destination must be on the same volume.')
    clone = ctypes.CDLL('/usr/lib/libSystem.B.dylib', use_errno=True).clonefile
    clone.argtypes = (ctypes.c_char_p, ctypes.c_char_p, ctypes.c_uint32)
    clone.restype = ctypes.c_int
    # CLONE_NOFOLLOW | CLONE_ACL: preserve source attributes/ACLs without following a file symlink.
    if clone(os.fsencode(source), os.fsencode(destination), 0x0001 | 0x0004) != 0:
        error = ctypes.get_errno()
        raise OSError(error, 'clonefile failed; no ordinary-copy fallback: ' + os.strerror(error))
    shutil.copystat(source, destination, follow_symlinks=False)
    return str(destination)


def preflight_clone_copies(temporary, output):
    """Check the output volume and a tiny temporary clone without creating output."""
    ancestor = output.parent
    while not ancestor.exists():
        require(not ancestor.is_symlink(), 'Broken output ancestor symlink.')
        ancestor = ancestor.parent
    require(ancestor.is_dir() and ancestor.stat().st_dev == temporary.stat().st_dev,
            'Clone source and output must be on the same volume; set TMPDIR accordingly.')
    probe = temporary / 'clone-probe'
    probe.mkdir()
    source, destination = probe / 'source', probe / 'destination'
    original = b'Clinic clone isolation probe\n'
    source.write_bytes(original)
    clone_copyfile(source, destination)
    require(destination.read_bytes() == original and source.stat().st_ino != destination.stat().st_ino,
            'Clone probe did not produce an independent file.')
    destination.write_bytes(b'changed clone\n')
    require(source.read_bytes() == original, 'Clone probe changed its source.')


def seal_unsigned(args):
    metadata = json_read(args.export_manifest)
    verify_metadata(metadata, args.source, args.export_sha256)
    app, info = verify_app(args.archive, metadata)
    require(not list(app.rglob('embedded.mobileprovision')), 'Unsigned archive contains provisioning.')
    report = {'sourceCommit': args.source, 'workflowRun': str(args.workflow_run),
              'signingState': 'unsigned', 'buildMachineOS': info['BuildMachineOSBuild'],
              'xcodeBuild': info['DTXcodeBuild'], 'exportSha256': args.export_sha256,
              'inventory': inventory(args.archive)}
    if args.write:
        require(Path(args.archive).name == ARCHIVE, 'Unexpected archive directory name.')
        output_directory(args.output)
        package = args.output / UNSIGNED_ZIP
        zip_tree(args.archive, package)
        metadata.update(report, unsignedArchiveSha256=sha256(package))
        json_write(args.output / 'unsigned-archive-manifest.json', metadata)
        report = {'unsignedSha256': sha256(package),
                  'manifestSha256': sha256(args.output / 'unsigned-archive-manifest.json'),
                  'sourceCommit': args.source, 'workflowRun': str(args.workflow_run)}
    else:
        report.pop('inventory')
    return report


def sign_local(args):
    with tempfile.TemporaryDirectory(prefix='clinic-sign-preflight-') as temporary:
        clone_copies = getattr(args, 'clone_copies', False)
        if clone_copies:
            preflight_clone_copies(Path(temporary), args.output)
        source, metadata, app = check_unsigned(args, Path(temporary) / 'unsigned')
        profile = read_profile(args.profile, args.signer_sha256, args.device_sha256)
        identities = run('security', 'find-identity', '-v', '-p', 'codesigning').decode()
        matches = re.findall(r'\b([A-F0-9]{40}) "' + re.escape(args.identity) + r'"\s*$', identities, re.M)
        require(len(matches) == 1 and args.identity.startswith('Apple Development:'),
                'An unrevoked matching development keychain identity is required.')
        cert = next(cert for cert in profile['DeveloperCertificates'] if digest_bytes(cert) == args.signer_sha256)
        require(hashlib.sha1(cert).hexdigest().upper() == matches[0], 'Keychain identity/profile mismatch.')
        # Validate supported binary formats before touching output or asking Keychain to sign.
        paths, main = code_paths(app)
        for path in paths:
            inspect_macho(path.read_bytes())
        report = {'sourceCommit': args.source, 'unsignedArchiveSha256': sha256(args.unsigned),
                  'unsignedManifestSha256': sha256(args.manifest), 'unsignedWorkflowRun': str(args.unsigned_run_id),
                  'signingCertificateSha256': args.signer_sha256, 'deviceSha256': args.device_sha256,
                  'profileSha256': sha256(args.profile), 'profileUUID': profile['UUID'],
                  'signingIdentity': args.identity, 'codeObjectCount': len(paths)}
        if clone_copies:
            report['cloneCopies'] = True
        if not args.write:
            return report
        output_directory(args.output)
        signed = args.output / ARCHIVE
        if clone_copies:
            shutil.copytree(source, signed, copy_function=clone_copyfile)
        else:
            shutil.copytree(source, signed)
        signed_app = signed / app.relative_to(source)
        shutil.copyfile(args.profile, signed_app / 'embedded.mobileprovision')
        entitlements = Path(temporary) / 'entitlements.plist'
        entitlements.write_bytes(plistlib.dumps(app_entitlements()))
        signed_paths, signed_main = code_paths(signed_app)
        for path in sorted((p for p in signed_paths if p != signed_main), key=lambda p: len(p.parts), reverse=True):
            target = path.parent if path.parent.suffix == '.framework' else path
            run('codesign', '--force', '--sign', matches[0], '--generate-entitlement-der', target)
        run('codesign', '--force', '--sign', matches[0], '--entitlements', entitlements,
            '--generate-entitlement-der', signed_app)
        outer_path = signed / 'Info.plist'
        outer = plistlib.loads(outer_path.read_bytes())
        outer['ApplicationProperties'].update(SigningIdentity=args.identity, Team=TEAM)
        outer_path.write_bytes(plistlib.dumps(outer))
        verify_app(signed, metadata)
        verify_signature(signed_app, args.signer_sha256)
        if clone_copies:
            require(inventory(source) == metadata['inventory'], 'Unsigned snapshot changed during clone signing.')
        report['binaryIdentities'] = compare_archives(source, signed)
        report['format'] = 1
        report['signedAt'] = datetime.now(timezone.utc).isoformat()
        package = args.output / SIGNED_ZIP
        zip_tree(signed, package)
        report['archiveSha256'] = sha256(package)
        json_write(args.output / RECEIPT, report)
        with zipfile.ZipFile(args.output / TRANSFER, 'x', zipfile.ZIP_STORED) as transfer:
            transfer.write(package, SIGNED_ZIP)
            transfer.write(args.output / RECEIPT, RECEIPT)
        return {**report, 'transferSha256': sha256(args.output / TRANSFER)}


def adopt(args):
    require(sha256(args.transfer) == args.transfer_sha256, 'Pinned signed transfer mismatch.')
    with tempfile.TemporaryDirectory(prefix='clinic-adopt-') as temporary:
        temporary = Path(temporary)
        unsigned, metadata, _ = check_unsigned(args, temporary / 'unsigned', pinned=False)
        transfer = unpack(args.transfer, temporary / 'transfer', exact_files={SIGNED_ZIP, RECEIPT})
        receipt = json_read(transfer / RECEIPT)
        for key, expected in {'format': 1, 'sourceCommit': args.source,
                              'unsignedWorkflowRun': str(args.unsigned_run_id),
                              'unsignedArchiveSha256': sha256(args.unsigned),
                              'unsignedManifestSha256': sha256(args.manifest),
                              'signingCertificateSha256': args.signer_sha256,
                              'deviceSha256': args.device_sha256,
                              'archiveSha256': sha256(transfer / SIGNED_ZIP)}.items():
            require(receipt.get(key) == expected, 'Signing receipt mismatch: ' + key)
        signed = unpack(transfer / SIGNED_ZIP, temporary / 'signed', ARCHIVE)
        app, _ = verify_app(signed, metadata)
        profile_path = app / 'embedded.mobileprovision'
        profile = read_profile(profile_path, args.signer_sha256, args.device_sha256)
        require(sha256(profile_path) == receipt.get('profileSha256')
                and profile['UUID'] == receipt.get('profileUUID'), 'Signing profile receipt mismatch.')
        properties = plistlib.loads((signed / 'Info.plist').read_bytes())['ApplicationProperties']
        require(properties.get('Team') == TEAM and properties.get('SigningIdentity') == receipt.get('signingIdentity')
                and str(properties.get('SigningIdentity', '')).startswith('Apple Development:'),
                'Archive signing metadata mismatch.')
        binary_identities = compare_archives(unsigned, signed)
        require(binary_identities == receipt.get('binaryIdentities'), 'Binary identity receipt mismatch.')
        verify_signature(app, args.signer_sha256)
        provider = live_profile(profile_path, profile, args.signer_sha256, args.device_sha256)
        report = {'sourceCommit': args.source, 'archiveSha256': receipt['archiveSha256'],
                  'unsignedArchiveSha256': sha256(args.unsigned), 'unsignedWorkflowRun': str(args.unsigned_run_id),
                  'transferSha256': args.transfer_sha256, 'binaryIdentities': binary_identities,
                  'profileReadback': provider, 'signingState': 'signed-local',
                  'signingCertificateSha256': args.signer_sha256, 'deviceSha256': args.device_sha256,
                  'profileSha256': sha256(profile_path)}
        if args.write:
            output_directory(args.output)
            with (args.output / SIGNED_ZIP).open('xb') as output, (transfer / SIGNED_ZIP).open('rb') as incoming:
                shutil.copyfileobj(incoming, output)
            metadata.pop('inventory')
            json_write(args.output / 'adoption-receipt.json', report)
            metadata.update(report, workflowRun=str(args.workflow_run), signingReceiptSha256=sha256(transfer / RECEIPT),
                            adoptionReceiptSha256=sha256(args.output / 'adoption-receipt.json'))
            json_write(args.output / 'archive-manifest.json', metadata)
            with (args.output / RECEIPT).open('xb') as output:
                output.write((transfer / RECEIPT).read_bytes())
        return report


def promotion_metadata(directory, source, run_id):
    directory = Path(directory)
    metadata = json_read(directory / 'archive-manifest.json')
    verify_metadata(metadata, source, metadata.get('exportSha256', ''))
    require(metadata.get('signingState') in (None, 'signed-local'), 'Unsigned or unknown signing state cannot promote.')
    require(metadata.get('workflowRun') == str(run_id), 'Promotion manifest belongs to a different run.')
    require(metadata.get('archiveSha256') == sha256(directory / SIGNED_ZIP), 'Promotion checksum mismatch.')
    if metadata.get('signingState') == 'signed-local':
        require(sha256(directory / RECEIPT) == metadata.get('signingReceiptSha256'), 'Signing receipt checksum mismatch.')
        require(sha256(directory / 'adoption-receipt.json') == metadata.get('adoptionReceiptSha256'),
                'Adoption receipt checksum mismatch.')
        signing, adoption = json_read(directory / RECEIPT), json_read(directory / 'adoption-receipt.json')
        for key in ('sourceCommit', 'archiveSha256', 'unsignedArchiveSha256', 'unsignedWorkflowRun',
                    'binaryIdentities', 'signingCertificateSha256', 'deviceSha256', 'profileSha256'):
            require(key in metadata and signing.get(key) == adoption.get(key) == metadata[key],
                    'Promotion signing/adoption provenance mismatch: ' + key)
        require(adoption.get('signingState') == 'signed-local' and signing.get('format') == 1
                and adoption.get('profileReadback', {}).get('state') == 'ACTIVE'
                and adoption.get('profileReadback') == metadata.get('profileReadback')
                and adoption.get('transferSha256') == metadata.get('transferSha256'),
                'Promotion lacks a complete signed archive adoption receipt.')
        require(re.fullmatch(r'[0-9]+', metadata['unsignedWorkflowRun']) is not None,
                'Original unsigned run is missing.')
    return metadata


def restore_signed(args):
    metadata = promotion_metadata(args.transfer_directory, args.source, args.archive_run_id)
    with tempfile.TemporaryDirectory(prefix='clinic-promote-') as temporary:
        archive = unpack(args.transfer_directory / SIGNED_ZIP, Path(temporary) / 'signed', ARCHIVE)
        app, _ = verify_app(archive, metadata)
        if metadata.get('signingState') == 'signed-local':
            verify_signature(app, metadata['signingCertificateSha256'])
        else:
            run('codesign', '--verify', '--deep', '--strict', app)
            ent = plistlib.loads(run('codesign', '-d', '--entitlements', ':-', app))
            require(ent.get('com.apple.developer.game-center') is True, 'Game Center signing entitlement missing.')
        if args.write:
            output_directory(args.output)
            shutil.copytree(archive, args.output / ARCHIVE)
    return {'sourceCommit': args.source, 'archiveSha256': metadata['archiveSha256'],
            'workflowRun': str(args.archive_run_id), 'marketingVersion': metadata['marketingVersion'],
            'buildNumber': metadata['buildNumber']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    seal = commands.add_parser('seal-unsigned')
    seal.add_argument('--archive', type=Path, required=True)
    seal.add_argument('--export-manifest', type=Path, required=True)
    seal.add_argument('--workflow-run', required=True)
    sign = commands.add_parser('sign')
    sign.add_argument('--unsigned-sha256', required=True)
    sign.add_argument('--manifest-sha256', required=True)
    sign.add_argument('--identity', required=True)
    sign.add_argument('--profile', type=Path, required=True)
    sign.add_argument('--clone-copies', action='store_true',
                      help='Require macOS same-volume clonefile copies; fail instead of falling back to data copies.')
    adoption = commands.add_parser('adopt')
    adoption.add_argument('--transfer', type=Path, required=True)
    adoption.add_argument('--transfer-sha256', required=True)
    adoption.add_argument('--workflow-run', required=True)
    restore = commands.add_parser('restore-signed')
    restore.add_argument('--transfer-directory', type=Path, required=True)
    restore.add_argument('--archive-run-id', required=True)
    for command in (sign, adoption):
        command.add_argument('--unsigned', type=Path, required=True)
        command.add_argument('--manifest', type=Path, required=True)
        command.add_argument('--unsigned-run-id', required=True)
        command.add_argument('--signer-sha256', required=True)
        command.add_argument('--device-sha256', required=True)
    for command in (seal, sign, adoption):
        command.add_argument('--export-sha256', required=True)
    for command in (seal, sign, adoption, restore):
        command.add_argument('--source', required=True)
        command.add_argument('--output', type=Path, required=True)
        command.add_argument('--write', action='store_true')
    args = parser.parse_args()
    result = {'seal-unsigned': seal_unsigned, 'sign': sign_local, 'adopt': adopt,
              'restore-signed': restore_signed}[args.command](args)
    print(json.dumps({'mode': 'write' if args.write else 'preflight', **result}, indent=2, sort_keys=True))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, zipfile.BadZipFile) as error:
        raise SystemExit('Clinic archive verification failed: ' + str(error))
