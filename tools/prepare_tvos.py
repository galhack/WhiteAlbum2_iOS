#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Prepare the pinned tvOS toolchain, isolated Godot project and native export."""
import argparse
import plistlib
import shutil
import subprocess
import zipfile
from pathlib import Path

REVISION = "017c627991dba852e1d44253825f80a1dfd553f0"
ROOT = Path(__file__).resolve().parents[1]


def device_framework(library, destination):
    if destination.exists():
        shutil.rmtree(destination)
    subprocess.run(["xcodebuild", "-create-xcframework", "-library", str(library.resolve()),
                    "-output", str(destination.resolve())], check=True)


def template(engine):
    engine = engine.resolve()
    output = ROOT / "build/templates/tvos-mono-arm64.zip"
    directory = ROOT / "build/tvos-template"
    if directory.exists():
        shutil.rmtree(directory)
    shutil.copytree(engine / "misc/dist/apple_embedded_xcode", directory)
    for framework in directory.glob("*.xcframework"):
        shutil.rmtree(framework)
    device_framework(engine / "bin/libgodot.tvos.template_debug.arm64.a", directory / "libgodot.tvos.debug.xcframework")
    release = engine / "bin/libgodot.tvos.template_release.arm64.a"
    if release.is_file():
        device_framework(release, directory / "libgodot.tvos.release.xcframework")
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
        for path in directory.rglob("*"):
            if path.is_file():
                archive.write(path, path.relative_to(directory))
    print("Prepared tvOS template:", output, "(debug + release)" if release.is_file() else "(debug only)")


def plugin():
    output = ROOT / "addons/wmv_video/bin/tvos"
    for target in ("debug", "release"):
        library = output / f"libwmv_video_v0_6.tvos.template_{target}.device.a"
        if not library.is_file():
            continue
        device_framework(library, output / f"libwmv_video_v0_6.tvos.template_{target}.xcframework")
        device_framework(ROOT / f"thirdparty/godot-cpp/bin/libgodot-cpp.tvos.template_{target}.arm64.a", output / f"libgodot-cpp.tvos.template_{target}.xcframework")
    for name in ("avcodec", "avformat", "avutil", "swresample", "swscale"):
        device_framework(ROOT / "thirdparty/ffmpeg-sdk/tvos/device/lib" / ("lib" + name + ".a"), output / ("lib" + name + ".xcframework"))


