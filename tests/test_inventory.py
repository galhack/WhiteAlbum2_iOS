# SPDX-License-Identifier: Apache-2.0
import importlib.util
import struct
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location("inventory", Path(__file__).parents[1] / "tools/inventory.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ArchiveIndexTests(unittest.TestCase):
    def read_fixture(self, data):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "fixture.pak"
            path.write_bytes(data)
            return module.archive_index(path)

    def test_reads_names_without_reading_content(self):
        header = b"KCAP" + bytes(8) + struct.pack("<I", 1)
        record = struct.pack("<I24s8xII", 0, b"1001.bnr", 60, 0)
        self.assertEqual(["1001.bnr"], self.read_fixture(header + record))

    def test_rejects_truncated_index(self):
        with self.assertRaisesRegex(ValueError, "Truncated"):
            self.read_fixture(b"KCAP" + bytes(8) + struct.pack("<I", 1))

    def test_rejects_entry_outside_archive(self):
        header = b"KCAP" + bytes(8) + struct.pack("<I", 1)
        record = struct.pack("<I24s8xII", 0, b"1001.bnr", 60, 1)
        with self.assertRaisesRegex(ValueError, "exceeds"):
            self.read_fixture(header + record)


if __name__ == "__main__":
    unittest.main()
