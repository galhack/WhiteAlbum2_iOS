#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Validate and package an unsigned ARM64 iOS/tvOS .app for re-signing."""
import argparse
import os
import plistlib
import subprocess
import zipfile
from pathlib import Path


def validate(app, platform="ios"):
    with (app / "Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    executable = info.get("CFBundleExecutable", "")
    if not executable or Path(executable).name != executable or not (app / executable).is_file():
        raise ValueError("Application executable is missing or invalid")
    families = set(info.get("UIDeviceFamily", []))
    if platform == "tvos":
        if families != {3}:
            raise ValueError("tvOS application must target Apple TV only")
    else:
        if not {1, 2}.issubset(families):
            raise ValueError("Application must support both iPhone and iPad")
        if not info.get("UIFileSharingEnabled") or not info.get("LSSupportsOpeningDocumentsInPlace"):
            raise ValueError("Documents import is not enabled in Info.plist")
    scenes = info.get("UIApplicationSceneManifest", {}).get("UISceneConfigurations", {})
    if not scenes.get("UIWindowSceneSessionRoleApplication"):
        raise ValueError("UIKit scene lifecycle is missing; run prepare_ios_project.py before building")
    architectures = subprocess.check_output(["lipo", "-archs", str(app / executable)], text=True).split()
    if architectures != ["arm64"]:
        raise ValueError("Expected an ARM64 device application, found: " + str(architectures))
    build = subprocess.check_output(["xcrun", "vtool", "-show-build", str(app / executable)], text=True)
    expected = "TVOS" if platform == "tvos" else "IOS"
    if f"platform {expected}\n" not in build or "SIMULATOR" in build:
        raise ValueError("Application does not target the requested device platform: " + expected)
    for framework in (app / "Frameworks").glob("*.framework"):
        binary = framework / framework.stem
        if not binary.is_file():
            raise ValueError("Framework executable is missing: " + str(binary))
        target = subprocess.check_output(["xcrun", "vtool", "-show-build", str(binary)], text=True)
        if f"platform {expected}\n" not in target or "SIMULATOR" in target:
            raise ValueError("Embedded framework targets the wrong platform: " + str(binary))
    if any(app.rglob("*.pak")):
        raise ValueError("Original game archives must be imported separately")
    return info


def package(app, destination, platform="ios"):
    info = validate(app, platform)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(app.rglob("*")):
            name = str(Path("Payload") / app.name / path.relative_to(app))
            if path.is_symlink():
                entry = zipfile.ZipInfo(name)
                entry.create_system = 3
                entry.external_attr = path.lstat().st_mode << 16
                archive.writestr(entry, os.readlink(path))
            elif path.is_file():
                archive.write(path, name)
    with zipfile.ZipFile(destination) as archive:
        damaged = archive.testzip()
        if damaged:
            raise ValueError("IPA integrity check failed: " + damaged)
    print(f"Created {destination} ({destination.stat().st_size} bytes), bundle {info['CFBundleIdentifier']}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("app", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--platform", choices=("ios", "tvos"), default="ios")
    args = parser.parse_args()
    package(args.app, args.output, args.platform)
