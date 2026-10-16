# tvOS tool derived from the pinned MIT-licensed godot-cpp iOS tool.
import codecs
import os
import subprocess
import sys

import common_compiler_flags
from SCons.Variables import BoolVariable


def has_tvos_osxcross():
    return "OSXCROSS_TVOS" in os.environ


def options(opts):
    opts.Add(BoolVariable("tvos_simulator", "Target tvOS Simulator", False))
    opts.Add("tvos_min_version", "Target minimum appletvos/appletvsimulator version", "17.0")
    opts.Add(
        "TVOS_TOOLCHAIN_PATH",
        "Path to tvOS toolchain",
        "/Applications/Xcode.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain",
    )
    opts.Add("TVOS_SDK_PATH", "Path to the tvOS SDK", "")

    if has_tvos_osxcross():
        opts.Add("tvos_triple", "Triple for tvos toolchain", "")


def exists(env):
    return sys.platform == "darwin" or has_tvos_osxcross()


def generate(env):
    if env["arch"] not in ("universal", "arm64", "x86_64"):
        raise ValueError("Only universal, arm64, and x86_64 are supported on tvOS. Exiting.")

    if env["tvos_simulator"]:
        sdk_name = "appletvsimulator"
        env.Append(CCFLAGS=["-mtvos-simulator-version-min=" + env["tvos_min_version"]])
        env.Append(LINKFLAGS=["-mtvos-simulator-version-min=" + env["tvos_min_version"]])
    else:
        sdk_name = "appletvos"
        env.Append(CCFLAGS=["-mtvos-version-min=" + env["tvos_min_version"]])
        env.Append(LINKFLAGS=["-mtvos-version-min=" + env["tvos_min_version"]])

    if sys.platform == "darwin":
        if env["TVOS_SDK_PATH"] == "":
            try:
                env["TVOS_SDK_PATH"] = codecs.utf_8_decode(
                    subprocess.check_output(["xcrun", "--sdk", sdk_name, "--show-sdk-path"]).strip()
                )[0]
            except (subprocess.CalledProcessError, OSError):
                raise ValueError(
                    "Failed to find SDK path while running xcrun --sdk {} --show-sdk-path.".format(sdk_name)
                )

        compiler_path = env["TVOS_TOOLCHAIN_PATH"] + "/usr/bin/"
        env["CC"] = compiler_path + "clang"
        env["CXX"] = compiler_path + "clang++"
        env["AR"] = compiler_path + "ar"
        env["RANLIB"] = compiler_path + "ranlib"
        env["SHLIBSUFFIX"] = ".dylib"
        env["ENV"]["PATH"] = env["TVOS_TOOLCHAIN_PATH"] + "/Developer/usr/bin/:" + env["ENV"]["PATH"]

    else:
        # OSXCross
        compiler_path = "$TVOS_TOOLCHAIN_PATH/usr/bin/${tvos_triple}"
        env["CC"] = compiler_path + "clang"
        env["CXX"] = compiler_path + "clang++"
        env["AR"] = compiler_path + "ar"
        env["RANLIB"] = compiler_path + "ranlib"
        env["SHLIBSUFFIX"] = ".dylib"

        env.Prepend(
            CPPPATH=[
                "$TVOS_SDK_PATH/usr/include",
                "$TVOS_SDK_PATH/System/Library/Frameworks/AudioUnit.framework/Headers",
            ]
        )

        env.Append(CCFLAGS=["-stdlib=libc++"])

        binpath = os.path.join(env["TVOS_TOOLCHAIN_PATH"], "usr", "bin")
        if binpath not in env["ENV"]["PATH"]:
            env.PrependENVPath("PATH", binpath)

    if env["arch"] == "universal":
        if env["tvos_simulator"]:
            env.Append(LINKFLAGS=["-arch", "x86_64", "-arch", "arm64"])
            env.Append(CCFLAGS=["-arch", "x86_64", "-arch", "arm64"])
        else:
            env.Append(LINKFLAGS=["-arch", "arm64"])
            env.Append(CCFLAGS=["-arch", "arm64"])
    else:
        env.Append(LINKFLAGS=["-arch", env["arch"]])
        env.Append(CCFLAGS=["-arch", env["arch"]])

    env.Append(CCFLAGS=["-isysroot", env["TVOS_SDK_PATH"]])
    env.Append(LINKFLAGS=["-isysroot", env["TVOS_SDK_PATH"], "-F" + env["TVOS_SDK_PATH"]])

    env.Append(CPPDEFINES=["TVOS_ENABLED", "UNIX_ENABLED"])

    # Refer to https://github.com/godotengine/godot/blob/master/platform/tvos/detect.py:
    # Disable by default as it makes linking in Xcode very slow.
    if env["lto"] == "auto":
        env["lto"] = "none"

    common_compiler_flags.generate(env)
