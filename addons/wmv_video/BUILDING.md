# Building WMV Video

## Status

This repository targets Godot 4.5 and FFmpeg 7.1.5. The build files define the following matrix:

| Platform | Architectures | Output | Validation status |
| --- | --- | --- | --- |
| Windows | x86_64 | DLL | Built and tested |
| Android | ARM64, x86_64 | SO | ARM64 built and APK packaging verified; no device test |
| Linux | x86_64, ARM64 | SO | Configuration only |
| macOS | ARM64 (built); universal (configuration) | Framework | ARM64 compiled, original media decoded, app export verified |
| iOS | ARM64 device, universal simulator | XCFramework | Configuration only |

Linux, macOS, and iOS must be built on their native host systems. macOS ARM64 was built during the Apple port; iOS still requires validation.

## Prerequisites

- Python 3.9 or newer and SCons 4.
- Godot 4.5.
- `godot-cpp` commit `27d9dd23c83871e0619fca5dc2cddfbfd69e926a` from the Godot 4.5 branch.
- An FFmpeg 7.1.5 development SDK for every target architecture.
- MSVC on Windows, Android NDK for Android, GCC or Clang on Linux, and Xcode on macOS/iOS.

The default ignored dependency layout is:

```text
thirdparty/
  godot-cpp/
  ffmpeg-sdk/
    windows/x86_64/{include,lib,bin}
    android/arm64-v8a/{include,lib}
    android/x86_64/{include,lib}
    linux/x86_64/{include,lib}
    linux/arm64/{include,lib}
    macos/universal/{include,lib}
    ios/device/{include,lib}
    ios/simulator/{include,lib}
```

All FFmpeg SDKs must contain `include/libavformat/avformat.h`. Desktop and Android SDKs use shared libraries. iOS uses static libraries because Godot packages iOS GDExtensions as XCFrameworks.

## Unified build tool

Run commands from the WA2 repository root. `--target both` is the default.

Windows PowerShell:

```powershell
addons\wmv_video\tools\build.ps1 --platform windows --target both
addons\wmv_video\tools\build.ps1 --platform android --arch arm64 --android-ndk D:\Android\ndk\25.2.9519653
```

Linux or macOS:

```bash
bash addons/wmv_video/tools/build.sh --platform linux --arch x86_64
bash addons/wmv_video/tools/build.sh --platform macos --arch universal
bash addons/wmv_video/tools/build.sh --platform ios
```

Use explicit SDK paths when the default layout is not suitable:

```bash
python3 addons/wmv_video/tools/build.py \
  --platform linux --target release --arch arm64 \
  --godot-cpp-root /opt/godot-cpp \
  --ffmpeg-root /opt/ffmpeg-linux-arm64
```

`--dry-run` prints every SCons, copy, `install_name_tool`, and `xcodebuild` command without checking SDKs or changing files. `--no-runtime-copy` builds only the plugin library.

## Platform details

### Windows

The FFmpeg SDK must provide the five import libraries and these runtime files in `bin`: `avcodec-61.dll`, `avformat-61.dll`, `avutil-59.dll`, `swresample-5.dll`, `swscale-8.dll`, and `libwinpthread-1.dll`. The tool copies them into the plugin `bin` directory.

### Android

Set `ANDROID_NDK_ROOT` or pass `--android-ndk`. Godot architecture `arm64` maps to Android ABI directory `arm64-v8a`. The minimum API defaults to 24; override it with `--android-api` when required.

### Linux

The tool copies the FFmpeg SONAME libraries beside the GDExtension and links the plugin with `$ORIGIN` RPATH. The expected FFmpeg 7 names are `libavcodec.so.61`, `libavformat.so.61`, `libavutil.so.59`, `libswresample.so.5`, and `libswscale.so.8`.

### macOS

For the current Apple Silicon port, use `--arch arm64` and put its FFmpeg SDK in `thirdparty/ffmpeg-sdk/macos/arm64`. The tested godot-cpp revision is `e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77` from branch 4.5. FFmpeg 7.1.5 was configured with the flags below plus `--enable-shared --disable-static` and the SDK prefix. Both debug and release plugin binaries were built. Framework metadata is generated before export. Use the macOS export preset's system codesign option to sign nested frameworks correctly.

Use universal FFmpeg dylibs containing x86_64 and ARM64 slices. The plugin is emitted as a framework. The tool copies FFmpeg dylibs, rewrites their install names to `@rpath`, and rewrites the plugin and FFmpeg cross-dependencies with `install_name_tool`. Godot exports these dependencies into `Contents/Frameworks`. Signing and notarization happen as part of the final application workflow.

### iOS

The tool performs two builds for each target: ARM64 device and universal simulator. It then uses `xcodebuild -create-xcframework` for the plugin, godot-cpp, and each FFmpeg static library. The resulting XCFrameworks match the entries in `wmv_video.gdextension`.

iOS static linking has additional LGPL requirements. A distributor must provide a practical relinking mechanism, such as the application object files and link instructions, or obtain a different FFmpeg license. Merely publishing this plugin source is not sufficient for an iOS application that statically links FFmpeg.

## FFmpeg configuration

The checked-in binaries use a minimal LGPL build with GPL and nonfree features disabled:

```text
--disable-everything --disable-autodetect
--disable-programs --disable-doc --disable-debug --disable-network
--disable-avdevice --disable-avfilter --disable-postproc
--enable-avcodec --enable-avformat --enable-avutil
--enable-swresample --enable-swscale
--enable-protocol=file --enable-demuxer=asf --enable-parser=vc1
--enable-decoder=wmv1,wmv2,wmv3,vc1
--enable-decoder=wmav1,wmav2,wmapro,wmalossless,wmavoice
```

Use shared FFmpeg builds for Windows, Android, Linux, and macOS. Use static builds for the iOS SDK directories. Keep the exact FFmpeg source revision, configure command, and license text with distributed binaries.

## Git policy

Commit plugin source, scripts, documentation, licenses, `.gdextension`, and final runtime files under `addons/wmv_video/bin`. Do not commit `thirdparty`, SCons intermediates, raw iOS `.a` files, SDKs, Android Gradle output, application packages, signing material, original game media, or editor caches. Final `.xcframework` contents are explicitly allowed by `.gitignore`.
