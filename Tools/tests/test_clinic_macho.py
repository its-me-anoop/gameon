"""Synthetic byte-level signing transitions; these are not codesign/runtime proof."""
import hashlib
import importlib.util
import json
import struct
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / 'clinic_macho.py'
MACHO = None
if MODULE_PATH.exists():
    SPEC = importlib.util.spec_from_file_location('clinic_macho', MODULE_PATH)
    MACHO = importlib.util.module_from_spec(SPEC)
    SPEC.loader.exec_module(MACHO)


UUID = bytes(range(16))
TEXT_OFFSET = 1024
LINKEDIT_OFFSET = 4096


def commands(data):
    result = []
    offset = 32
    for _ in range(struct.unpack_from('<I', data, 16)[0]):
        command, size = struct.unpack_from('<II', data, offset)
        result.append((command, offset, size))
        offset += size
    return result


def command_offset(data, command):
    return next(offset for kind, offset, _ in commands(data) if kind == command)


def linkedit_offset(data):
    return next(offset for kind, offset, _ in commands(data)
                if kind == 0x19 and data[offset + 8:offset + 24].rstrip(b'\0') == b'__LINKEDIT')


def fixture(*, placeholder=False, placeholder_at_eof=False, page=4096,
            linkedit_size=123, linkedit_vm_size=4096, filetype=2):
    text_vm = 0x100000000
    section = struct.pack('<16s16sQQIIIIIIII', b'__text', b'__TEXT',
                          text_vm + TEXT_OFFSET, 32, TEXT_OFFSET, 2, 0, 0,
                          0x80000400, 0, 0, 0)
    text = struct.pack('<II16sQQQQiiII', 0x19, 152, b'__TEXT', text_vm,
                       page, 0, LINKEDIT_OFFSET, 5, 5, 1, 0) + section
    linkedit = struct.pack('<II16sQQQQiiII', 0x19, 72, b'__LINKEDIT',
                           text_vm + page, linkedit_vm_size, LINKEDIT_OFFSET,
                           linkedit_size, 1, 1, 0, 0)
    uuid = struct.pack('<II16s', 0x1b, 24, UUID)
    symtab = struct.pack('<6I', 0x2, 24, LINKEDIT_OFFSET, 2,
                         LINKEDIT_OFFSET + 32, 64)
    functions = struct.pack('<4I', 0x26, 16, LINKEDIT_OFFSET + 96, 16)
    loads = [text, linkedit, uuid, symtab, functions]
    if placeholder:
        dataoff = (LINKEDIT_OFFSET + linkedit_size + 15) & ~15 if placeholder_at_eof else 0
        loads.append(struct.pack('<4I', 0x1d, 16, dataoff, 0))
    raw = bytearray(LINKEDIT_OFFSET + linkedit_size)
    struct.pack_into('<8I', raw, 0, 0xfeedfacf, 0x0100000c, 0, filetype,
                     len(loads), sum(map(len, loads)), 0x200085, 0)
    raw[32:32 + sum(map(len, loads))] = b''.join(loads)
    raw[TEXT_OFFSET:TEXT_OFFSET + 32] = bytes(range(32))
    raw[LINKEDIT_OFFSET:LINKEDIT_OFFSET + 112] = bytes(range(112))
    return bytes(raw)


def signed_fixture(before, *, signature_size=64, signature_byte=0x5a, page=4096):
    raw = bytearray(before)
    signatures = [offset for kind, offset, _ in commands(before) if kind == 0x1d]
    if signatures:
        signature_command = signatures[0]
        old_dataoff, old_size = struct.unpack_from('<II', before, signature_command + 8)
        payload_end = old_dataoff if old_size else len(before)
    else:
        signature_command = 32 + struct.unpack_from('<I', before, 20)[0]
        ncmds, sizeofcmds = struct.unpack_from('<II', before, 16)
        struct.pack_into('<II', raw, 16, ncmds + 1, sizeofcmds + 16)
        payload_end = len(before)
    dataoff = (payload_end + 15) & ~15
    raw = raw[:payload_end] + bytes(dataoff - payload_end) + bytes([signature_byte]) * signature_size
    struct.pack_into('<4I', raw, signature_command, 0x1d, 16, dataoff, signature_size)
    segment = linkedit_offset(before)
    vm_size = struct.unpack_from('<Q', before, segment + 32)[0]
    new_size = len(raw) - LINKEDIT_OFFSET
    if new_size > vm_size:
        vm_size = (new_size + page - 1) & -page
    struct.pack_into('<Q', raw, segment + 32, vm_size)
    struct.pack_into('<Q', raw, segment + 48, new_size)
    return bytes(raw)


