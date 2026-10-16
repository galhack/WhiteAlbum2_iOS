# WA2 Apple TV development build

## Current evidence (2026-10-16)

The port is an ARM64 tvOS application, minimum tvOS 17.0, version 0.3.0/build 4. It is not an iOS IPA with a changed device-family field. `tools/package_ipa.py --platform tvos` validates the application and embedded framework Mach-O platforms as well as the tvOS device family.

The application has been signed with an explicitly authorized development team and installed through Xcode on Apple TV 4K 3rd generation (2022, A15), tvOS 17.6.1. Metal 3.1 initializes on the A15 GPU. The first launch exposed a NativeAOT `__Internal` lookup failure before launcher creation; the app now resolves that bridge against `NativeLibrary.GetMainProgramHandle()`. A second on-device startup log confirms that exception is gone. The user confirms the physical resource menu, gameplay, remote save/load and music work. Physical settings and reading the previous slot after restart are awaiting confirmation. No tvOS simulator runtime is installed, so simulator rendering is not claimed.

The 84 main-game and 14 Special Contents resource files have been transferred to the app's cache. All 98 relative paths and byte sizes match the prepared source, totaling 12,297,152,359 bytes. This comparison is not a content-hash verification. Original archives remain outside the application package and Git.

Sideloadly 0.70.1 currently lists this paired Apple TV with `Mux error (-8)` and a disabled re-pair control. The unsigned IPA is ready at `build/WA2-AppleTV.ipa`, but Sideloadly installation has not passed. Xcode installation does not establish Sideloadly transport or automatic refresh. Keep the same signing team and bundle identifier for an in-place update.

## Controls

- Directional clicks or swipes move the visible blue focus rectangle; Select activates the focused button.
- Select also advances dialogue or skips the active opening flow.
- Menu/Back opens the gameplay pause menu or returns from a submenu.
- Pause provides Resume, Save, Load, Options, Backlog, Manage local saves and Title.
- Save/load use the original slot and page UI. Disabled and hidden choices are excluded from focus navigation.

The desktop rendered integration test dispatches actual Godot GUI input events and covers pause, directional slot movement, load, settings, back and a three-choice fixture with a disabled middle choice. Physical remote delivery uses the engine patch below and still requires device verification.

## Local storage

