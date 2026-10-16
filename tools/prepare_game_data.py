#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Prepare a local Finder file-sharing folder from user-owned game resources."""
import argparse
import shutil
import subprocess
import sys
from pathlib import Path


def copy_resource(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    if sys.platform == "darwin":
        result = subprocess.run(["/bin/cp", "-c", str(source), str(destination)], capture_output=True)
        if result.returncode == 0:
            return
    shutil.copy2(source, destination)


def prepare(source, destination, subtitles=None):
    if destination.exists():
        raise FileExistsError("Refusing to replace existing game data: " + str(destination))
    if not source.is_dir():
        raise FileNotFoundError("Game source folder does not exist: " + str(source))
    resources = []
    for path in sorted(source.rglob("*")):
        if not path.is_file() or path.is_symlink():
            continue
        relative = path.relative_to(source)
        top, suffix = relative.parts[0].lower(), path.suffix.lower()
        if (suffix == ".pak" and (len(relative.parts) == 1 or top == "ic")) or (top == "grp" and suffix == ".tga") or (top == "languages" and suffix == ".cfg") or (top == "subtitles" and suffix == ".yaml"):
            resources.append(path)
    if not resources:
        raise ValueError("No game archives were found in " + str(source))
    if subtitles is not None and not subtitles.is_file():
        raise FileNotFoundError("Subtitle YAML does not exist: " + str(subtitles))
    destination.mkdir(parents=True)
    for path in resources:
        copy_resource(path, destination / path.relative_to(source))
    if subtitles is not None:
        copy_resource(subtitles, destination / "subtitles" / "zh-Hans.yaml")
    print(f"Prepared {destination}: {len(resources)} resource files. Original files are unchanged.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--chinese-subtitles", type=Path)
    args = parser.parse_args()
    prepare(args.source.resolve(), args.destination.resolve(), args.chinese_subtitles)
