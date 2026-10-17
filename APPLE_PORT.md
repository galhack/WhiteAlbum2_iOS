# Apple port development notes

This is an in-progress port. The local macOS artifact is `build/WA2.app` (Apple Silicon). An unsigned iPhone/iPad device IPA is available at `build/WA2-iPhone-iPad.ipa` for AltServer or Sideloadly re-signing. Physical iPad installation, representative dialogue/audio and cross-process saves pass. The iPad-compatible app is installed through Xcode on physical Vision Pro; the user confirms gaze/pinch chapter selection after the pointer fix. Complete routes, physical iPhone and background/resume checks remain outstanding.

## Local progress

Cloud UI, background synchronization and the native document-folder bridge have been removed at the user's request (2026-10-16). Progress is stored only in the application's local save directory. Updating the app with the same bundle identifier preserves that directory; uninstalling removes it. Existing local saves remain compatible. Previously exported cloud history is left untouched, and earlier iCloud setup instructions no longer apply.

Version 0.3.0/build 4 adds main-game progression and directional remote navigation. IC is always available; CC requires persistent flag 0; Coda requires flags 0 and 1. Both Japanese and CK-GAL archives set flag 0 at the end of 1013 and flag 1 at the end of 2413. Opening-video flags 202/210/220 do not unlock chapters. Ten policy fixtures and the actual title-menu integration cover fresh, IC-complete, Coda-eligible and inconsistent states. Original flags are restored after integration tests. The debug `--start-script` option intentionally bypasses normal title eligibility for development.

The Apple TV target uses a separate pinned development engine with tvOS .NET support. Its local storage and build instructions are in [APPLE_TV.md](APPLE_TV.md). Other targets retain Godot 4.5.1. Current regression checks include 87 C# fixture cases, 11 Python tests, rendered remote menu/choice integration and the actual Chinese Special Contents save/load round trip. These checks do not establish complete-route correctness or physical Siri Remote behavior.

The launcher reads original game files from an external folder. Select your main game folder (referred to below as `/path/to/wa2`). Include the main archives and `IC/`. Japanese uses `script.pak`, `fnt.pak` and CP932. Simplified Chinese uses `ck-gal.pak`, `fon.pak` and the CK-GAL decoder. Main-game saves are under Godot application data at `saves/<language>/`; Special Contents uses `saves/special/<language>/`. On iOS this application data root is Documents. Game archives are not modified.

Launch a chapter directly for development:

```sh
build/WA2.app/Contents/MacOS/wa2 -- --resources /path/to/wa2 --language ja --play --start-script 1001
```

Chapter starts: IC `1001`, CC `2001`, Coda `3001`. Normal launching opens the folder/language chooser and original title flow. English and traditional Chinese require actual user-supplied translations and `languages/en.cfg` or `languages/zh-Hant.cfg` inside the data folder:

```ini
[language]
script_archive="translation.pak"
font_archive="translated-fonts.pak"
text_encoding="shift_jis"
```

Use `text_encoding="ck-gal"` only for patches with that encoding. Translation fonts must match `assets/font.map`; alternate atlas layouts are not supported yet. No English or traditional Chinese translation is supplied.

## Reproduce the macOS build

Use Godot 4.5.1 .NET, .NET SDK 9, Python 3, SCons and Apple command-line tools. Follow `addons/wmv_video/BUILDING.md` for FFmpeg 7.1.5 and the native plugin. This Apple build uses godot-cpp revision `e83fd0904c13356ed1d4c3d09f8bb9132bdc6b77` from branch 4.5.

```sh
dotnet build wa2.csproj
dotnet run --project tests/LaunchOptions/LaunchOptions.csproj
python3 -m unittest discover -s tests -p 'test_*.py'
mkdir -p build
python3 tools/inventory.py /path/to/wa2 /path/to/wa2SC > build/resource-inventory.json
python3 tools/prepare_macos_template.py /path/to/official/4.5.1.stable.mono/macos.zip
godot --headless --path . --export-debug macOS build/WA2.app
codesign --verify --deep --strict build/WA2.app
```