def changed(data, offset, replacement):
    raw = bytearray(data)
    raw[offset:offset + len(replacement)] = replacement
    return bytes(raw)


class MachOComparisonTests(unittest.TestCase):
    def setUp(self):
        self.assertIsNotNone(MACHO, 'The signature-only Mach-O comparator must exist')

    def assert_rejected(self, before, after):
        with self.assertRaises(ValueError):
            MACHO.compare_macho(before, after)

    def test_unsigned_executable_can_gain_only_a_terminal_signature(self):
        before = fixture()
        after = signed_fixture(before)
        result = MACHO.compare_macho(before, after)
        self.assertEqual(result['unsignedSha256'], hashlib.sha256(before).hexdigest())
        self.assertEqual(result['signedSha256'], hashlib.sha256(after).hexdigest())
        self.assertEqual(result['architecture'], 'arm64')
        self.assertEqual(result['uuid'], '00010203-0405-0607-0809-0A0B0C0D0E0F')
        self.assertEqual(len(result['payloadSha256']), 64)
        json.dumps(result)

    def test_all_macho_magic_variants_are_detected_even_when_unsupported(self):
        for magic in ('feedface', 'cefaedfe', 'feedfacf', 'cffaedfe',
                      'cafebabe', 'bebafeca', 'cafebabf', 'bfbafeca'):
            with self.subTest(magic=magic):
                self.assertTrue(MACHO.is_macho(bytes.fromhex(magic)))
        for data in (b'', b'abc', b'plain text', b'\x7fELF'):
            self.assertFalse(MACHO.is_macho(data))

    def test_inspection_validates_unsigned_input_without_requiring_a_signature(self):
        self.assertTrue(callable(getattr(MACHO, 'inspect_macho', None)),
                        'An unsigned preflight inspection API must exist')
        before = fixture()
        result = MACHO.inspect_macho(before)
        self.assertEqual(result['sha256'], hashlib.sha256(before).hexdigest())
        self.assertEqual(result['architecture'], 'arm64')
        self.assertEqual(result['fileType'], 'MH_EXECUTE')
        self.assertIsNone(result['signature'])
        json.dumps(result)

    def test_inspection_reports_existing_and_empty_signature_commands(self):
        self.assertTrue(callable(getattr(MACHO, 'inspect_macho', None)))
        for before in (fixture(placeholder=True), signed_fixture(fixture(filetype=6))):
            with self.subTest(length=len(before)):
                result = MACHO.inspect_macho(before)
                offset = command_offset(before, 0x1d)
                start, size = struct.unpack_from('<II', before, offset + 8)
                self.assertEqual(result['signature'],
                                 {'commandOffset': offset, 'dataOffset': start, 'dataSize': size})

    def test_inspection_rejects_unsupported_and_malformed_inputs(self):
        self.assertTrue(callable(getattr(MACHO, 'inspect_macho', None)))
        for before in (b'', b'\xca\xfe\xba\xbe' + bytes(100), fixture()[:100]):
            with self.subTest(length=len(before)), self.assertRaises(ValueError):
                MACHO.inspect_macho(before)

    def test_inspection_rejects_unsigned_binary_without_zero_signature_command_space(self):
        before = fixture()
        command_end = 32 + struct.unpack_from('<I', before, 20)[0]
        before = changed(before, command_end, b'no header space!')
        with self.assertRaises(ValueError):
            MACHO.inspect_macho(before)

    def test_dylib_is_supported(self):
        before = fixture(filetype=6)
        MACHO.compare_macho(before, signed_fixture(before))

    def test_existing_terminal_signature_may_grow_or_shrink(self):
        before = signed_fixture(fixture(), signature_size=96)
        for size in (32, 128, 8192):
            with self.subTest(size=size):
                MACHO.compare_macho(before, signed_fixture(before, signature_size=size))

    def test_zero_sized_existing_signature_command_is_supported(self):
        for at_eof in (False, True):
            before = fixture(placeholder=True, placeholder_at_eof=at_eof)
            with self.subTest(at_eof=at_eof):
                MACHO.compare_macho(before, signed_fixture(before))

    def test_payload_hash_is_independent_of_signature_content_and_size(self):
        before = fixture()
        first = MACHO.compare_macho(before, signed_fixture(before))
        second_signed = signed_fixture(before, signature_size=8192, signature_byte=0x77)
        second = MACHO.compare_macho(before, second_signed)
        self.assertEqual(first['payloadSha256'], second['payloadSha256'])
        resigned = signed_fixture(second_signed, signature_size=48)
        third = MACHO.compare_macho(second_signed, resigned)
        self.assertEqual(first['payloadSha256'], third['payloadSha256'])

    def test_linkedit_growth_uses_binary_alignment_and_preserves_spare_capacity(self):
        for page, capacity, size in ((4096, 4096, 5000), (16384, 16384, 18000),
                                     (4096, 8192, 64)):
            with self.subTest(page=page, capacity=capacity):
                before = fixture(page=page, linkedit_vm_size=capacity)
                MACHO.compare_macho(before, signed_fixture(before, page=page, signature_size=size))

    def test_identical_uuid_does_not_allow_changed_code_or_data(self):
        before = fixture()
        after = signed_fixture(before)
        for offset in (TEXT_OFFSET, LINKEDIT_OFFSET, LINKEDIT_OFFSET + 95,
                       LINKEDIT_OFFSET + 112, len(before) - 1, 700):
            with self.subTest(offset=offset):
                self.assert_rejected(before, changed(after, offset, bytes([after[offset] ^ 0xff])))

    def test_header_and_non_signature_load_commands_are_immutable(self):
        before = fixture()
        after = signed_fixture(before)
        offsets = (24, 28, 32 + 24, 32 + 40, 32 + 56,
                   command_offset(before, 0x1b) + 8,
                   command_offset(before, 0x2) + 20,
                   command_offset(before, 0x26) + 12)
        for offset in offsets:
            with self.subTest(offset=offset):
                self.assert_rejected(before, changed(after, offset, bytes([after[offset] ^ 1])))

    def test_added_command_cannot_overwrite_nonzero_header_padding(self):
        before = fixture()
        end = 32 + struct.unpack_from('<I', before, 20)[0]
        before = changed(before, end, b'payload')
        self.assert_rejected(before, signed_fixture(before))

    def test_added_command_cannot_overwrite_a_file_backed_section(self):
        before = fixture()
        end = 32 + struct.unpack_from('<I', before, 20)[0]
        before = changed(before, 32 + 72 + 32, struct.pack('<Q', 0x100000000 + end))
        before = changed(before, 32 + 72 + 48, struct.pack('<I', end))
        self.assert_rejected(before, signed_fixture(before))

    def test_signature_cannot_be_used_to_drop_or_ignore_payload_bytes(self):
        before = fixture()
        after = signed_fixture(before)
        offset = command_offset(after, 0x1d)
        dataoff, size = struct.unpack_from('<II', after, offset + 8)
        for new_start in (dataoff - 16, dataoff + 16, LINKEDIT_OFFSET - 16):
            with self.subTest(start=new_start):
                candidate = changed(after, offset + 8, struct.pack('<II', new_start, len(after) - new_start))
                self.assert_rejected(before, candidate)

    def test_existing_signature_boundary_cannot_move(self):
        before = signed_fixture(fixture())
        after = signed_fixture(before, signature_size=96)
        offset = command_offset(after, 0x1d)
        start, size = struct.unpack_from('<II', after, offset + 8)
        self.assert_rejected(before, changed(after, offset + 8, struct.pack('<II', start - 16, size + 16)))

    def test_only_new_alignment_bytes_may_be_inserted_and_they_must_be_zero(self):
        before = fixture()
        after = signed_fixture(before)
        self.assert_rejected(before, changed(after, len(before), b'x'))

    def test_signature_must_cover_the_entire_file_tail(self):
        before = fixture()
        after = signed_fixture(before)
        for suffix in (b'x', bytes(16)):
            with self.subTest(suffix=suffix):
                self.assert_rejected(before, after + suffix)
        offset = command_offset(after, 0x1d)
        self.assert_rejected(before, changed(after, offset + 12, struct.pack('<I', 0)))

    def test_linkedit_sizes_cannot_be_arbitrarily_normalized(self):
        before = fixture()
        after = signed_fixture(before)
        segment = linkedit_offset(after)
        for offset, value in ((segment + 32, 2**40), (segment + 48, 1),
                              (segment + 32, 8192)):
            with self.subTest(offset=offset, value=value):
                self.assert_rejected(before, changed(after, offset, struct.pack('<Q', value)))

    def test_old_excess_linkedit_vm_size_may_not_shrink(self):
        before = fixture(linkedit_vm_size=8192)
        after = signed_fixture(before)
        self.assert_rejected(before, changed(after, linkedit_offset(after) + 32, struct.pack('<Q', 4096)))

    def test_unsupported_architectures_filetypes_and_magic_fail_closed(self):
        before = fixture()
        for offset, replacement in ((0, bytes.fromhex('cafebabe')), (0, bytes.fromhex('feedfacf')),
                                     (4, struct.pack('<I', 0x01000007)),
                                     (8, struct.pack('<I', 2)), (12, struct.pack('<I', 1))):
            candidate = changed(before, offset, replacement)
            with self.subTest(offset=offset, value=replacement):
                self.assert_rejected(candidate, candidate)

    def test_truncated_and_malformed_commands_fail_closed(self):
        before = fixture()
        cases = (b'', before[:31], changed(before, 16, struct.pack('<I', 0xffffffff)),
                 changed(before, 20, struct.pack('<I', 0xffffffff)),
                 changed(before, 36, struct.pack('<I', 0)),
                 changed(before, 36, struct.pack('<I', 7)),
                 changed(before, 36, struct.pack('<I', 2**20)),
                 changed(before, 32 + 64, struct.pack('<I', 0xffffffff)))
        for index, candidate in enumerate(cases):
            with self.subTest(index=index):
                self.assert_rejected(candidate, candidate)

    def test_unsigned_after_is_rejected(self):
        before = fixture()
        self.assert_rejected(before, before)

    def test_zero_signature_placeholder_cannot_point_inside_payload(self):
        before = fixture(placeholder=True)
        before = changed(before, command_offset(before, 0x1d) + 8, struct.pack('<I', 1024))
        self.assert_rejected(before, signed_fixture(before))

    def test_payload_references_cannot_overlap_an_existing_signature(self):
        before = signed_fixture(fixture())
        start = struct.unpack_from('<I', before, command_offset(before, 0x1d) + 8)[0]
        before = changed(before, command_offset(before, 0x26) + 8, struct.pack('<II', start, 16))
        self.assert_rejected(before, signed_fixture(before))

    def test_signature_command_must_be_last_and_unique(self):
        before = fixture()
        after = signed_fixture(before)
        signature = command_offset(after, 0x1d)
        functions = command_offset(after, 0x26)
        candidate = changed(after, functions, after[signature:signature + 16])
        candidate = changed(candidate, signature, after[functions:functions + 16])
        self.assert_rejected(before, candidate)
        candidate = changed(after, functions, after[signature:signature + 16])
        self.assert_rejected(before, candidate)

    def test_uuid_is_required_and_unique(self):
        before = fixture()
        missing = changed(before, command_offset(before, 0x1b), struct.pack('<I', 0x777))
        self.assert_rejected(missing, signed_fixture(missing))
        duplicate = changed(before, command_offset(before, 0x2), struct.pack('<I', 0x1b))
        self.assert_rejected(duplicate, signed_fixture(duplicate))

    def test_linkedit_must_be_terminal_read_only_and_without_sections(self):
        before = fixture()
        segment = linkedit_offset(before)
        for offset, replacement in ((segment + 40, struct.pack('<Q', LINKEDIT_OFFSET - 16)),
                                     (segment + 56, struct.pack('<i', 5)),
                                     (segment + 60, struct.pack('<i', 3))):
            candidate = changed(before, offset, replacement)
            with self.subTest(offset=offset):
                self.assert_rejected(candidate, signed_fixture(candidate))


if __name__ == '__main__':
    unittest.main()
