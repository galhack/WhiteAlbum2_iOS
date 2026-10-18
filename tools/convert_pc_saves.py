#!/usr/bin/env python3
"""Convert inspected WA2 PC v2 / VM 1.00 saves to the Apple port's native format.

Outputs go into a NEW directory. No originals, system progress or settings are changed.
Only the observed single-script CK-GAL layout is supported; unknown layouts fail closed.
"""
# SPDX-License-Identifier: Apache-2.0
import argparse
import datetime
import hashlib
import json
import math
import re
import struct
from functools import lru_cache
from pathlib import Path

STATE = 0x110A0
VM = 0x134E0
SIZE = 348112
EXPECTED_FLAGS = (
    'FLG_00',
    'SubWait_timer',
    'SubWhiteIn_timer',
    'SubWhiteOut_timer',
    'FLG_章管理サブ',
    'FLG_かずさ本気度',
    'FLG_かずさ浮気度',
    'FLG_雪菜好意度',
    'FLG_小春好意度',
    'FLG_千晶好意度',
    'FLG_麻理好意度',
    'FLG_小春ルート消滅',
    'FLG_千晶ルート消滅',
    'FLG_麻理ルート消滅',
    'FLG_第２部03選択肢１',
    'FLG_第２部03選択肢２',
    'FLG_第２部13選択肢１',
    'FLG_第２部14選択肢１',
    'FLG_第２部15選択肢１',
    'FLG_第２部18選択肢１',
    'FLG_第３部05選択肢１',
    'FLG_第３部09選択肢１',
    'FLG_第３部13条件１',
    'FLG_第３部13選択肢１',
    'FLG_第３部13選択肢２',
    'FLG_第３部14選択肢１',
    'FLG_第３部15条件２',
    'FLG_第３部16条件３',
    'FLG_第３部16選択肢２',
)


def u32(data, offset):
    return struct.unpack_from('<I', data, offset)[0]


def i32(data, offset):
    return struct.unpack_from('<i', data, offset)[0]


def ints(*values):
    return struct.pack('<' + 'I' * len(values), *(int(x) & 0xffffffff for x in values))


def floats(*values):
    if not all(math.isfinite(x) for x in values):
        raise ValueError('Non-finite snapshot coordinate')
    return struct.pack('<' + 'f' * len(values), *values)


def decode_text(raw):
    raw = raw.split(b'\0', 1)[0]
    # Match Wa2Encoding.CustomMappings rather than silently dropping atlas glyphs.
    custom = dict(zip(list(range(0x89, 0xa0)) + list(range(0xe0, 0xeb)),
                      '蹑蹒荚荞蹦蹩蹭蹰蹲车轨转轮软轰轴轶轻载较辄辅辆辈辉辍辐辑输辕辙邋锋'))
    out = []
    offset = 0
    while offset < len(raw):
        first = raw[offset]
        pair = offset + 1 < len(raw) and (0x81 <= first <= 0x9f or 0xe0 <= first <= 0xfe)
        unit = raw[offset:offset + (2 if pair else 1)]
        if pair and unit[1] == 0x7f and first in custom:
            out.append(custom[first])
        else:
            out.append(unit.decode('cp932'))
        offset += len(unit)
    return ''.join(out)


def fixed_text(text, size):
    raw = text.encode('utf-16-le')
    if len(raw) > size:
        raise ValueError(f'Text exceeds native field ({len(raw)} > {size})')
    return raw.ljust(size, b'\0')


def local_values(data, offset, kind):
    # Native VM has 16 call frames per local variable; imported saves use frame 0.
    return [struct.unpack_from('<' + kind, data, offset + index * 64)[0] for index in range(26)]


