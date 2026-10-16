#!/usr/bin/env python3
"""Make an ARM64 Godot template from the official universal macOS template."""
# SPDX-License-Identifier: Apache-2.0
import argparse
import subprocess
import tempfile
import zipfile
from pathlib import Path


def prepare(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as temporary:
        with zipfile.ZipFile(source) as original, zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as output:
            for info in original.infolist():
                data = original.read(info)
                if info.filename.endswith(".universal"):
                    binary = Path(temporary) / "universal"
                    thin = Path(temporary) / "arm64"
                    binary.write_bytes(data)
                    subprocess.run(["lipo", str(binary), "-thin", "arm64", "-output", str(thin)], check=True)
                    data = thin.read_bytes()
                    info.filename = info.filename.removesuffix(".universal") + ".arm64"
                output.writestr(info, data)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path, nargs="?", default=Path("build/templates/macos-arm64.zip"))
    args = parser.parse_args()
    prepare(args.source, args.destination)
