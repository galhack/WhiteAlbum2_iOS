#!/usr/bin/env python3

import argparse
import os
import platform as host_platform
import plistlib
import shlex
import shutil
import subprocess
import sys
from pathlib import Path


PLUGIN_VERSION = "v0_6"
FFMPEG_COMPONENTS = ("avcodec", "avformat", "avutil", "swresample", "swscale")
FFMPEG_MAJORS = {
    "avcodec": "61",
    "avformat": "61",
    "avutil": "59",
    "swresample": "5",
    "swscale": "8",
}
PLATFORMS = ("windows", "android", "linux", "macos", "ios")

TOOL_DIR = Path(__file__).resolve().parent
PLUGIN_DIR = TOOL_DIR.parent
REPOSITORY_DIR = PLUGIN_DIR.parents[1]


def fail(message):
    raise SystemExit("error: " + message)


def display_command(command):
    print("+ " + shlex.join(str(item) for item in command))


def run(command, *, cwd=PLUGIN_DIR, env=None, dry_run=False):
    display_command(command)
    if not dry_run:
        subprocess.run([str(item) for item in command], cwd=cwd, env=env, check=True)


def default_arch(platform_name):
    machine = host_platform.machine().lower()
    if platform_name == "android":
        return "arm64"
    if platform_name == "macos":
        return "universal"
    if platform_name == "ios":
        return "universal"
    if platform_name == "linux" and machine in ("aarch64", "arm64"):
        return "arm64"
    return "x86_64"


def abi_for_arch(arch):
    return {"arm64": "arm64-v8a", "x86_64": "x86_64"}.get(arch, arch)


def default_ffmpeg_root(platform_name, arch):
    sdk_root = REPOSITORY_DIR / "thirdparty" / "ffmpeg-sdk"
    if platform_name == "android":
        return sdk_root / "android" / abi_for_arch(arch)
    if platform_name == "macos":
        return sdk_root / "macos" / arch
    if platform_name == "ios":
        return sdk_root / "ios"
    return sdk_root / platform_name / arch


def validate_host(platform_name):
    if platform_name == "windows" and os.name != "nt":
        fail("Windows builds must run on Windows.")
    if platform_name == "linux" and not sys.platform.startswith("linux"):
        fail("Linux builds must run on Linux unless a custom cross toolchain is configured manually.")
    if platform_name in ("macos", "ios") and sys.platform != "darwin":
        fail("macOS and iOS builds must run on macOS with Xcode installed.")


def validate_sdk(root):
    header = root / "include" / "libavformat" / "avformat.h"
    if not header.is_file():
        fail("invalid FFmpeg SDK at {} (missing {})".format(root, header.relative_to(root)))


def copy_file(source, destination, dry_run):
    print("copy {} -> {}".format(source, destination))
    if dry_run:
        return
    if not source.is_file():
        fail("required runtime library is missing: {}".format(source))
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)


def copy_runtime(platform_name, arch, ffmpeg_root, dry_run):
    if platform_name == "windows":
        output = PLUGIN_DIR / "bin"
        for component in FFMPEG_COMPONENTS:
            name = "{}-{}.dll".format(component, FFMPEG_MAJORS[component])
            copy_file(ffmpeg_root / "bin" / name, output / name, dry_run)
        copy_file(ffmpeg_root / "bin" / "libwinpthread-1.dll", output / "libwinpthread-1.dll", dry_run)
    elif platform_name == "android":
        output = PLUGIN_DIR / "bin" / "android" / abi_for_arch(arch)
        for component in FFMPEG_COMPONENTS:
            name = "lib{}.so".format(component)
            copy_file(ffmpeg_root / "lib" / name, output / name, dry_run)
    elif platform_name == "linux":
        output = PLUGIN_DIR / "bin" / "linux" / arch
        for component in FFMPEG_COMPONENTS:
            name = "lib{}.so.{}".format(component, FFMPEG_MAJORS[component])
            copy_file(ffmpeg_root / "lib" / name, output / name, dry_run)
    elif platform_name == "macos":
        output = PLUGIN_DIR / "bin" / "macos"
        for component in FFMPEG_COMPONENTS:
            name = "lib{}.{}.dylib".format(component, FFMPEG_MAJORS[component])
            copy_file(ffmpeg_root / "lib" / name, output / name, dry_run)