def stage():
    destination = ROOT / "build/tvos-project"
    destination.mkdir(parents=True, exist_ok=True)
    for name in ("assets", "scene", "shader", "script", "addons"):
        shutil.copytree(ROOT / name, destination / name, dirs_exist_ok=True,
                        ignore=shutil.ignore_patterns("bin", "obj", "__pycache__"))
    for name in ("project.godot", "main.tscn", "wa2.csproj", "wa2.sln", "icon.svg", "default_bus_layout.tres"):
        shutil.copy2(ROOT / name, destination / name)
    binary = destination / "addons/wmv_video/bin"
    if not binary.exists():
        binary.symlink_to(ROOT / "addons/wmv_video/bin", target_is_directory=True)
    project = destination / "wa2.csproj"
    project.write_text(project.read_text().replace("Godot.NET.Sdk/4.5.1", "Godot.NET.Sdk/4.7.0-dev"))
    (destination / "NuGet.Config").write_text(f'''<configuration><packageSources><clear/>
<add key="pinned-tvos" value="{ROOT}/build/tvos-investigation/nuget"/>
<add key="nuget.org" value="https://api.nuget.org/v3/index.json"/>
</packageSources></configuration>''')
    extension = destination / "addons/wmv_video/wmv_video.gdextension"
    text = extension.read_text()
    for target in ("debug", "release"):
        text = text.replace('[libraries]', f'[libraries]\ntvos.{target} = "res://addons/wmv_video/bin/tvos/libwmv_video_v0_6.tvos.template_{target}.xcframework"', 1)
        dependencies = [f"libgodot-cpp.tvos.template_{target}"] + ["lib" + n for n in ("avcodec", "avformat", "avutil", "swresample", "swscale")]
        text += f'\ntvos.{target} = {{\n' + ',\n'.join(f'    "res://addons/wmv_video/bin/tvos/{n}.xcframework": ""' for n in dependencies) + '\n}\n'
    extension.write_text(text)
    (destination / "export_presets.cfg").write_text(f'''[preset.0]
name="tvOS"
platform="tvOS"
runnable=true
advanced_options=true
export_filter="all_resources"
include_filter="*.map,*.yaml"
exclude_filter="tests/*,build/*,thirdparty/*,*.pak,*.exe,*.dll,assets/grp/*,assets/fonts/cn/*,assets/fonts/jp/*,assets/se/*,assets/app_icon/*.svg"
export_path="{ROOT}/build/tvos/WA2.ipa"
script_export_mode=2

[preset.0.options]
custom_template/debug="{ROOT}/build/templates/tvos-mono-arm64.zip"
custom_template/release="{ROOT}/build/templates/tvos-mono-arm64.zip"
icons/tvos_small_app_icon="res://assets/app_icon/tvos_small_400x240.png"
icons/tvos_large_app_icon="res://assets/app_icon/tvos_large_1280x768.png"
icons/tvos_top_shelf="res://assets/app_icon/tvos_top_shelf_1920x720.png"
icons/tvos_top_shelf_wide="res://assets/app_icon/tvos_top_shelf_wide_2320x720.png"
shader_baker/enabled=true
architectures/arm64=true
application/app_store_team_id="LOCAL00000"
application/bundle_identifier="org.local.wa2.tv"
application/short_version="0.4.0"
application/version="5"
application/min_tvos_version="17.0"
application/export_project_only=true
application/delete_old_export_files_unconditionally=true
dotnet/include_scripts_content=false
dotnet/include_debug_symbols=true
''')
    # Only this isolated project uses the development engine; other Apple targets stay on 4.5.1.
    settings = destination / "project.godot"
    settings.write_text(settings.read_text().replace('[rendering]', '[rendering]\nrendering_device/driver.tvos="metal"'))
    (ROOT / "build/tvos").mkdir(exist_ok=True)
    print("Staged tvOS project:", destination)


def project(directory):
    projects = list(directory.glob("*.xcodeproj"))
    if len(projects) != 1:
        raise ValueError("Expected one exported tvOS project")
    application = directory / projects[0].stem
    source, target = application / "dummy.cpp", application / "dummy.mm"
    if source.exists():
        source.rename(target)
    include = '#include "WA2LocalStorage.mm"'
    text = target.read_text()
    if include not in text:
        target.write_text(text + "\n" + include + "\n")
    shutil.copy2(ROOT / "native/tvos/WA2LocalStorage.mm", application / "WA2LocalStorage.mm")
    pbx = projects[0] / "project.pbxproj"
    pbx.write_text(pbx.read_text().replace("dummy.cpp", "dummy.mm").replace("sourcecode.cpp.cpp", "sourcecode.cpp.objcpp"))
    info = application / (projects[0].stem + "-Info.plist")
    with info.open("rb") as stream:
        data = plistlib.load(stream)
    for key in ("UIFileSharingEnabled", "LSSupportsOpeningDocumentsInPlace", "CFBundleIcons~ipad",
                "UISupportedInterfaceOrientations~ipad", "NSCameraUsageDescription", "NSMicrophoneUsageDescription", "NSPhotoLibraryUsageDescription"):
        data.pop(key, None)
    data["UIRequiredDeviceCapabilities"] = ["arm64", "metal"]
    with info.open("wb") as stream:
        plistlib.dump(data, stream)
    print("Prepared tvOS local storage bridge:", projects[0])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("template").add_argument("engine", type=Path)
    commands.add_parser("plugin")
    commands.add_parser("stage")
    commands.add_parser("project").add_argument("directory", type=Path)
    args = parser.parse_args()
    if args.command == "template": template(args.engine)
    elif args.command == "plugin": plugin()
    elif args.command == "stage": stage()
    else: project(args.directory)
