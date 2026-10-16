#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Build minimal FFmpeg device and universal simulator SDKs from unpacked source."""
import argparse
import os
import shutil
import subprocess
from pathlib import Path

COMPONENTS = ("avcodec", "avformat", "avutil", "swresample", "swscale")


def run(command, **options):
    subprocess.run([str(x) for x in command], check=True, **options)


def build(source, output, build_root, sdk, arch, jobs):
    directory = build_root / (sdk + "-" + arch)
    directory.mkdir(parents=True, exist_ok=True)
    sdk_path = subprocess.check_output(["xcrun", "--sdk", sdk, "--show-sdk-path"], text=True).strip()
    clang = subprocess.check_output(["xcrun", "--sdk", sdk, "--find", "clang"], text=True).strip()
    minimum = {"iphoneos": "-miphoneos-version-min=15.0", "iphonesimulator": "-mios-simulator-version-min=15.0",
        "appletvos": "-mtvos-version-min=17.0", "appletvsimulator": "-mtvos-simulator-version-min=17.0"}[sdk]
    flags = f"-arch {arch} {minimum}"
    command = [source / "configure", "--prefix=" + str(output), "--target-os=darwin",
        "--arch=" + ("aarch64" if arch == "arm64" else arch), "--enable-cross-compile",
        "--cc=" + clang, "--sysroot=" + sdk_path, "--extra-cflags=" + flags, "--extra-ldflags=" + flags,
        "--disable-everything", "--disable-autodetect", "--disable-programs", "--disable-doc",
        "--disable-debug", "--disable-network", "--disable-avdevice", "--disable-avfilter",
        "--disable-postproc", "--enable-static", "--disable-shared", "--enable-pic",
        "--enable-avcodec", "--enable-avformat", "--enable-avutil", "--enable-swresample",
        "--enable-swscale", "--enable-protocol=file", "--enable-demuxer=asf", "--enable-parser=vc1",
        "--enable-decoder=wmv1,wmv2,wmv3,vc1,wmav1,wmav2,wmapro,wmalossless,wmavoice"]
    if arch == "x86_64":
        command.append("--disable-x86asm")
    run(command, cwd=directory)
    run(["make", "-j" + str(jobs)], cwd=directory)
    run(["make", "install"], cwd=directory)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Clean FFmpeg 7.1.5 source tree")
    parser.add_argument("--platform", choices=("ios", "tvos"), default="ios")
    parser.add_argument("--device-only", action="store_true")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--build-root", type=Path, default=Path("thirdparty/ffmpeg-build"))
    parser.add_argument("--jobs", type=int, default=8)
    args = parser.parse_args()
    source, output, build_root = args.source.resolve(), (args.output or Path("thirdparty/ffmpeg-sdk") / args.platform).resolve(), args.build_root.resolve()
    if not (source / "configure").is_file():
        parser.error("FFmpeg configure script is missing")
    device, simulator_sdk = ("iphoneos", "iphonesimulator") if args.platform == "ios" else ("appletvos", "appletvsimulator")
    build(source, output / "device", build_root, device, "arm64", args.jobs)
    if args.device_only:
        return
    for arch in ("arm64", "x86_64"):
        build(source, output / ("simulator-" + arch), build_root, simulator_sdk, arch, args.jobs)
    simulator = output / "simulator"
    (simulator / "lib").mkdir(parents=True, exist_ok=True)
    shutil.copytree(output / "simulator-arm64" / "include", simulator / "include", dirs_exist_ok=True)
    for component in COMPONENTS:
        name = "lib" + component + ".a"
        run(["lipo", "-create", output / "simulator-arm64" / "lib" / name,
            output / "simulator-x86_64" / "lib" / name, "-output", simulator / "lib" / name])


if __name__ == "__main__":
    main()
