# SPDX-License-Identifier: Apache-2.0
import importlib.util
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location("prepare_game_data", Path(__file__).parents[1] / "tools/prepare_game_data.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class TransferTests(unittest.TestCase):
    def test_copies_resources_and_preserves_ic_without_windows_programs(self):
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source"
            (source / "IC").mkdir(parents=True)
            (source / "IC" / "VOICE.PAK").write_bytes(b"fixture voice")
            (source / "script.pak").write_bytes(b"fixture script")
            (source / "game.exe").write_bytes(b"not a resource")
            destination = Path(temporary) / "Wa2Res"
            module.prepare(source, destination)
            self.assertEqual(["IC/VOICE.PAK", "script.pak"], sorted(str(p.relative_to(destination)) for p in destination.rglob("*") if p.is_file()))

    def test_refuses_existing_destination(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            with self.assertRaises(FileExistsError):
                module.prepare(directory, directory)


if __name__ == "__main__":
    unittest.main()
