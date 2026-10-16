#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Add a single-window scene lifecycle adapter to Godot 4.5's exported project."""
import argparse
import plistlib
import shutil
from pathlib import Path


def prepare(directory):
    projects = list(directory.glob("*.xcodeproj"))
    if len(projects) != 1:
        raise ValueError("Expected one exported Xcode project")
    project = projects[0]
    application = directory / project.stem
    source = application / "dummy.cpp"
    target = application / "dummy.mm"
    if source.exists():
        source.rename(target)
    if not target.is_file():
        raise FileNotFoundError("Exported Godot plugin source is missing")
    text = target.read_text()
    include = '#include "WA2SceneDelegate.mm"'
    if include not in text:
        target.write_text(text + "\n" + include + "\n")
    shutil.copy2(Path(__file__).parents[1] / "native/ios/WA2SceneDelegate.mm", application / "WA2SceneDelegate.mm")
    (application / "WA2CloudBridge.mm").unlink(missing_ok=True)
    pbx = project / "project.pbxproj"
    text = pbx.read_text().replace("dummy.cpp", "dummy.mm")
    text = text.replace("lastKnownFileType = sourcecode.cpp.cpp;", "lastKnownFileType = sourcecode.cpp.objcpp;")
    pbx.write_text(text)
    plist = application / (project.stem + "-Info.plist")
    with plist.open("rb") as stream:
        info = plistlib.load(stream)
    info["UIFileSharingEnabled"] = True
    info["LSSupportsOpeningDocumentsInPlace"] = True
    info["UIApplicationSceneManifest"] = {
        "UIApplicationSupportsMultipleScenes": False,
        "UISceneConfigurations": {"UIWindowSceneSessionRoleApplication": [{
            "UISceneConfigurationName": "WA2", "UISceneDelegateClassName": "WA2SceneDelegate"}]}}
    with plist.open("wb") as stream:
        plistlib.dump(info, stream)
    print("Prepared UIKit scene lifecycle in " + str(project))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    prepare(parser.parse_args().directory)