The official template has universal executables; the preparation script thins them to ARM64 to match .NET export. System `codesign` signs the application and nested libraries with an ad-hoc identity for local testing. Distribution on other machines still needs validation.

The rendered smoke test requires a window:

```sh
godot --path . --fixed-fps 60 --script res://tests/smoke.gd -- --resources /path/to/wa2 --language zh-Hans --play --start-script 1001
```

It skips active opening videos, waits for dialogue and saves `build/smoke.png`. Review the screenshot and log. Test video separately without skipping.

## Evidence and remaining work (2026-10-13)

- Apple M1 Max, Godot 4.5.1 .NET, .NET SDK 9.0.318: build passes with zero compiler warnings/errors; 54 C# fixture checks (also executed as a native AOT binary) and 3 archive-index tests pass.
- Actual window: simplified Chinese IC and Japanese CC render readable dialogue, backgrounds and bitmap fonts. Native FFmpeg decodes original ASF/WMV/WMA files. Japanese must not show the bundled Chinese subtitle overlay; that language guard has been added.
- Exported ARM64 app launches independently of the editor. Godot built-in signing produced invalid framework sealing; system signing verifies successfully.
- Resource inventory finds 187 scripts in the main archive and 228 in Special Contents, including 41 additions. Local metadata-only report: `build/resource-inventory.json`.
- Coda renders actual Chinese dialogue. IC and Coda pass a save/load round trip that checks script, position, text and script-local state, then removes its reserved diagnostic save.
- Xcode 27.0 first-launch setup is complete. The ARM64 iOS device build succeeds. The IPA validates both device families, Documents sharing, device Mach-O target and ZIP integrity.
- NativeAOT now uses statically compiled VM values and explicit YAML parsing. All three C# AOT targets compile. Upstream GodotSharp trimming/AOT warnings remain; device runtime validation is required.
- Official Godot 4.5.1 Mono iOS templates label their simulator library universal, but `lipo -archs` reports only x86_64. Current Xcode simulators require ARM64. An ARM64 simulator engine was built from official source and added to the debug template with `tools/prepare_ios_template.py`.
- Special Contents 6001/6101/4000/7300 reach dialogue and pass save/load round trips on macOS. iPad simulator renders IC Chinese and Special Contents Chinese and plays original WMV video.
- iPhone simulator renders the Chinese extra episode with readable text. The same ARM64 simulator application installs as an iPad-compatible flat window on visionOS 27 and renders Japanese Setsuna mini-after dialogue. Physical iPhone/Vision Pro installation, touch, suspend/resume and all-story completion remain unverified. Physical iPad results are recorded below.
- Vision Pro needs a verified runtime and installation path. Template availability alone does not establish C# export support or AltServer compatibility.

## Reproduce the iPhone/iPad package

Build minimal LGPL FFmpeg and the native playback XCFrameworks, then export the project:

```sh
python3 tools/build_ios_ffmpeg.py --help
python3 addons/wmv_video/tools/build.py --platform ios --target both --scons /path/to/scons --jobs 8
godot --headless --path . --export-debug iOS build/ios/WA2.ipa
python3 tools/prepare_ios_project.py build/ios
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer xcodebuild -project build/ios/WA2.xcodeproj -scheme WA2 -configuration Debug -sdk iphoneos -destination 'generic/platform=iOS' -derivedDataPath build/ios-derived CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO DEVELOPMENT_TEAM= build
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer python3 tools/package_ipa.py build/ios-derived/Build/Products/Debug-iphoneos/WA2.app build/WA2-iPhone-iPad.ipa
```

The export preset's `LOCAL00000` team is a project-generation placeholder. This build disables signing; AltServer must sign it with the user's own account. No account credentials or provisioning profiles are included. The app contains no original game archives. Import the game data into Documents/Wa2Res through Finder file sharing after installation. The required directory layout matches the macOS data folder.

## Special Contents data

Select the independent Special Contents folder (`/path/to/wa2SC`). Its Chinese profile uses script_cn.pak and fon.pak; Japanese uses script.pak and fnt.pak. Optional patch.pak and Chinese gcn.pak override images. The launcher detects this edition by the absence of IC/ and validates mini-after and extra-episode scripts before scene creation. Its catalog provides both mini-afters, the extra episode and nine digital novels. These are narrative entry points; full completion and system-voice extras are not yet verified.

