[简体中文](README.md) | **English**

# WHITE ALBUM 2 · Apple Port

Play *WHITE ALBUM 2* natively on iPhone, iPad, Apple Vision Pro, Apple TV and Mac.

This is an **engine port** built on [dorakyuraduang/wa2-godot](https://github.com/dorakyuraduang/wa2-godot), a Godot 4 .NET reimplementation of the original engine. It adds Apple platform support, touch and Siri Remote controls, and performance work.

> **The app packages contain no original game resources.** Scripts, images, audio, movies and game fonts are read from your purchased copy of the PC game. Version 0.4.1 bundles separate third-party Chinese subtitle data; see [subtitle provenance and licensing](SUBTITLE_NOTICE.md).

## Supported devices

| Device | Minimum OS | How it runs | Verified on hardware |
| --- | --- | --- | --- |
| iPhone | iOS 15 | Native app | iPhone 16 Pro Max · iOS 26.6 |
| iPad | iPadOS 15 | Native app | iPad Pro 13-inch (M5) · iPadOS 26.4 |
| Apple Vision Pro | visionOS (compatible iPad app) | Flat window, gaze + pinch | Vision Pro (1st gen) · visionOS 27.2 |
| Apple TV | tvOS 17 | Separate native tvOS app, Siri Remote | Apple TV 4K (3rd gen, 2022) · tvOS 17.6 |
| Mac | macOS 12, Apple silicon | Native app, windowed or full screen | MacBook Pro (M1 Max) · macOS 26.6 |

All builds target Apple silicon/ARM64. iPhone, iPad, Vision Pro and Apple TV use NativeAOT and need no AltJIT or jailbreak. The Mac app includes its .NET runtime and uses the OpenGL compatibility renderer.

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

Current mobile release: [v0.4.1 · Build 9](https://github.com/galhack/WhiteAlbum2_iOS/releases/tag/v0.4.1-build9), for iPhone, iPad and Vision Pro. Mac remains on Build 8. Apple TV updates are paused until the maintainer explicitly announces otherwise.

Download from [Releases](https://github.com/galhack/WhiteAlbum2_iOS/releases):

- [WA2-iPhone-iPad.ipa](https://github.com/galhack/WhiteAlbum2_iOS/releases/latest/download/WA2-iPhone-iPad.ipa): iPhone/iPad, unsigned. Sign it with your own Apple ID in Sideloadly.
- [WA2-AppleTV.ipa](https://github.com/galhack/WhiteAlbum2_iOS/releases/download/v0.4.1/WA2-AppleTV.ipa): Apple TV, Build 8, unsigned; updates paused.
- [WA2-macOS.dmg](https://github.com/galhack/WhiteAlbum2_iOS/releases/download/v0.4.1/WA2-macOS.dmg): Mac with Apple silicon, Build 8.
- [ffmpeg-7.1.5.tar.xz](https://github.com/galhack/WhiteAlbum2_iOS/releases/latest/download/ffmpeg-7.1.5.tar.xz): the corresponding FFmpeg source, provided as the LGPL requires. You do not need it to play.
- [SHA256SUMS.txt](https://github.com/galhack/WhiteAlbum2_iOS/releases/latest/download/SHA256SUMS.txt): checksums.

The packages contain no game content. You can also [build them from source](#building-from-source).

## 3. Install

**iPhone / iPad (Sideloadly)**

1. Connect and unlock the device, then tap *Trust This Computer*.
2. Drag `WA2-iPhone-iPad.ipa` into Sideloadly, pick the device, enter your Apple ID and click **Start**.
3. On the device, trust the developer certificate in **Settings → General → VPN & Device Management**.
4. On iOS 16 and later, turn on **Developer Mode** in **Settings → Privacy & Security** and restart when prompted.

Free accounts can have 3 sideloaded apps per device, and signatures last 7 days. Enable *Automatic refresh* in Sideloadly to re-sign automatically. Always update with the **same Apple ID**; otherwise the update is installed as a separate app and your data and saves are not visible to it.

**Vision Pro and Apple TV (Xcode)**

Pair the device in Xcode → Devices and Simulators. Then build with your own team and install with `xcrun devicectl device install app`. The exact commands are in the Chinese README and in [APPLE_TV.md](APPLE_TV.md). Vision Pro runs the same app as iPhone/iPad (*Designed for iPad*).

**Mac**

1. Open `WA2-macOS.dmg` and drag **WA2** to Applications.
2. The app is not notarized. If macOS refuses to open it, click **Open Anyway** in **System Settings → Privacy & Security**, or run `xattr -dr com.apple.quarantine /Applications/WA2.app`.
3. Nothing needs copying on a Mac. Click **Choose folder** and select the PC game folder directly: either the main game folder that contains `IC/`, or the Special Contents folder.

## 4. Import the game data

- **iPhone / iPad**: in Finder, select the device → **Files** tab → **wa2**, and drag in the whole `Wa2Res` and `Wa2SC` folders. On Windows, use File Sharing in the Apple Devices app or iTunes. You can also copy the folders on the device through the Files app (**On My iPhone/iPad → wa2**).
- **Vision Pro / Apple TV**: use `xcrun devicectl device copy to` with `--domain-type appDataContainer`. On Vision Pro the destination is `Documents`; on Apple TV it is `Library/Caches`. Never pass `--remove-existing-content`, which can wipe the app container, saves included.

tvOS may purge cached game data when storage runs low. If that happens, import the data again. Saves and progress are backed up separately and restore automatically.

Launch the app, tap **Main** or **Special Contents**, choose a language and press **Play**.

Before playing, turn off **Show corner menu / 显示右上角菜单** to hide the **≡** button. It is enabled by default; pressing Play saves your choice, and you can enable it again on the next launch. The Apple port adds this button and its pause panel. The original Save, Load, Options, Backlog, Auto, Skip and Hide controls remain available when it is hidden. Returning to the title uses the pause menu; enable the corner button on the next launch if needed.

## Controls

| Action | Touch | Siri Remote | Keyboard / mouse |
| --- | --- | --- | --- |
| Advance text | Tap | Click (Select) | Click / Enter |
| Menu | **≡** button, top right | Menu / Back | Esc, or click ≡ |
| Skip | Menu → Skip, or long-press (enable it in Options) | Menu → Skip | Hold Ctrl |
| Auto | Menu → Auto | Play/Pause | Quick bar |
| Backlog | Swipe down on the game canvas, including outside the dialogue box, or use the original Backlog button / Menu → Backlog; drag to scroll | Menu → Backlog, then up/down | Mouse wheel |
| Close a menu | Tap outside it | Menu | Esc |

On Vision Pro, look at a control and pinch. On Apple TV, pressing Menu on the launcher or the title screen returns to the Home screen. Small buttons get enlarged touch areas that never overlap a neighbouring button's own area.

A touch tap on the game canvas advances text on release; swiping down opens history without advancing. Full-page digital novels support the same swipe. Choices, movies and other menus keep their own input handling, and the original shortcut buttons remain available.

## Languages and subtitles

The Special Contents catalog displays full novel titles in the selected
language. In the previous numbered order, these are *His God, Her Savior*;
*Until the Snow Melts and Falls Again*; *After the Festival ~ Setsuna's Thirty
Minutes ~*; *Twinkle Snow ~ Reverie ~*; *The Idol Who Forgot How to Sing*;
*An Unreachable Love, Reached*; *Princess Setsuna's Ordeal and Her Minister's
Schemes*; *Koharu's Fair Weather After the Typhoon*; and *It's No Longer the
Season of White Album*. English menu labels identify the stories; an English
script translation must still be supplied separately.

- **Japanese**: needs `script.pak` and `fnt.pak` from the original game.
- **Simplified Chinese**: needs CK-GAL's `ck-gal.pak` and `fon.pak`. Special Contents uses `script_cn.pak` and `fon.pak` instead.
- **English and Traditional Chinese**: need a user-supplied translation; see [APPLE_PORT.md](APPLE_PORT.md).

**v0.4.1 bundles Simplified Chinese subtitles on every Apple client.** Select Simplified Chinese to enable them; no extra files or Windows DLL are needed.

The data comes from [nxs7/WA2-CHS-Subtitle-Patch](https://github.com/nxs7/WA2-CHS-Subtitle-Patch), with its MIT notice and original translation credits retained. See [SUBTITLE_NOTICE.md](SUBTITLE_NOTICE.md).

- Main game: the upstream patch's 6 sound-effect and 40 voice entries.
- Special Contents: choose **Radio dramas** in the catalog, then select a drama/chapter. The upstream data supplies dialogue for 23 chapters and 5 song entries. Dialogue and lyrics can display simultaneously. Pause, restart, next chapter and automatic playback within one drama are supported. Audio still comes from your game resources.
- The upstream patch does not subtitle the *2016 New Year's Eve* Talk Comment chapter. That audio remains playable without invented captions.
- Japanese, English and Traditional Chinese modes do not display Simplified Chinese subtitles.
- Optional overrides go at `<data folder>/subtitles/zh-Hans.yaml`. The main game accepts upstream `soundEffect/voice` and legacy `soundSubtitle/voiceSubtitle`; Special Contents accepts `voiceDrama/song`. The two editions never share their subtitle timelines.

## Saves

Saves stay on each device and are kept separately for each edition and language. Chapters unlock as in the original: IC first, then CC, then Coda. Deleting the app deletes its data and saves, so back up `wa2/saves` first.

PC scenario saves use a different binary layout. See [PC save conversion](PC_SAVE_IMPORT.md) for the offline converter and its supported layout. Mac main-game Simplified Chinese saves are in `~/Library/Application Support/Godot/app_userdata/wa2/saves/zh-Hans/`.

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

To produce every release package at once (iOS, tvOS, Mac, notices, FFmpeg source and checksums), run `GODOT=… TVOS_ENGINE=… FFMPEG_SOURCE=… tools/build_release.sh`. The output goes to `build/release/`.

For the FFmpeg plugin and templates, see [APPLE_PORT.md](APPLE_PORT.md) and [addons/wmv_video/BUILDING.md](addons/wmv_video/BUILDING.md). For Apple TV, see [APPLE_TV.md](APPLE_TV.md).

## FFmpeg and the LGPL

The apps use FFmpeg 7.1.5 under the GNU LGPL 2.1 or later. Only LGPL components are enabled: there is no `--enable-gpl`, `--enable-nonfree` or `--enable-version3`, and the build includes only the ASF demuxer and the WMV, VC-1 and WMA decoders.

- **Mac**: FFmpeg is a set of replaceable dynamic libraries in `WA2.app/Contents/Frameworks`.
- **iPhone, iPad, Vision Pro and Apple TV**: FFmpeg is linked statically. To meet the LGPL for these builds:
  - the complete application source and build scripts are published here;
  - every release attaches the corresponding FFmpeg source;
  - each app shows the full LGPL text under **Licenses** on the launcher.

To relink the iOS app with a modified FFmpeg, run these steps and then rebuild as described above:

```sh
python3 tools/build_ios_ffmpeg.py /path/to/your/ffmpeg --jobs 8
python3 addons/wmv_video/tools/build.py --platform ios --target both --jobs 8
```

You may modify the app for your own use and reverse engineer it to debug such modifications.

## Credits and licensing

The engine reimplementation is by [dorakyuraduang/wa2-godot](https://github.com/dorakyuraduang/wa2-godot) and its contributors. The project uses Godot (MIT), FFmpeg (LGPL 2.1+), godot-cpp and YamlDotNet; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Licensing is per file:

- **Apache-2.0**: the upstream porting code, plus the code, tools and documentation added by the Apple port.
- **MIT**: `addons/wmv_video` and two files derived from Godot and godot-cpp.
- **LGPL-2.1-or-later**: FFmpeg.

[LICENSE](LICENSE) gives the full scope.

*WHITE ALBUM 2* and all of its content are © Leaf / AQUAPLUS. This project is not affiliated with or endorsed by the rights holders and distributes no original game resources. Bundled third-party subtitles retain their separate notices.
