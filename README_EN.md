[简体中文](README.md) | **English**

# WHITE ALBUM 2 · Apple Port

Play *WHITE ALBUM 2* natively on iPhone, iPad, Apple Vision Pro and Apple TV.

This is an **engine port** built on [dorakyuraduang/wa2-godot](https://github.com/dorakyuraduang/wa2-godot), a Godot 4 .NET reimplementation of the original engine. It adds Apple platform support, touch and Siri Remote controls, and performance work.

> **Neither this repository nor the app packages contain any game content.** Scripts, images, audio, movies and fonts are read from your own purchased copy of the PC game. Please buy the game.

## Supported devices

| Device | Minimum OS | How it runs | Verified on hardware |
| --- | --- | --- | --- |
| iPhone | iOS 15 | Native app | iPhone 16 Pro Max · iOS 26.6 |
| iPad | iPadOS 15 | Native app | iPad Pro 13-inch (M5) · iPadOS 26.4 |
| Apple Vision Pro | visionOS (compatible iPad app) | Flat window, gaze + pinch | Vision Pro (1st gen) · visionOS 27.2 |
| Apple TV | tvOS 17 | Separate native tvOS app, Siri Remote | Apple TV 4K (3rd gen, 2022) · tvOS 17.6 |

All builds are native ARM64: C# is compiled ahead of time with NativeAOT and rendering uses Metal. No JIT or jailbreak is needed.

## What you need

- A purchased copy of the **WHITE ALBUM 2 PC game**: the main game (IC/CC/Coda), plus Special Contents if you own it.
- A **Mac** (recommended) or a Windows PC to sign the app and copy the data.
- An **Apple ID**. A free account works, but its signatures expire after 7 days.
- About **12 GB free** on the device: about 7.9 GB for the main game and 3.5 GB for Special Contents.
- [Sideloadly](https://sideloadly.io/) for iPhone/iPad, or [Xcode](https://developer.apple.com/xcode/) for Vision Pro and Apple TV.

## 1. Prepare the game data

Create two folders with **exactly** these names:

- `Wa2Res`: the main game's `.pak` files plus the `IC/` subfolder. For Simplified Chinese, also add the CK-GAL patch files `ck-gal.pak` and `fon.pak`.
- `Wa2SC` (optional): the Special Contents `.pak` files.

The helper script copies only what is needed and never modifies your originals:

```sh
python3 tools/prepare_game_data.py "/path/to/PC install" ~/Desktop/Wa2Res
python3 tools/prepare_game_data.py "/path/to/Special Contents" ~/Desktop/Wa2SC
```

Do not nest folders. `Wa2Res` must directly contain `BGM.PAK`, `IC/` and so on.

## 2. Get the app package

Build the unsigned IPA from source (see [Building from source](#building-from-source)), or download one from this repository's Releases page if provided. The IPA contains no game data.

## 3. Install

**iPhone / iPad (Sideloadly)**

1. Connect and unlock the device, then tap *Trust This Computer*.
2. Drag `WA2-iPhone-iPad.ipa` into Sideloadly, pick the device, enter your Apple ID and click **Start**.
3. On the device, trust the developer certificate in **Settings → General → VPN & Device Management**.
4. On iOS 16 and later, turn on **Developer Mode** in **Settings → Privacy & Security** and restart when prompted.

Free accounts can have 3 sideloaded apps per device, and signatures last 7 days. Enable *Automatic refresh* in Sideloadly to re-sign automatically. Always update with the **same Apple ID**; otherwise the update is installed as a separate app and your data and saves are not visible to it.

**Vision Pro and Apple TV (Xcode)**

Pair the device in Xcode → Devices and Simulators. Then build with your own team and install with `xcrun devicectl device install app`. The exact commands are in the Chinese README and in [APPLE_TV.md](APPLE_TV.md). Vision Pro runs the same app as iPhone/iPad (*Designed for iPad*).

## 4. Import the game data

- **iPhone / iPad**: in Finder, select the device → **Files** tab → **wa2**, and drag in the whole `Wa2Res` and `Wa2SC` folders. On Windows, use File Sharing in the Apple Devices app or iTunes. You can also copy the folders on the device through the Files app (**On My iPhone/iPad → wa2**).
- **Vision Pro / Apple TV**: use `xcrun devicectl device copy to` with `--domain-type appDataContainer`. On Vision Pro the destination is `Documents`; on Apple TV it is `Library/Caches`. Never pass `--remove-existing-content`, which can wipe the app container, saves included.

tvOS may purge cached game data when storage runs low. If that happens, import the data again. Saves and progress are backed up separately and restore automatically.

Launch the app, tap **Main** or **Special Contents**, choose a language and press **Play**.

## Controls

| Action | Touch | Siri Remote | Keyboard / mouse |
| --- | --- | --- | --- |
| Advance text | Tap | Click (Select) | Click / Enter |
| Menu | **≡** button, top right | Menu / Back | Esc |
| Skip | Menu → Skip, or long-press (enable it in Options) | Menu → Skip | Hold Ctrl |
| Auto | Menu → Auto | Play/Pause | Quick bar |
| Backlog | Menu → Backlog, then drag | Menu → Backlog, then up/down | Mouse wheel |
| Close a menu | Tap outside it | Menu | Esc |

On Vision Pro, look at a control and pinch. On Apple TV, pressing Menu on the launcher or the title screen returns to the Home screen. Small buttons get enlarged touch areas that never overlap a neighbouring button's own area.

## Languages and subtitles

- **Japanese**: needs `script.pak` and `fnt.pak` from the original game.
- **Simplified Chinese**: needs CK-GAL's `ck-gal.pak` and `fon.pak`. Special Contents uses `script_cn.pak` and `fon.pak` instead.
- **English and Traditional Chinese**: need a user-supplied translation; see [APPLE_PORT.md](APPLE_PORT.md).

Song and voice subtitles are optional and **not included**, because their lyric translations lack redistribution permission. If you have a subtitle YAML, put it at `<data folder>/subtitles/zh-Hans.yaml`.

## Saves

Saves stay on each device and are kept separately for each edition and language. Chapters unlock as in the original: IC first, then CC, then Coda. Deleting the app deletes its data and saves, so back up `wa2/saves` first.

## Building from source

You need macOS (Apple Silicon), Xcode, Godot 4.5.1 .NET with export templates, .NET SDK 9, Python 3 and SCons.

```sh
dotnet build wa2.csproj && dotnet run --project tests/LaunchOptions/LaunchOptions.csproj
godot --headless --path . --export-release iOS build/ios/WA2.ipa
python3 tools/prepare_ios_project.py build/ios
xcodebuild -project build/ios/WA2.xcodeproj -scheme WA2 -configuration Release -sdk iphoneos \
  -destination 'generic/platform=iOS' -derivedDataPath build/ios-derived \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO DEVELOPMENT_TEAM= build
python3 tools/package_ipa.py build/ios-derived/Build/Products/Release-iphoneos/WA2.app build/WA2-iPhone-iPad.ipa
```

For the FFmpeg plugin and templates, see [APPLE_PORT.md](APPLE_PORT.md) and [addons/wmv_video/BUILDING.md](addons/wmv_video/BUILDING.md). For Apple TV, see [APPLE_TV.md](APPLE_TV.md).

## Credits and licensing

The engine reimplementation is by [dorakyuraduang/wa2-godot](https://github.com/dorakyuraduang/wa2-godot) and its contributors. The project uses Godot (MIT), FFmpeg (LGPL 2.1+), godot-cpp and YamlDotNet; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

There is no single repository license. The porting code listed in [PORTING_CODE.md](PORTING_CODE.md) is Apache-2.0, and `addons/wmv_video` is MIT. For everything else, see [LICENSE](LICENSE) and [ASSET_POLICY.md](ASSET_POLICY.md).

*WHITE ALBUM 2* and all of its content are © Leaf / AQUAPLUS. This project is not affiliated with or endorsed by the rights holders and distributes no game data.
