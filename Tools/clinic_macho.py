"""Fail-closed, fixed-offset comparison of locally signed iOS Mach-O files.

This checks provenance, not signature validity: callers must independently run
Apple's codesign verification and validate identity, entitlements and profiles.
Only little-endian, thin arm64 ALL executables/dylibs are supported. Signing may
append a terminal signature command in zero header padding, replace an existing
terminal command, and extend the read-only __LINKEDIT segment. Repacking, page-
aligned (-p) signing and arbitrary load-command normalization are unsupported.

The permitted changes follow Apple's codesign_allocate.c and libstuff/align.c:
https://github.com/apple-oss-distributions/cctools/tree/main
"""

import hashlib
import struct
import uuid
from dataclasses import dataclass


_MAGICS = {bytes.fromhex(value) for value in (
    'feedface', 'cefaedfe', 'feedfacf', 'cffaedfe',
    'cafebabe', 'bebafeca', 'cafebabf', 'bfbafeca',
)}
_LINKEDIT_DATA_COMMANDS = {
    0x1e, 0x26, 0x29, 0x2b, 0x2e, 0x80000033, 0x80000034,
    0x36, 0x37, 0x38,
}
_ZERO_FILL_TYPES = {0x1, 0xc, 0x12}


@dataclass(frozen=True)
class _Command:
    kind: int
    offset: int
    size: int


@dataclass(frozen=True)
class _Segment:
    offset: int
    name: bytes
    vmaddr: int
    vmsize: int
    fileoff: int
    filesize: int
    maxprot: int
    initprot: int
    nsects: int


@dataclass(frozen=True)
class _Image:
    ncmds: int
    sizeofcmds: int
    commands: tuple
    segments: tuple
    linkedit: _Segment
    signature: tuple | None
    uuid_bytes: bytes
    first_section_offset: int
    payload_end: int


def is_macho(data: bytes) -> bool:
    """Recognize all Mach-O/fat magic variants, including unsupported formats."""
    return data[:4] in _MAGICS


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def _align(value, boundary):
    return (value + boundary - 1) & -boundary


def _segment_alignment(segments):
    # cctools infers alignment from vmaddrs, clamped to exponents 3..15.
    exponents = (min(15, max(3, (segment.vmaddr & -segment.vmaddr).bit_length() - 1))
                 if segment.vmaddr else 15 for segment in segments)
    return 1 << min(exponents)


def _range(start, size, lower, upper, description):
    if size:
        _require(lower <= start <= upper and size <= upper - start,
                 f'{description} lies outside its permitted payload range')


