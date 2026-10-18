# SPDX-License-Identifier: Apache-2.0
import importlib.util
from pathlib import Path
import struct
import unittest

SPEC = importlib.util.spec_from_file_location('convert_pc_saves', Path(__file__).parents[1] / 'tools/convert_pc_saves.py')

class ConversionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.converter = importlib.util.module_from_spec(SPEC)
        SPEC.loader.exec_module(cls.converter)

    def pc_fixture(self):
        c = self.converter
        data = bytearray(c.SIZE)
        struct.pack_into('<I', data, 0, 2)
        struct.pack_into('<8H', data, 8, 2025, 5, 6, 17, 2, 14, 0, 432)
        data[0x134d4:0x134d8] = b'1.00'
        struct.pack_into('<I', data, 0x134d8, 4)
        data[0x203d8:0x203e0] = b'2001\0\0\0\0'
        struct.pack_into('<I', data, 0x1c01c, 131)
        struct.pack_into('<I', data, 0x1c18c, 16)
        offset = 0x54cb4
        struct.pack_into('<I', data, offset, len(c.EXPECTED_FLAGS))
        offset += 4
        for index, name in enumerate(c.EXPECTED_FLAGS):
            raw = name.encode('cp932') + b'\0'
            struct.pack_into('<I', data, offset, len(raw))
            offset += 4
            data[offset:offset+len(raw)] = raw
            offset += len(raw)
            data[offset] = 3
            struct.pack_into('<i', data, offset+1, index)
            offset += 5
        self.assertEqual(offset, c.SIZE)
        return data

    def test_preserves_all_named_route_flags(self):
        state = self.converter.parse_pc(self.pc_fixture())
        self.assertEqual(state['flags'], list(range(29)))

    def test_voice_chapter_uses_voice_state_not_script_index(self):
        c = self.converter
        data = self.pc_fixture()
        struct.pack_into('<i', data, c.STATE+1091*4, 1012)
        struct.pack_into('<i', data, c.STATE+948*4, 0)
        class Scripts:
            def read(self, name): return b'LSCR'+bytes(4)+struct.pack('<II', 4, 131)+bytes(16)
        output, state = c.convert(data, Scripts())
        # Fixed prefix, variables, empty jump/operand/character/choice lists.
        native_time_mode = 0x1b020+8+16+1024+16+4+29*4+26*4+26*4+4*4
        self.assertEqual(struct.unpack_from('<i', output, native_time_mode+4)[0], 1012)

    def test_background_uses_saved_day_night_variant(self):
        c = self.converter
        data = self.pc_fixture()
        struct.pack_into('<i', data, c.STATE+6*4, 20490)
        struct.pack_into('<i', data, c.STATE+19*4, 3)
        self.assertEqual(c.parse_pc(data)['bg_path'], 'B204903.tga')

    def test_thumbnail_keeps_top_row_at_top(self):
        data = bytearray(348112)
        data[0x58:0x5c] = bytes([3, 2, 1, 255])
        self.assertEqual(self.converter.thumbnail(data)[:3], bytes([1, 2, 3]))

    def test_rejects_nonzero_nested_call_frame(self):
        data = self.pc_fixture()
        struct.pack_into('<i', data, 0x1d594+4, 9)
        with self.assertRaisesRegex(ValueError, 'nested local'):
            self.converter.parse_pc(data)

    def test_rejects_changed_registry_order(self):
        data = self.pc_fixture()
        data[0x54cbc:0x54cc2] = b'WRONG!'
        with self.assertRaisesRegex(ValueError, 'registry'):
            self.converter.parse_pc(data)

    def test_rejects_native_save_and_truncated_pc_save(self):
        for data in [b'\0' * 113409, b'\x02\0\0\0' + b'\0' * 100]:
            with self.subTest(size=len(data)), self.assertRaises(ValueError):
                self.converter.parse_pc(data)

    def test_native_argument_keeps_selection_return_value(self):
        raw = struct.pack('<5I', 0, 0, 0, 0, 0)
        self.assertEqual(struct.unpack('<5I', self.converter.convert_argument(raw)), (5, 3, 0, 0, 0))

    def test_rejects_unresolved_pointer_arguments(self):
        with self.assertRaises(ValueError):
            self.converter.convert_argument(struct.pack('<5I', 5, 5, 0x123456, 0, 0))

    def test_local_variables_use_native_frame_stride(self):
        data = bytearray(348112)
        struct.pack_into('<i', data, 0x1d594 + 64, 37)
        values = self.converter.local_values(data, 0x1d594, 'i')
        self.assertEqual(values[1], 37)

    def test_thumbnail_converts_bgra_to_native_rgb(self):
        data = bytearray(348112)
        for i in range(128 * 128): data[0x58+i*4:0x5c+i*4] = bytes([3, 2, 1, 255])
        self.assertEqual(self.converter.thumbnail(data), bytes([1, 2, 3]) * (256 * 144))

if __name__ == '__main__': unittest.main()