Optional Chinese subtitle YAML goes in subtitles/zh-Hans.yaml in the selected resource folder. Special Contents never falls back to the main game's subtitle timings. The transfer folder prepared locally includes the user's local Special Contents subtitle patch YAML.

```sh
python3 tools/prepare_game_data.py /path/to/wa2 build/resource-transfer/Wa2Res
python3 tools/prepare_game_data.py /path/to/wa2SC build/resource-transfer/Wa2SC --chinese-subtitles /path/to/SpecialContents/script/sub.yaml
```

The helper refuses existing output, copies resource archives, IC data and loose image/profile/subtitle files, and omits Windows executables. On APFS it uses copy-on-write clones when available. These folders contain original user-owned game data and are local-only artifacts, excluded from Git. Drag the two whole folders into the app's Finder Files pane. The mobile launcher has Main and Special Contents shortcuts.

## ARM64 simulator template and UIKit lifecycle

Build the missing simulator engine from the official Godot 4.5.1-stable source:

```sh
scons platform=ios target=template_debug arch=arm64 simulator=yes module_mono_enabled=yes vulkan=no use_volk=no debug_symbols=no -j8
python3 tools/prepare_ios_template.py /path/to/official/ios.zip /path/to/godot/bin/libgodot.ios.template_debug.arm64.simulator.a
```

The preparation script combines this ARM64 library with the official x86_64 simulator library. It leaves the official device and release libraries intact. The iOS debug export preset points at this local ZIP. Release simulator export is not repaired by this debug-only template.

iOS 27 requires a scene-based lifecycle for apps linked against its SDK. `native/ios/WA2SceneDelegate.mm` attaches Godot's single window to its UIWindowScene and forwards scene focus/background events to Godot. Run `tools/prepare_ios_project.py` **after every export**, before Xcode compilation. It adds the adapter to the plugin translation unit, makes it Objective-C++, and writes the scene manifest. The IPA packaging tool rejects applications without a scene configuration. UIKit callbacks still require suspend/resume verification on actual devices. Apple requirement: https://developer.apple.com/documentation/uikit/transitioning-to-the-uikit-scene-based-life-cycle

Vision Pro uses Apple's compatible-iPad-app path, not a native immersive visionOS target. Godot 4.5.1's C# exporter does not expose visionOS; having its native export template alone is insufficient. See https://developer.apple.com/documentation/visionos/making-your-app-compatible-with-visionos. Physical installation now passes using Xcode's Designed for iPad/iPhone destination and an explicitly authorized development team. AltServer's official direct-IPA instructions document iPhone/iPad; direct AltServer installation on Vision Pro is not established.

## Save persistence probes

Debug builds accept `--verify-save write` or `--verify-save read` together with `--play --start-script <id>`. The write phase waits for stable dialogue and saves to reserved slot 9000. It refuses any existing reserved save or expectation. The read phase runs in a new application process and checks script, position, a dialogue hash and a script-local sentinel, then removes its temporary save and expectation. Only test metadata is retained in `user://port-save-result.cfg`; no original text is logged. Normal launching does not run this probe.

```sh
# Run as two separate processes with the same data and language.
build/WA2.app/Contents/MacOS/wa2 -- --resources /path/to/wa2SC --language ja --play --start-script 6101 --verify-save write
build/WA2.app/Contents/MacOS/wa2 -- --resources /path/to/wa2SC --language ja --play --start-script 6101 --verify-save read
# Simulator driver locates the sandbox, verifies different process IDs and waits for results.
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer python3 tools/verify_simulator_persistence.py SIMULATOR_UUID --folder Wa2SC --language zh-Hans --script 4000
```

macOS Japanese 6101, iPad simulator NativeAOT Chinese 4000 and Japanese IC 1001 pass across distinct processes. Physical iPadOS 26.4.2 also passes Chinese Special Contents 4000 and Japanese IC 1001 across distinct application processes. This verifies save serialization and persistence, while user interaction and background restoration require their own tests.