def _parse(data):
    _require(isinstance(data, bytes), 'Mach-O inputs must be bytes')
    _require(len(data) >= 32, 'Truncated Mach-O header')
    magic, cpu, subtype, filetype, ncmds, sizeofcmds, _, _ = struct.unpack_from('<8I', data)
    _require(magic == 0xfeedfacf, 'Only little-endian thin 64-bit Mach-O is supported')
    _require(cpu == 0x0100000c and subtype == 0, 'Only arm64 ALL is supported')
    _require(filetype in (2, 6), 'Only MH_EXECUTE and MH_DYLIB are supported')
    command_end = 32 + sizeofcmds
    _require(command_end <= len(data) and 0 < ncmds <= sizeofcmds // 8,
             'Invalid Mach-O load-command bounds')
    commands = []
    segments = []
    signatures = []
    uuids = []
    references = []
    section_ranges = []
    first_section_offset = len(data)
    offset = 32
    for index in range(ncmds):
        _require(offset + 8 <= command_end, 'Truncated load command')
        kind, size = struct.unpack_from('<II', data, offset)
        _require(size >= 8 and size % 8 == 0 and size <= command_end - offset,
                 'Invalid load-command size')
        command = _Command(kind, offset, size)
        commands.append(command)
        if kind == 0x19:
            _require(size >= 72, 'Truncated 64-bit segment')
            values = struct.unpack_from('<II16sQQQQiiII', data, offset)
            segment = _Segment(offset, values[2].rstrip(b'\0'), *values[3:10])
            _require(size == 72 + 80 * segment.nsects, 'Invalid segment section table')
            _require(segment.filesize <= segment.vmsize, 'Segment filesize exceeds vmsize')
            _require(segment.vmaddr + segment.vmsize <= 2**64, 'Segment VM range overflows')
            _range(segment.fileoff, segment.filesize, 0, len(data), 'Segment')
            segments.append(segment)
            for section_index in range(segment.nsects):
                section = struct.unpack_from('<16s16sQQIIIIIIII', data,
                                             offset + 72 + 80 * section_index)
                _, section_segment, address, length, file_offset, _, reloc_offset, reloc_count, flags, _, _, _ = section
                _require(section_segment.rstrip(b'\0') == segment.name,
                         'Section references a different segment')
                _range(address, length, segment.vmaddr, segment.vmaddr + segment.vmsize, 'Section VM')
                if flags & 0xff not in _ZERO_FILL_TYPES and length:
                    _range(file_offset, length, segment.fileoff,
                           segment.fileoff + segment.filesize, 'Section file')
                    _require(file_offset >= command_end, 'Section overlaps Mach-O headers')
                    first_section_offset = min(first_section_offset, file_offset)
                    section_ranges.append((file_offset, file_offset + length))
                    references.append((file_offset, length, False, 'Section'))
                references.append((reloc_offset, reloc_count * 8, True, 'Section relocations'))
        elif kind in (0x1, 0x80000035):
            raise ValueError('32-bit segments and filesets are unsupported')
        elif kind == 0x1d:
            _require(size == 16 and index == ncmds - 1, 'Signature command must be terminal and 16 bytes')
            signatures.append((offset, *struct.unpack_from('<II', data, offset + 8)))
        elif kind == 0x1b:
            _require(size == 24, 'Invalid UUID command')
            uuids.append(data[offset + 8:offset + 24])
        elif kind in _LINKEDIT_DATA_COMMANDS:
            _require(size == 16, 'Invalid linkedit-data command')
            start, length = struct.unpack_from('<II', data, offset + 8)
            references.append((start, length, True, 'Linkedit data'))
        elif kind == 0x2:
            _require(size == 24, 'Invalid symbol table command')
            symoff, count, stroff, strsize = struct.unpack_from('<4I', data, offset + 8)
            references.extend(((symoff, count * 16, True, 'Symbol table'),
                               (stroff, strsize, True, 'String table')))
        elif kind == 0xb:
            _require(size == 80, 'Invalid dynamic symbol table command')
            values = struct.unpack_from('<18I', data, offset + 8)
            for field, element_size in ((6, 8), (8, 56), (10, 4), (12, 4), (14, 8), (16, 8)):
                references.append((values[field], values[field + 1] * element_size,
                                   True, 'Dynamic symbol table'))
        elif kind in (0x22, 0x80000022):
            _require(size == 48, 'Invalid dyld-info command')
            values = struct.unpack_from('<10I', data, offset + 8)
            references.extend((values[index], values[index + 1], True, 'Dyld info')
                              for index in range(0, 10, 2))
        elif kind == 0x16:
            _require(size == 16, 'Invalid two-level hints command')
            start, count = struct.unpack_from('<II', data, offset + 8)
            references.append((start, count * 4, True, 'Two-level hints'))
        elif kind == 0x31:
            _require(size == 40, 'Invalid note command')
            start, length = struct.unpack_from('<QQ', data, offset + 24)
            references.append((start, length, False, 'Note'))
        elif kind == 0x2c:
            _require(size == 24, 'Invalid encryption-info command')
            start, length, cryptid, _ = struct.unpack_from('<4I', data, offset + 8)
            _require(cryptid == 0, 'Encrypted Mach-O inputs are unsupported')
            references.append((start, length, False, 'Encryption range'))
        elif kind == 0x80000028:
            _require(size == 24, 'Invalid entry-point command')
            start = struct.unpack_from('<Q', data, offset + 8)[0]
            references.append((start, 1, False, 'Entry point'))
        offset += size
    _require(offset == command_end, 'Load commands do not exactly fill sizeofcmds')
    _require(len(uuids) == 1, 'Exactly one UUID command is required')
    _require(len(signatures) <= 1, 'Duplicate signature command')
    _require(len({segment.name for segment in segments}) == len(segments), 'Duplicate segment name')
    texts = [segment for segment in segments if segment.name == b'__TEXT']
    linkedits = [segment for segment in segments if segment.name == b'__LINKEDIT']
    _require(len(texts) == len(linkedits) == 1, 'Exactly one __TEXT and __LINKEDIT are required')
    text, linkedit = texts[0], linkedits[0]
    _require(text.fileoff == 0 and text.filesize >= command_end, '__TEXT must contain the headers')
    _require(linkedit.nsects == 0 and linkedit.maxprot == linkedit.initprot == 1,
             '__LINKEDIT must be read-only and contain no sections')
    _require(linkedit.filesize > 0 and linkedit.fileoff + linkedit.filesize == len(data),
             '__LINKEDIT must cover the exact file tail')
    for ranges, description in (
        ([(segment.fileoff, segment.fileoff + segment.filesize)
          for segment in segments if segment.filesize], 'File segments'),
        ([(segment.vmaddr, segment.vmaddr + segment.vmsize)
          for segment in segments if segment.vmsize], 'VM segments'),
        (section_ranges, 'File sections'),
    ):
        ordered = sorted(ranges)
        _require(all(left[1] <= right[0] for left, right in zip(ordered, ordered[1:])),
                 f'{description} overlap')
    signature = signatures[0] if signatures else None
    payload_end = len(data)
    if signature:
        _, start, length = signature
        if length:
            _require(start % 16 == 0 and start >= linkedit.fileoff and
                     start + length == len(data), 'Invalid terminal signature extent')
            payload_end = start
        else:
            _require(start in (0, _align(len(data), 16)), 'Invalid empty signature placeholder')
    for start, length, in_linkedit, description in references:
        _range(start, length, linkedit.fileoff if in_linkedit else 0, payload_end, description)
    for segment in segments:
        if segment is not linkedit:
            _range(segment.fileoff, segment.filesize, 0, payload_end, 'Non-signature segment')
    return _Image(ncmds, sizeofcmds, tuple(commands), tuple(segments), linkedit,
                  signature, uuids[0], first_section_offset, payload_end)


