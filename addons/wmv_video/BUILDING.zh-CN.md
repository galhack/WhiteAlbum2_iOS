# WMV Video 编译说明

插件源码、`SConstruct` 和统一构建工具均位于 `addons/wmv_video`。目标版本为 Godot 4.5、godot-cpp 4.5 分支指定提交，以及 FFmpeg 7.1.5。

当前实际编译验证过 Windows x86_64 和 Android ARM64。Linux、macOS、iOS 与 Android x86_64 已写好构建路径和 GDExtension 打包配置，但没有在对应系统上实际编译，不应视为已经测试通过的成品。

## 依赖目录

默认目录如下，并已由 Git 忽略：

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

每套 FFmpeg SDK 都必须包含 `include/libavformat/avformat.h`。Windows、Android、Linux、macOS 使用共享库；iOS 使用静态库并组装 XCFramework。

## 命令

Windows：

```powershell
addons\wmv_video\tools\build.ps1 --platform windows --target both
addons\wmv_video\tools\build.ps1 --platform android --arch arm64 --android-ndk D:\Android\ndk\25.2.9519653
```

Linux 或 macOS：

```bash
bash addons/wmv_video/tools/build.sh --platform linux --arch x86_64
bash addons/wmv_video/tools/build.sh --platform macos --arch universal
bash addons/wmv_video/tools/build.sh --platform ios
```

依赖不在默认目录时可传入 `--godot-cpp-root` 与 `--ffmpeg-root`。`--target` 可取 `debug`、`release`、`both`。`--dry-run` 只打印命令，不检查 SDK、不编译、不复制文件。

## 平台说明

- Windows：生成 x86_64 DLL，并复制 FFmpeg 7 DLL 与 `libwinpthread-1.dll`。
- Android：支持 `arm64` 和 `x86_64`；需设置 `ANDROID_NDK_ROOT` 或传入 `--android-ndk`。最低 API 默认为 24，可用 `--android-api` 覆盖。
- Linux：支持 x86_64、ARM64；FFmpeg SONAME 库放在插件旁，使用 `$ORIGIN` RPATH。
- macOS：要求 FFmpeg universal dylib；生成 framework，并自动修正 `@rpath`。
- iOS：分别构建设备和模拟器静态库，再用 `xcodebuild` 生成插件、godot-cpp 和 FFmpeg XCFramework。

iOS 会静态链接 FFmpeg。发布者必须满足 LGPL 的重新链接要求，例如同时提供应用目标文件和链接说明，或者取得其他许可证。只公开插件源码并不足以自动满足 iOS 应用的静态链接义务。

最终可分发运行库放在 `addons/wmv_video/bin`，可以连同许可证一起提交。不要提交 `thirdparty`、SDK、SCons 中间文件、原始 iOS `.a`、签名证书、APK/AAB、原游戏资源或编辑器缓存。