def read_macos_install_name(library):
    result = subprocess.run(
        ["otool", "-D", str(library)], check=True, text=True, capture_output=True
    )
    lines = [line.strip() for line in result.stdout.splitlines()[1:] if line.strip()]
    if not lines:
        fail("otool did not report an install name for {}".format(library))
    return lines[0]


def relocate_macos_libraries(targets, dry_run):
    output = PLUGIN_DIR / "bin" / "macos"
    libraries = [
        output / "lib{}.{}.dylib".format(component, FFMPEG_MAJORS[component])
        for component in FFMPEG_COMPONENTS
    ]
    if dry_run:
        for library in libraries:
            print("relocate {} to @rpath/{}".format(library, library.name))
        return

    old_names = {library: read_macos_install_name(library) for library in libraries}
    patched_files = list(libraries)
    for target in targets:
        name = "libwmv_video_{}.macos.{}".format(PLUGIN_VERSION, target)
        patched_files.append(output / (name + ".framework") / name)

    for library in libraries:
        run(["install_name_tool", "-id", "@rpath/" + library.name, library])
    for binary in patched_files:
        for library, old_name in old_names.items():
            run(["install_name_tool", "-change", old_name, "@rpath/" + library.name, binary])


def write_macos_framework_metadata(targets, dry_run):
    for target in targets:
        name = "libwmv_video_{}.macos.{}".format(PLUGIN_VERSION, target)
        path = PLUGIN_DIR / "bin" / "macos" / (name + ".framework") / "Resources" / "Info.plist"
        print("write framework metadata: {}".format(path))
        if dry_run:
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        with path.open("wb") as stream:
            plistlib.dump({
                "CFBundleExecutable": name,
                "CFBundleIdentifier": "org.wa2.wmv." + target.replace("_", "-"),
                "CFBundlePackageType": "FMWK",
                "CFBundleInfoDictionaryVersion": "6.0",
                "CFBundleName": name,
                "CFBundleSupportedPlatforms": ["MacOSX"],
                "CFBundleVersion": "0.6.0",
                "CFBundleShortVersionString": "0.6.0",
            }, stream)


def remove_generated_directory(path, dry_run):
    if path.exists():
        print("remove {}".format(path))
        if not dry_run:
            shutil.rmtree(path)


def create_xcframework(libraries, output, dry_run, headers=None):
    remove_generated_directory(output, dry_run)
    command = ["xcodebuild", "-create-xcframework"]
    for library in libraries:
        command.extend(["-library", str(library)])
        if headers is not None:
            command.extend(["-headers", str(headers[library])])
    command.extend(["-output", str(output)])
    run(command, dry_run=dry_run)


def package_ios(targets, godot_cpp_root, ios_sdk_root, dry_run):
    output = PLUGIN_DIR / "bin" / "ios"
    device_sdk = ios_sdk_root / "device"
    simulator_sdk = ios_sdk_root / "simulator"

    for component in FFMPEG_COMPONENTS:
        device_library = device_sdk / "lib" / "lib{}.a".format(component)
        simulator_library = simulator_sdk / "lib" / "lib{}.a".format(component)
        create_xcframework(
            [device_library, simulator_library],
            output / "lib{}.xcframework".format(component),
            dry_run,
        )

    for target in targets:
        plugin_device = output / "libwmv_video_{}.ios.{}.device.a".format(PLUGIN_VERSION, target)
        plugin_simulator = output / "libwmv_video_{}.ios.{}.simulator.a".format(PLUGIN_VERSION, target)
        create_xcframework(
            [plugin_device, plugin_simulator],
            output / "libwmv_video_{}.ios.{}.xcframework".format(PLUGIN_VERSION, target),
            dry_run,
        )

        godot_device = godot_cpp_root / "bin" / "libgodot-cpp.ios.{}.arm64.a".format(target)
        godot_simulator = godot_cpp_root / "bin" / "libgodot-cpp.ios.{}.universal.simulator.a".format(target)
        create_xcframework(
            [godot_device, godot_simulator],
            output / "libgodot-cpp.ios.{}.xcframework".format(target),
            dry_run,
        )


