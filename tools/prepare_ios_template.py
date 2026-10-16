#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Add a source-built ARM64 simulator engine to the official Mono debug template."""
import argparse
import subprocess
import tempfile
import zipfile
from pathlib import Path


def prepare(official, arm64, output):
    member = "libgodot.ios.debug.xcframework/ios-arm64_x86_64-simulator/libgodot.a"
    if subprocess.check_output(["lipo", "-archs", str(arm64)], text=True).split() != ["arm64"]:
        raise ValueError("Source-built engine must contain only ARM64 simulator code")
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as temporary, zipfile.ZipFile(official) as source:
        directory = Path(temporary)
        original = directory / "original.a"
        original.write_bytes(source.read(member))
        architectures = subprocess.check_output(["lipo", "-archs", str(original)], text=True).split()
        if architectures != ["x86_64"]:
            raise ValueError("Expected the original x86_64-only simulator engine, found " + str(architectures))
        universal = directory / "universal.a"
        subprocess.run(["lipo", "-create", str(original), str(arm64), "-output", str(universal)], check=True)
        with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as destination:
            for entry in source.infolist():
                if entry.filename == member:
                    destination.write(universal, member)
                else:
                    destination.writestr(entry, source.read(entry))
    print("Created " + str(output))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("official", type=Path)
    parser.add_argument("arm64", type=Path)
    parser.add_argument("--output", type=Path, default=Path("build/templates/ios-mono-arm64.zip"))
    args = parser.parse_args()
    prepare(args.official, args.arm64, args.output)