## Physical iPad installation and validation (2026-10-13)

The unsigned IPA installs using Sideloadly 0.70.1 after freeing one device app slot with the user's explicit authorization. Before that, both AltStore and Sideloadly reached the device's three-app free-profile limit. Sideloadly signing succeeded with an account reporting ten available App IDs, but the device still rejected installation. App ID availability must not be confused with simultaneous installed-app limits. See https://sideloadly.io/faq.

After the user trusted the signing profile on the device, WA2 launches natively on a physical iPad Pro with iPadOS 26.4.2. Both resource folders were imported over USB. All 84 main-game files match the local relative paths and byte sizes; all thirteen root Special Contents archives also match their local sizes. The launcher renders Chinese/Japanese labels and landscape layout. Actual Chinese Special Contents 4000 and Japanese IC 1001 dialogue render. The user reports normal sound and picture.

The diagnostic write/read tests pass for both editions in separate processes. The read phase checks the edition/language profile, script, position, dialogue hash and local state, and removes its temporary save and expectation. Real touch, save/load buttons and background/resume are being checked separately. Physical iPhone and Vision Pro installation remain unverified. Screenshots and device/account metadata are local-only build artifacts and are excluded from Git.

## v0.4.1 release validation (2026-10-18)

Published [v0.4.1](https://github.com/galhack/WhiteAlbum2_iOS/releases/tag/v0.4.1)
as the latest stable release, with iOS/tvOS/macOS optimized packages, FFmpeg source,
third-party notices and checksums. All three applications report 0.4.1/build 6.
GitHub asset sizes and SHA256 digests match the six local release files. Both
READMEs link directly to the latest assets.

The upstream subtitle files are preserved byte-for-byte, including line endings.
Main data contains 6 SE/40 voice entries; Special Contents contains 23 drama
chapters/5 song entries. Separate bundled files and external overrides are
selected by edition; only Simplified Chinese mode enables these captions.
The old mixed-source assets/sub.yaml remains excluded. Provenance, upstream
revision, translation credits and the MIT notice are in SUBTITLE_NOTICE.md.

Validation passed: C# compilation with zero warnings/errors, all C# fixtures
(including 13 new schema/timeline checks and both complete datasets), 11 Python
tests, rendered subtitle start/end/seek/stop, external overrides, Japanese
isolation, actual SC drama audio with concurrent captions and lyrics, remote
chapter selection/Play-Pause/Back, existing remote menus/choices and an actual
dialogue save/load round trip. Both subtitle hashes and the MIT notice were
verified inside each exported resource pack. The packaged Mac app launches
and exits normally. Fresh physical subtitle interaction on every Apple
device is not claimed by this release.

An initial sandboxed verification failed to create Godot's user-data log
directory. A later macOS window-restoration prompt blocked the legacy save
smoke driver; dismissing that prompt allowed the unchanged test to run and
pass. No game save implementation was changed to work around the harness.

## v0.4.1 build 7 device follow-up (2026-10-18)

The user confirmed Simplified Chinese radio-drama captions and audio on physical
iPhone, iPad and Vision Pro. The user also confirmed the CC opening station
caption on iPad. Real scene 2001 playback on Mac verified both the station SE
caption and the subsequent interview voice caption at the top of the screen.

The settings return failure was reproduced with pointer input: the runtime
Special Contents catalog covered legacy overlay pages. Bringing each active
overlay to the front fixes pointer access while preserving stack navigation.
The regression exercises the catalog settings button, cancellation of the
default-settings confirmation, and the settings return button. It passes along
with the existing remote menus/choices regression and C# fixtures; compilation
reports zero warnings/errors. Fixture restore reported NU1900 while the NuGet
vulnerability endpoint was unavailable; all fixture execution passed.

Build 7 was development-signed and installed through Xcode on all three physical
devices. Their actual installed versions report 0.4.1/build 7. Each retained all
98 resource files with matching paths/sizes. The original manual saves were
compared byte-for-byte with fresh pre-update backups: 7 on iPhone, 3 on iPad and
2 on Vision Pro, with no mismatches. After installation, the user confirmed that
settings open and the on-screen return button works on all three devices,
including gaze-and-pinch on Vision Pro. This records the initial local build 7
verification, before the later Build 8 replacement release described below.

## v0.4.1 build 8 full novel titles (2026-10-18)

The shared Special Contents catalog now uses full Japanese, Chinese and English
titles for all nine digital novels. Story entry points and their order are
preserved. Title/entry mapping was cross-checked against the local script
openings and the [translation project's script index](https://www.baka-tsuki.org/project/index.php?title=White_Album_2).
Original Japanese titles also appear in the [AQUAPLUS special-content catalog](https://aquaplus.jp/wa2/special_early.html).
In particular, entry 5300 is After the Festival and entry 5200 is Twinkle Snow.
Both READMEs document the full titles.

C# compilation passed with zero warnings/errors, the existing content fixtures
and actual catalog click test passed, and all three languages fit the mobile
catalog width at 30 px. The rendered catalog was reviewed at both scroll ends.
The existing catalog smoke driver still reports its known ObjectDB cleanup
warning when quitting immediately after selection; the selection check passes.

Build 8 was signed, installed through Xcode and launched on physical iPhone,
iPad and Vision Pro. Actual installed versions report 0.4.1/build 8. All 98
resource files on each device retained matching paths/sizes, and fresh
pre-update manual saves matched byte-for-byte afterward: 8 on iPhone, 4 on iPad
and 3 on Vision Pro. These checks were performed on local test installations
before preparing the replacement release described below.

## Publishing identity and local-only files

Publish to `galhack/WhiteAlbum2_iOS` using the token in `GALHACK`. Bind that
variable explicitly to the publishing process; do not rely on the machine's
default GitHub login when several accounts are available. Check that the
authenticated login is `galhack` before publishing. Keep the token in the
process environment, never in a remote URL, repository file or printed log.

Git author identity and GitHub authentication are separate. For this repository,
use `galhack <galhack@protonmail.com>` for new author and committer metadata.
The earlier v0.4.1 Release and its attachments were uploaded by `galhack`, while
several source commits inherited the machine's `davidqw` author identity. New
publication uses the corrected repository-local Git identity and explicit
`GALHACK` authentication.

The tracked `.gitignore` explicitly excludes both `agents.md` and `AGENTS.md`,
`IMPLEMENTATION_PLAN.md`, local credential/environment files, signing material,
original game installations/archives/installers, extracted scripts/images and
user saves. Ignore rules do not remove files that are already tracked: inspect
the tracked publish tree and scan for credential values before pushing.

## v0.4.1 Build 8 replacement release (2026-10-18)

The existing [v0.4.1 Release](https://github.com/galhack/WhiteAlbum2_iOS/releases/tag/v0.4.1)
now serves Build 8 using the original asset filenames. All three optimized
application packages report 0.4.1/build 8. The tvOS staging tool reads the shared
Apple export metadata to keep its build number aligned. This release includes
the verified settings return fix and full Special Contents novel titles.

Publication explicitly used GALHACK; release/asset uploaders, source author and
committer, and the updated annotated tag's tagger are galhack. The v0.4.1 tag
points to release source commit 9bfd081ea4db6522d25df1c62fb1843d696182bb.
The original Release identity and latest/download links are retained. Four old
Build 6 backup attachments were removed after public verification and the
user's explicit deletion authorization. Six final attachments remain: the
three installers, third-party notices, FFmpeg source and SHA256SUMS.txt.

Validation passed: 11 Python tests; iOS/tvOS platform and archive checks;
version metadata and resource-pack checksums on all three platforms; exact
bundled subtitle/notice bytes and the upstream MIT text; exclusion of original
game archives and local instruction/credential files; Mac application signature
and normal launch/exit; DMG integrity and a mounted byte-for-byte comparison of
its application with the validated build. All six public assets were downloaded
again and matched local SHA256 values, and all six latest/download links returned
HTTP 200. The public source ZIP matches Build 8 code, metadata and original YAML
bytes while excluding local instructions, plans, credentials and game archives.