def thumbnail(data):
    # PC: 128x128 top-down BGRA. Apple: 256x144 top-down RGB.
    out = bytearray()
    for y in range(144):
        sy = y * 128 // 144
        for x in range(256):
            offset = 0x58 + (sy * 128 + x * 128 // 256) * 4
            out.extend(data[offset:offset + 3][::-1])
    return bytes(out)


def convert_argument(raw):
    cmd, kind, pointer, float_bits, value = struct.unpack('<5I', raw)
    if cmd == kind == pointer == float_bits == value == 0:
        return ints(5, 3, 0, 0, 0)  # Pending function return, including route choices.
    if pointer or cmd not in (1, 2, 3, 5) or kind not in (0, 3, 4):
        raise ValueError('Unsupported VM argument; refusing to serialize a PC pointer')
    return ints(cmd, kind, 0, value, float_bits)


def parse_pc(data):
    if len(data) != SIZE or u32(data, 0) != 2 or data[0x134d4:0x134d8] != b'1.00':
        raise ValueError('Expected PC save v2 with VM 1.00 (348112 bytes)')
    if u32(data, 0x134d8) != 4:
        raise ValueError('Unknown PC script storage layout')
    for index in range(1, 5):
        start = VM + index * 53648
        if any(data[start:start+53648]):
            raise ValueError('Nested PC script stack is not supported')
    script = data[0x203d8:0x203e0].split(b'\0', 1)[0].decode('ascii')
    if not re.fullmatch(r'[123]\d{3}(?:_\d{3})?', script):
        raise ValueError('Unsupported or missing main-game script')
    if u32(data, 0x1c01c) not in (131, 209):
        raise ValueError('Only stable dialogue and route-choice snapshots are supported')
    # Native local values at nonzero call depth cannot be represented by this port.
    for offset in (0x1d594, 0x1dc14):
        for index in range(26):
            if any(data[offset+index*64+4:offset+(index+1)*64]):
                raise ValueError('Nonzero nested local call frames are unsupported')
    offset = 0x54cb4
    count = u32(data, offset)
    offset += 4
    if count != 29:
        raise ValueError('Expected 29 named game variables')
    names, flags = [], []
    for index in range(count):
        size = u32(data, offset)
        offset += 4
        if not 1 <= size <= 256 or offset + size + 5 > len(data):
            raise ValueError('Invalid named variable size')
        names.append(data[offset:offset+size-1].decode('cp932'))
        offset += size
        if data[offset] != 3:
            raise ValueError('Unsupported global variable type')
        flags.append(i32(data, offset+1))
        offset += 5
    if offset != len(data) or tuple(names) != EXPECTED_FLAGS:
        raise ValueError('Unknown global variable registry')
    position = u32(data, 0x1c18c)
    count = u32(data, 0x203d4)
    if count > 15:
        raise ValueError('Invalid jump stack count')
    jumps = []
    for index in range(count):
        start = 0x1e4a8 + index * 532
        # Native: type, count, reserved, end position, positions[64], flags[64], condition.
        jumps.append(ints(u32(data, start), u32(data, start+4), u32(data, start+12), u32(data, start+528))
                     + data[start+16:start+528])
    count = u32(data, 0x1d590)
    if count > 256:
        raise ValueError('Invalid operand stack count')
    args = [convert_argument(data[0x1c190+i*20:0x1c190+(i+1)*20]) for i in range(count)]
    state = lambda index: i32(data, STATE + index*4)
    text = decode_text(data[STATE+546:STATE+546+1024])
    name = decode_text(data[STATE+1572:STATE+1588])
    choices = []
    count = state(412)
    if not 0 <= count <= 3:
        raise ValueError('Invalid choice count')
    if count and not args:
        raise ValueError('Route choice has no return operand')
    for index in range(count):
        choices.append((decode_text(data[STATE+1652+index*256:STATE+1652+(index+1)*256]),
                        *(data[STATE+field*4+index] for field in (941, 943, 945))))
    chars = [(state(76+i), state(56+i), state(64+i)) for i in range(8) if state(48+i)]
    if any(not 0 <= pos <= 10 for pos, _, _ in chars):
        raise ValueError('Unknown character position')
    timestamp = list(struct.unpack_from('<8H', data, 8))
    datetime.datetime(timestamp[0], timestamp[1], timestamp[3], timestamp[4], timestamp[5], timestamp[6])
    bg_id, bg_type = state(6), state(11)
    bg_path = f'v{bg_id:06}.tga' if bg_type == 1 else f'h{bg_id:06}.tga' if bg_type == 2 else f'B{bg_id // 10:04}{bg_id % 10}{state(19)}.tga'
    return dict(script=script, position=position, flags=flags, named_flags=dict(zip(names, flags)),
                locals=local_values(data, 0x1d594, 'i'), local_floats=local_values(data, 0x1dc14, 'f'),
                jumps=jumps, args=args, text=text, name=name, choices=choices, chars=chars,
                timestamp=timestamp, bg_path=bg_path, bg_type=bg_type)


class Archive:
    def __init__(self, path):
        self.path = Path(path)
        self.entries = {}
        with self.path.open('rb') as stream:
            header = stream.read(16)
            if header[:4] != b'KCAP':
                raise ValueError(f'Not a KCAP archive: {path}')
            count = u32(header, 12)
            if 16 + count * 44 > self.path.stat().st_size:
                raise ValueError('Archive index exceeds file')
            for _ in range(count):
                record = stream.read(44)
                name = record[4:28].split(b'\0', 1)[0].decode('cp932').lower()
                flag, offset, size = u32(record, 0), u32(record, 36), u32(record, 40)
                if offset + size > self.path.stat().st_size:
                    raise ValueError('Archive entry exceeds file')
                self.entries[name] = flag, offset, size

    @lru_cache(maxsize=128)
    def read(self, name):
        flag, offset, size = self.entries[name.lower()]
        with self.path.open('rb') as stream:
            stream.seek(offset)
            data = stream.read(size)
        if not flag:
            return data
        compressed, expected = struct.unpack_from('<II', data)
        if not 8 <= compressed <= len(data) or expected > 16 * 1024 * 1024:
            raise ValueError('Invalid compressed script size')
        data = data[8:compressed]
        window = bytearray(b' ' * 0xfee + b'\0' * 18)
        write, offset, out = 0xfee, 0, bytearray()
        while offset < len(data) and len(out) < expected:
            bits = data[offset]
            offset += 1
            for index in range(8):
                if offset >= len(data) or len(out) >= expected:
                    break
                first = data[offset]
                offset += 1
                if bits & (1 << index):
                    values = [first]
                else:
                    if offset >= len(data):
                        raise ValueError('Truncated compressed script')
                    second = data[offset]
                    offset += 1
                    read = first | (second & 0xf0) << 4
                    values = None
                for j in range(1 if values else (second & 15) + 3):
                    value = first if values else window[(read+j) & 4095]
                    window[write & 4095] = value
                    write += 1
                    out.append(value)
        if len(out) != expected:
            raise ValueError('Decompressed script size mismatch')
        return bytes(out)


def convert(data, scripts):
    state = parse_pc(data)
    bnr = scripts.read(state['script'] + '.bnr')
    pos = state['position']
    if bnr[:4] != b'LSCR' or pos % 4 or not 8 <= pos < len(bnr):
        raise ValueError('Saved position is outside the selected script')
    if (u32(bnr, pos-8), u32(bnr, pos-4)) != (4, u32(data, 0x1c01c)):
        raise ValueError('Saved script position does not match the pending PC function')
    for jump in state['jumps']:
        if u32(jump, 8) > len(bnr):
            raise ValueError('Jump destination exceeds script size')
    s = lambda index: i32(data, STATE+index*4)
    out = bytearray(ints(*state['timestamp']))
    out += thumbnail(data)
    out += state['script'].encode('ascii').ljust(8, b'\0')
    out += ints(s(2313), s(2314), s(2316), s(2315))
    out += fixed_text(state['text'], 1024) + fixed_text(state['name'], 16)
    out += ints(pos, *state['flags'], *state['locals']) + floats(*state['local_floats'])
    out += ints(len(state['jumps'])) + b''.join(state['jumps'])
    out += ints(len(state['args'])) + b''.join(state['args'])
    out += ints(len(state['chars']))
    for char in state['chars']: out += ints(*char)
    out += ints(len(state['choices']))
    for text, *values in state['choices']: out += fixed_text(text, 64) + ints(*values)
    out += ints(s(19), s(1091), 0) + fixed_text(state['bg_path'], 32)
    out += data[STATE+36:STATE+44] + floats(float(s(7)), float(s(8)))
    out += ints(0, state['bg_type'], 0, 0, 0, 0)
    bgm_channel = next((i for i in range(4) if s(963+i) and s(959+i) >= 0), None)
    out += ints(s(959+bgm_channel), s(967+bgm_channel), s(971+bgm_channel)) if bgm_channel is not None else ints(-1, 0, 255)
    out += ints(u32(data, 0x1c050) if u32(data, 0x1c01c) == 131 else 0)
    out += bytes(16)  # Transient transition effect is not part of the stable scene.
    out += ints(struct.unpack_from('<H', data, STATE+538)[0], 2) + b'\0'
    loops = [(i, s(991+i), s(1023+i)) for i in range(16) if s(975+i) and s(1007+i)]
    out += ints(len(loops))
    for channel, sound, volume in loops: out += ints(channel, sound, volume) + b'\1'
    out += bytes([s(409) != 0]) + ints(0) + bytes([s(956) != 0])
    out += ints(0, 0)  # No imported bitmap overlays or transient weather state.
    return bytes(out), state


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True, help='Must not already exist')
    parser.add_argument('--resources', type=Path, required=True)
    parser.add_argument('--quick-slot', type=int, help='Optional empty slot 0..99 for save_Q.sav')
    args = parser.parse_args()
    if args.output.exists() or args.output.resolve() == args.source.resolve():
        parser.error('Output must be a new directory distinct from source')
    if args.quick_slot is not None and not 0 <= args.quick_slot < 100:
        parser.error('Quick slot must be in 0..99')
    scripts = Archive(args.resources / 'ck-gal.pak')
    image_names = set()
    for archive in ('bak.pak', 'grp.pak', 'IC/bak.pak', 'IC/grp.pak'):
        image_names.update(Archive(args.resources / archive).entries)
    sources = []
    for path in sorted(args.source.iterdir()):
        match = re.fullmatch(r'(?:save_|sav)(\d{2})\.sav', path.name)
        if match:
            sources.append((path, 'sav' + match[1] + '.sav'))
        elif path.name == 'save_Q.sav' and args.quick_slot is not None:
            sources.append((path, f'sav{args.quick_slot:02}.sav'))
    if not sources or len({name for _, name in sources}) != len(sources):
        parser.error('No numbered saves or conflicting destination names')
    outputs, manifest = [], []
    for path, name in sources:
        data = path.read_bytes()
        try:
            output, state = convert(data, scripts)
            if state['bg_path'].lower() not in image_names:
                raise ValueError('Saved background is absent from the selected resources')
        except (ValueError, UnicodeError, KeyError, struct.error) as error:
            raise ValueError(f'{path.name}: {error}') from error
        outputs.append((name, output))
        manifest.append(dict(source=path.name, target=name, source_sha256=hashlib.sha256(data).hexdigest(),
                             output_sha256=hashlib.sha256(output).hexdigest(), script=state['script'],
                             position=state['position'], choices=len(state['choices']),
                             global_variables=state['named_flags']))
    args.output.mkdir(parents=True)
    for name, output in outputs: (args.output/name).write_bytes(output)
    (args.output/'conversion-manifest.json').write_text(json.dumps(dict(
        limitations=['Single-script CK-GAL PC v2 / VM 1.00 snapshots only.',
                     'Historical backlog, playing voice, bitmap overlays, transient transitions and weather are not imported.',
                     'System progress and settings are not changed. Runtime validation is still required.'],
        files=manifest), ensure_ascii=False, indent=2))
    print(json.dumps(dict(converted=len(outputs), output=str(args.output), choices=sum(m['choices'] > 0 for m in manifest))))


if __name__ == '__main__':
    main()