def _canonical_hash(before, image, signature_offset, signature_start):
    """Hash original payload; derive any added alignment bytes, never strip zeros."""
    existing = int(image.signature is not None)
    patches = (
        (16, struct.pack('<II', image.ncmds - existing, image.sizeofcmds - 16 * existing)),
        (image.linkedit.offset + 32, bytes(8)),
        (image.linkedit.offset + 48, bytes(8)),
        (signature_offset, bytes(16)),
    )
    digest = hashlib.sha256()
    view = memoryview(before)
    position = 0
    for offset, replacement in patches:
        digest.update(view[position:offset])
        digest.update(replacement)
        position = offset + len(replacement)
    digest.update(view[position:image.payload_end])
    digest.update(bytes(signature_start - image.payload_end))
    return digest.hexdigest()


def _signature_command_offset(data, image):
    if image.signature is not None:
        return image.signature[0]
    offset = 32 + image.sizeofcmds
    _require(offset + 16 <= image.first_section_offset and
             offset + 16 <= image.linkedit.fileoff and
             data[offset:offset + 16] == bytes(16),
             'New signature command must occupy existing zero header padding')
    return offset


def inspect_macho(data: bytes) -> dict:
    """Validate supported structure before signing; do not authenticate a signature."""
    parsed = _parse(data)
    _signature_command_offset(data, parsed)
    signature = None
    if parsed.signature is not None:
        offset, start, size = parsed.signature
        signature = {'commandOffset': offset, 'dataOffset': start, 'dataSize': size}
    return {
        'sha256': hashlib.sha256(data).hexdigest(),
        'uuid': str(uuid.UUID(bytes=parsed.uuid_bytes)).upper(),
        'architecture': 'arm64',
        'fileType': 'MH_EXECUTE' if struct.unpack_from('<I', data, 12)[0] == 2 else 'MH_DYLIB',
        'signature': signature,
    }


def compare_macho(before: bytes, after: bytes) -> dict[str, str]:
    """Prove a supported signature-only transition, or raise ValueError.

    unsignedSha256 and signedSha256 hash the complete inputs. payloadSha256
    hashes the fixed-offset protected payload with signing fields canonicalized
    and only the precisely derived alignment bytes added. The original input
    may already have a terminal ad-hoc signature; its signature bytes are opaque.
    """
    original, signed = _parse(before), _parse(after)
    _require(signed.signature is not None and signed.signature[2] > 0,
             'Signed output must have a nonempty terminal signature')
    signature_offset, signature_start, signature_size = signed.signature
    added = original.signature is None
    expected_command = _signature_command_offset(before, original)
    _require(signature_offset == expected_command and
             signed.ncmds == original.ncmds + int(added) and
             signed.sizeofcmds == original.sizeofcmds + 16 * int(added),
             'Signing changed the load-command structure')
    expected_start = _align(original.payload_end, 16)
    _require(signature_start == expected_start, 'Signature boundary changed protected payload')
    _require(after[original.payload_end:signature_start] == bytes(signature_start - original.payload_end),
             'New signature alignment padding must be zero')
    old_linkedit = original.linkedit
    expected_filesize = signature_start + signature_size - old_linkedit.fileoff
    expected_vmsize = old_linkedit.vmsize
    if expected_filesize > expected_vmsize:
        expected_vmsize = _align(expected_filesize, _segment_alignment(original.segments))
    _require(signed.linkedit.offset == old_linkedit.offset and
             signed.linkedit.filesize == expected_filesize and
             signed.linkedit.vmsize == expected_vmsize,
             'Unexpected __LINKEDIT signing growth')
    # Every byte retains its original offset. Only these four exact regions may
    # differ; their structure/value transitions have already been validated.
    exclusions = ((16, 24), (old_linkedit.offset + 32, old_linkedit.offset + 40),
                  (old_linkedit.offset + 48, old_linkedit.offset + 56),
                  (signature_offset, signature_offset + 16))
    old_view, new_view = memoryview(before), memoryview(after)
    position = 0
    for start, end in exclusions:
        _require(old_view[position:start] == new_view[position:start],
                 f'Mach-O payload changed before byte {start}')
        position = end
    _require(old_view[position:original.payload_end] == new_view[position:original.payload_end],
             'Mach-O payload changed after the load commands')
    return {
        'unsignedSha256': hashlib.sha256(before).hexdigest(),
        'signedSha256': hashlib.sha256(after).hexdigest(),
        'payloadSha256': _canonical_hash(before, original, signature_offset, signature_start),
        'uuid': str(uuid.UUID(bytes=original.uuid_bytes)).upper(),
        'architecture': 'arm64',
    }