def scons_command(args, platform_name, target, arch, ffmpeg_root, godot_cpp_root, extra=None):
    command = [
        args.scons,
        "platform={}".format(platform_name),
        "target={}".format(target),
        "arch={}".format(arch),
        "ffmpeg_root={}".format(ffmpeg_root),
        "godot_cpp_dir={}".format(godot_cpp_root),
        "-j{}".format(args.jobs),
    ]
    if extra:
        command.extend(extra)
    return command


def parse_arguments():
    parser = argparse.ArgumentParser(description="Build the WA2 WMV GDExtension.")
    parser.add_argument("--platform", choices=PLATFORMS, required=True)
    parser.add_argument("--target", choices=("debug", "release", "both"), default="both")
    parser.add_argument("--arch", help="Defaults to the primary architecture for the selected platform.")
    parser.add_argument("--ffmpeg-root", type=Path, help="FFmpeg SDK root; iOS expects device/ and simulator/.")
    parser.add_argument("--godot-cpp-root", type=Path, default=REPOSITORY_DIR / "thirdparty" / "godot-cpp")
    parser.add_argument("--android-ndk", type=Path, help="Overrides ANDROID_NDK_ROOT.")
    parser.add_argument("--android-api", type=int, default=24, help="Android minimum API level (default: 24).")
    parser.add_argument("--scons", default=os.environ.get("SCONS", "scons"))
    parser.add_argument("--jobs", type=int, default=max(1, os.cpu_count() or 1))
    parser.add_argument("--no-runtime-copy", action="store_true")
    parser.add_argument("--dry-run", action="store_true", help="Print commands without checking SDKs or building.")
    return parser.parse_args()


def main():
    args = parse_arguments()
    arch = args.arch or default_arch(args.platform)
    ffmpeg_root = (args.ffmpeg_root or default_ffmpeg_root(args.platform, arch)).resolve()
    godot_cpp_root = args.godot_cpp_root.resolve()
    targets = {
        "debug": ["template_debug"],
        "release": ["template_release"],
        "both": ["template_debug", "template_release"],
    }[args.target]

    if not args.dry_run:
        validate_host(args.platform)
        if not (godot_cpp_root / "SConstruct").is_file():
            fail("godot-cpp was not found at {}".format(godot_cpp_root))
        if args.platform == "ios":
            validate_sdk(ffmpeg_root / "device")
            validate_sdk(ffmpeg_root / "simulator")
        else:
            validate_sdk(ffmpeg_root)

    build_env = os.environ.copy()
    if args.platform == "android":
        ndk_root = args.android_ndk or (Path(build_env["ANDROID_NDK_ROOT"]) if "ANDROID_NDK_ROOT" in build_env else None)
        if ndk_root is None and not args.dry_run:
            fail("set ANDROID_NDK_ROOT or pass --android-ndk")
        if ndk_root is not None:
            build_env["ANDROID_NDK_ROOT"] = str(ndk_root.resolve())

    for target in targets:
        if args.platform == "ios":
            run(
                scons_command(
                    args,
                    "ios",
                    target,
                    "arm64",
                    ffmpeg_root / "device",
                    godot_cpp_root,
                    ["ios_simulator=no"],
                ),
                env=build_env,
                dry_run=args.dry_run,
            )
            run(
                scons_command(
                    args,
                    "ios",
                    target,
                    "universal",
                    ffmpeg_root / "simulator",
                    godot_cpp_root,
                    ["ios_simulator=yes"],
                ),
                env=build_env,
                dry_run=args.dry_run,
            )
        else:
            extra = ["android_api_level={}".format(args.android_api)] if args.platform == "android" else None
            run(
                scons_command(args, args.platform, target, arch, ffmpeg_root, godot_cpp_root, extra),
                env=build_env,
                dry_run=args.dry_run,
            )

    if not args.no_runtime_copy:
        if args.platform == "ios":
            package_ios(targets, godot_cpp_root, ffmpeg_root, args.dry_run)
        else:
            copy_runtime(args.platform, arch, ffmpeg_root, args.dry_run)
            if args.platform == "macos":
                relocate_macos_libraries(targets, args.dry_run)
                write_macos_framework_metadata(targets, args.dry_run)

    print("WMV plugin build completed for {} {}.".format(args.platform, ", ".join(targets)))


if __name__ == "__main__":
    main()