tvOS does not provide the iOS Documents directory for arbitrary permanent game data. This port changes the engine's `user://` root to Library/Caches. The original game data in `Wa2Res` and `Wa2SC` can be purged by tvOS under storage pressure and would then need reimporting. See [Apple's tvOS storage guidance](https://developer.apple.com/library/archive/documentation/General/Conceptual/AppleTV_PG/iCloudStorage.html).

Progress remains local. The app keeps a compressed, bounded snapshot in its local UserDefaults domain, including launcher preferences, system progress, settings and manual save slots for both editions and languages. There is no cloud entitlement or synchronization. On launch, the entire snapshot is validated before restoring individual cache files. A corrupt backup is retained and further overwriting stops with a visible error.

Apple documents UserDefaults size notifications above 512 KiB and app termination at 1 MiB. This implementation limits the snapshot to 448 KiB and checks the full serialized preferences domain against 480 KiB. New manual saves use a lower 384 KiB budget to reserve room for later progression. If the snapshot would exceed the budget, saving reports an error and retains the previous slot and backup. The local-save management menu can delete selected manual slots with confirmation while preserving system progress. See [Apple UserDefaults size limits](https://developer.apple.com/documentation/foundation/userdefaults/sizelimitexceedednotification).

This deliberately bounded local design cannot promise 100 fully populated slots for every language. Back up important progress before uninstalling. On the physical TV, the native UserDefaults snapshot contains launcher settings, both language system files and a manual slot, occupying 49,354 bytes (49,428-byte preferences domain). After an in-place update and process restart, the cached 112,557-byte manual slot matches the pre-update snapshot byte for byte. Reading that slot through the restarted game UI remains a separate user check; pure archive tests alone are insufficient.

## Reproduce the development toolchain

Other Apple platforms use stable Godot 4.5.1 .NET. Only this isolated build uses Godot revision `017c627991dba852e1d44253825f80a1dfd553f0`, from the tvOS .NET contribution associated with [Godot tvOS platform PR](https://github.com/godotengine/godot/pull/118332) and [tvOS .NET work](https://github.com/stuartcarnie/godot/pull/3). It is a development fork, not a stable upstream tvOS release.

Apply `native/tvos/godot-tvos-17-remote.patch` to that source with `git apply --unidiff-zero /path/to/wa2-godot/native/tvos/godot-tvos-17-remote.patch`. The zero-context patch is checked against the exact pinned source. It lowers the target to tvOS 17, maps remote presses/swipes, enables first-responder input and uses the cache directory. It also enables the simulator Metal path, which has not been built or tested here. Build the engine and editor with the existing SCons toolchain and Xcode SDK:

```sh
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
# Run inside the pinned Godot source tree.
scons platform=tvos target=template_debug arch=arm64 module_mono_enabled=yes -j8
scons platform=macos target=editor arch=arm64 module_mono_enabled=yes vulkan=no -j8
bin/godot.macos.editor.arm64.mono --headless --generate-mono-glue modules/mono/glue
python3 modules/mono/build_scripts/build_assemblies.py --godot-output-dir bin --push-nupkgs-local /path/to/wa2-godot/build/tvos-investigation/nuget
```

Use .NET SDK 9 and ensure `dotnet`, Xcode compiler tools, SCons and Python are on PATH. The matching 4.7.0-dev NuGet packages must be available in the local feed before staging. The working root project keeps its stable Godot SDK reference.

Build FFmpeg 7.1.5 and the existing WMV extension. godot-cpp is pinned to `e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77`. Its tvOS platform uses `native/tvos/godot_cpp_tools/tvos.py`:

```sh
# Run from the WA2 repository.
python3 tools/build_ios_ffmpeg.py /path/to/ffmpeg-7.1.5 --platform tvos --device-only --jobs 8
# Run from addons/wmv_video, using absolute paths for custom_tools and ffmpeg_root.
scons platform=tvos custom_tools=/path/to/wa2-godot/native/tvos/godot_cpp_tools target=template_debug arch=arm64 ffmpeg_root=/path/to/wa2-godot/thirdparty/ffmpeg-sdk/tvos/device -j8
```

Prepare and export from the repository root:

```sh
python3 tools/prepare_tvos.py plugin
python3 tools/prepare_tvos.py template /path/to/pinned-godot
python3 tools/prepare_tvos.py stage
/path/to/pinned-godot/bin/godot.macos.editor.arm64.mono --headless --path build/tvos-project --editor --import
/path/to/pinned-godot/bin/godot.macos.editor.arm64.mono --headless --path build/tvos-project --export-debug tvOS /path/to/wa2-godot/build/tvos/WA2.ipa
python3 tools/prepare_tvos.py project build/tvos
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer xcodebuild -project build/tvos/WA2.xcodeproj -scheme WA2 -configuration Debug -sdk appletvos -destination 'generic/platform=tvOS' -derivedDataPath build/tvos-derived CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO DEVELOPMENT_TEAM= build
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer python3 tools/package_ipa.py build/tvos-derived/Build/Products/Debug-appletvos/WA2.app build/WA2-AppleTV.ipa --platform tvos
```

Re-run `stage` after source edits and `project` after each export. The latter links the local-storage Objective-C++ bridge into the native executable; the C# assembly resolves its exported symbols through the executable handle. NativeAOT upstream trimming warnings remain and require runtime testing of affected code paths.

## Device resource transfer

Use Xcode pairing and an appropriately signed app. With the installed app's exact bundle identifier:

```sh
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer xcrun devicectl device copy to --device DEVICE_UDID --source build/resource-transfer --destination Library/Caches --domain-type appDataContainer --domain-identifier INSTALLED_BUNDLE_ID
```

`resource-transfer` must contain Wa2Res and Wa2SC directly. Do not use `--remove-existing-content`: earlier testing showed that it could clear the broader container scope. Verify the resulting paths and sizes after transfer, and re-query the container after updates rather than retaining an old container UUID.
