# Apple port implementation plan

Scope: macOS, AltServer-installable iPhone/iPad IPA, and a verified Vision Pro path. Languages must select actual user-provided script/translation data. Cover IC, CC, Coda and Extended Edition content without claiming untested compatibility.

## Stage 1: Establish reproducible baseline and resource inventory
**Goal**: Build the existing engine and identify game editions, scripts, fonts and UI dependencies.
**Success Criteria**: Local .NET/Godot toolchain builds; machine-readable resource report; missing Extended Edition data identified explicitly.
**Tests**: Synthetic archive validation plus read-only inventory of local game data.
**Status**: Complete

## Stage 2: External resources and language profiles
**Goal**: Load local game data on macOS and from Documents/Wa2Res on iOS, with separate writable saves and explicit language profiles.
**Success Criteria**: Japanese and CK-GAL Chinese resolve distinct scripts/decoders/fonts; unsupported translations fail clearly; no original game archives are committed.
**Tests**: Profile selection, invalid/missing archives, case-sensitive paths, separate saves, script coverage.
**Status**: Complete

## Stage 3: macOS playable build
**Goal**: Produce and launch a native macOS application with local resource preparation and working media.
**Success Criteria**: Title, representative dialogue in IC/CC/Coda, audio/video, save/load and language switching verified.
**Tests**: Native compile and interactive smoke tests using local data, recording limitations.
**Status**: In Progress

## Stage 4: iPhone/iPad packaging and touch support
**Goal**: Export Xcode project and unsigned device IPA for AltServer signing, with sandbox resource import and touch input.
**Success Criteria**: ARM64 device build and validated Payload structure; installation and gameplay verified on available devices.
**Tests**: Packaging validation, resource transfer, orientation, touch, suspend/resume, save persistence.
**Status**: In Progress

## Stage 5: Extended Edition, Vision Pro and release validation
**Goal**: Verify added edition content, a viable Vision Pro installation/runtime path, and user-requested iCloud save sharing.
**Success Criteria**: Actual Extended Edition resources exercised; Vision Pro runtime and installation documented from evidence; iPhone/iPad/Vision Pro share saves through an explicitly selected iCloud Drive folder without dropping offline/conflicting saves; final artifacts and validation report.
**Tests**: Edition-specific script execution, multilingual rendering, Vision Pro interaction and media, deterministic offline/conflict/corruption save-sync cases, actual iCloud transfer between devices, cross-platform regression.
**Status**: In Progress

## Toolchain investigation

Initial build attempts: (1) .NET first-run wrote outside allowed workspace; moved DOTNET_CLI_HOME and NUGET_PACKAGES into workspace. (2) SDK resolver could not access NuGet service index. (3) passing --configfile to dotnet build did not affect early SDK resolution. Stopped retrying that route. Alternative: restore the SDK packages from official Godot-bundled local nupkgs through a plain Microsoft.NET.Sdk bootstrap project, then build from the populated package cache. curl reaches NuGet; .NET service-index access differs in this environment.

## NativeAOT and simulator investigation

VM dynamic operators required runtime code generation; replaced them with typed, statically compiled operations. Reflection-based YAML deserialization was replaced by explicit parsing. 46 synthetic behavior checks pass in both .NET and a native AOT executable. All three iOS AOT library targets publish; the full ARM64 device application builds and the unsigned IPA passes structural validation.

The first Xcode device build failed because every FFmpeg XCFramework carried duplicate copies of the complete SDK headers. Headers are only needed to compile the plugin, so final runtime XCFramework packaging omits them. The next device build succeeded.

Simulator attempt 1 specified both a destination and architecture; Xcode rejects this combination. Attempt 2 used the destination's ARM64 architecture and exposed an upstream template mismatch: the nominal universal Godot simulator archive contains only x86_64 objects. Stopped that build route and started an ARM64 template build from official Godot 4.5.1 source.

Coda smoke capture initially timed out even after reaching dialogue. Process sampling showed the engine was still running. Occluded windows do not necessarily emit frame_post_draw; an explicit RenderingServer draw fixes the diagnostic capture. Actual Coda dialogue renders, and IC/Coda save round trips pass. Smoke tests skip opening videos, so they do not establish uninterrupted playback or audio quality.

## Special Contents and Apple runtime evidence

Edition detection uses the presence of IC/. The separate Special Contents data selects script_cn.pak for Chinese, mounts optional patch/gcn image archives, has twelve narrative entry points and separate special/<language> saves. Actual macOS runs reach dialogue and pass save/load round trips for Chinese 6001, Japanese 6101, Chinese 4000 and Chinese 7300. The catalog's first button launches 6001 in an integration test. Main-game subtitles must not be reused in Special Contents; edition-specific optional YAML lives under subtitles/<language>.yaml.

The ARM64 simulator engine builds from official 4.5.1 source. Initial execution then failed in UIKit's no-scene-lifecycle assertion: iOS 27 requires UIScene. Added a single-window scene delegate forwarding focus/background events to Godot and an idempotent exported-project preparation script. iPad simulator subsequently renders Chinese IC and decodes the original WMV. Its Documents resources and saves survive application update and a changed sandbox path; the launcher rebases its remembered resource folder. Noto Sans CJK SC fixes missing CJK launcher glyphs.

Vision Pro simulator accepts and launches the same iOS simulator app as a compatible iPad application in a flat window. It also renders actual Japanese Setsuna mini-after dialogue; iPhone simulator renders Chinese extra-episode dialogue. This does not prove physical installation or AltServer support on Vision Pro. The first Vision Pro creation used the legacy non-4K device type, rejected by visionOS 27; the supported 4K device type works.

Desktop interaction tests are temporarily limited: the Device Hub window disappeared; coordinate input reported noWindowsAvailable, rebinding reported cgWindowNotFound, and the alternate launch helper is not exposed on macOS. Stopped that UI route after three attempts. Continue simulator CLI and rendered checks; real touch/save UI verification remains outstanding.

Current verification: 54 C# behavior checks pass in both .NET and a native AOT executable; 5 Python fixture tests pass. The latest macOS export passes strict/deep signature verification and launches standalone Special Contents. The latest unsigned device IPA is 54,185,867 bytes and passes ARM64/iOS device, iPhone+iPad family, Documents sharing, UIKit scene manifest and ZIP integrity validation. SHA-256: 5920912e1999f311bca8540974a40d4bc657832dc4019783c02ba9371c1cccd0. Simulator checks preceded the physical iPad connection described below.

## Cross-process saves and first physical iPad connection

Added an opt-in debug save probe (`--verify-save write|read`) with a reserved slot and an expectation containing only state metadata and a dialogue hash. It refuses to overwrite existing probe files. A separate fresh process verifies script, position, dialogue hash and a local variable, then removes the temporary save. macOS Japanese Special Contents 6101 passes both phases. iPad simulator NativeAOT Chinese Special Contents 4000 passes write/read with distinct process IDs. iPad simulator NativeAOT Japanese IC 1001 also passes write/read in different processes. At this stage, physical-device checks awaited signing and installation. Five option-validation tests were added before implementation.

The user connected a physical iPad Pro on iPadOS 26.4.2. USB pairing and Developer Mode are enabled. No local signing identity is available, but AltStore 2.3 is installed and AltServer 1.8.1 is running. The updated unsigned IPA was copied directly over USB to AltStore Documents and passed to AltStore's supported file-URL import handler. AltStore subsequently hit the device's three-free-profile-app limit.

At the user's request, Sideloadly 0.70.1 was used with the same unsigned IPA. Signing and USB upload succeeded, but ApplicationVerificationFailed explicitly listed three existing free-profile apps. The selected account still had ten App IDs available: changing a free account did not solve this device installation limit. After the user explicitly authorized deleting DevilutionX, its uninstall succeeded and Sideloadly completed WA2 installation at 100%. CoreDevice now lists WA2 as an installed, container-accessible application. Special Contents USB resource transfer succeeded; all thirteen root archive file sizes match the local source. After the user explicitly trusted the signing profile, the physical iPad renders the launcher and actual Chinese extra-episode dialogue (4000). Main-game transfer and save checks followed, as recorded below. No account credentials, provisioning data or device identifiers are committed.

Both USB transfers are now complete. All 84 main-game files match source paths and byte sizes. Physical Chinese Special Contents 4000 and Japanese IC 1001 pass diagnostic save write/read across separate application processes; the user confirms normal sound and picture. Real touch, UI save/load and background/resume feedback is pending. A fresh macOS CUA binding discovers the game window, but screenshot capture still returns cgWindowNotFound; no mouse UI result is claimed. Physical iPhone and Vision Pro remain outstanding, so the overall goal and stages are not complete.

A proposed unauthenticated LAN HTTP transfer was rejected by automatic approval review because it would expose the private IPA to other LAN clients. No server was started. The direct USB copy is the safer alternative and succeeded. No code or game data was published.

The official AltServer develop source at revision 74dc2d4c71e8a1d1abf91dcfc0cf3334f777f972 explicitly accepts ProductType prefixes iPhone/iPad/AppleTV and skips other types. RealityDevice (Vision Pro) is not in that whitelist. This is evidence against assuming official direct AltServer support for Vision Pro; actual compatible-app runtime remains established only in the simulator.

## macOS input regression and physical Vision Pro

User confirms Mac catalog buttons cannot be clicked. Earlier catalog testing emitted a signal directly and did not exercise GUI hit testing. The full-screen VideoPlayback wrapper sits after the UI manager with MouseFilter=Pass; Pass bubbles to parents rather than controls behind it. Its native video child already ignores mouse input, but the wrapper still intercepts the catalog. Replace the signal-only integration step with a synthetic viewport pointer click, establish failure, then make the paint-only wrapper ignore input. Actual OS mouse interaction will be verified separately.

Three macOS CUA coordinate attempts failed with noWindowsAvailable; fresh binding and raising the window did not restore pointer routing. Keyboard attempts and screenshots alone did not establish button activation. Stop that automation route and use the user-confirmed failure plus a GUI dispatch regression test.

Vision Pro is now paired and Developer Mode enabled on visionOS 27.2. Xcode lists it as a Designed for iPad/iPhone destination, and the compatible target builds unsigned. Automatic review initially rejected device registration/provisioning changes; after explicit user authorization, Xcode signed the app successfully. Strict/deep signature validation passes and the profile contains the target device with iOS/xrOS/visionOS platforms. First wireless install lost connection because the user turned the headset off; user reactivated it and installation is being retried. No existing certificates were explicitly revoked.

Sideloadly AutoRefresh is enabled for WA2 and the user's separate ONScripter-RU application. Both registered tasks have no errors; cached IPAs exist and the login LaunchAgent is running. WA2 overwrite preserves all 98 resource files and seven save files by path and size; only sys.sav modification time changes with game exit. ONScripter-RU restoration is tracked outside this repository.

The pointer-dispatch catalog test fails before the video wrapper fix and passes afterward. Headless runs omit only the optional diagnostic image because the dummy renderer has no texture. The synthetic test is not claimed as real OS interaction. The rebuilt Mac application then passes actual CUA mouse input: choose Chinese Kazusa mini-after, advance dialogue, open Save, create an empty slot, advance again, open Load, confirm and restore the earlier dialogue. Slot 01 is left available for the user. C# compilation reports zero warnings/errors and all 59 behavior checks pass. Godot's integration run reports its existing ObjectDB shutdown warning.

Physical Vision Pro installation succeeds after the user reactivates the headset. Special Contents resource transfer completes over the paired connection. The input fix is rebuilt for iPhone/iPad and signed for Vision Pro; updated unsigned IPA is 54,186,198 bytes, SHA-256 cf8a6ca5906190a732d72402e1ba8400ef0ef3ea24646cb206fac173ae097f23. Runtime verification and main-game transfer on Vision Pro remain in progress.

After unlocking, the user still reported an unclickable Special Contents menu on Vision Pro and iPad. The iPad still had the pre-fix package. The Vision Pro debug connection had been invalidated during headset sleep; inspection confirms the rebuilt package contains MouseFilter=Ignore on the video wrapper. Terminating the existing Vision Pro process and launching the installed fixed app restores chapter selection: the user confirms gaze/pinch enters actual dialogue. No new platform-specific input workaround is justified by this result. The iPad is reconnected by USB and the same fixed IPA is being installed through Sideloadly with AutoRefresh selected; the pre-update resource/save metadata has been captured for comparison. Vision Pro main-game transfer is running over the paired connection.

Vision Pro user confirms normal sound and manual save/load. The iPad fixed-package overwrite completes at 100%; all 106 current resource/save paths and sizes survive, and the AutoRefresh cache SHA-256 matches the fixed IPA. Both registered AutoRefresh tasks remain enabled and error-free. The user additionally requests shared game progress across iPhone/iPad/Vision Pro through iCloud. Use Apple's document-picker folder access, persisted security-scoped bookmarks and coordinated file operations to support the existing signing arrangement. Develop pure save merge behavior with deterministic fixtures before integrating the native picker and lifecycle hooks; retain independent conflicting snapshots and keep game archives local. Actual cloud propagation and permissions must be tested separately from local merge fixtures.

iPad user confirms actual chapter selection, sound and manual save/load after the fix. Vision Pro main-game transfer completes; all 84 file paths and sizes match the prepared source. Cloud-save tests fail first because the implementation is absent, then 20 deterministic cases pass: immutable per-slot version ancestry, offline branching, explicit conflict resolution with retained history, profile isolation, deferred active system files, restart deduplication, invalid paths/checksums/parents, empty-save refusal and bounded gzip compression. Native document-picker/bookmark/coordinated-file integration is compiling; actual iCloud propagation has not yet been established. The existing 59 C# checks also pass.

First physical cloud UI attempt has no button on either device: the launcher adds a sibling while the root is propagating readiness, and Godot rejects the immediate addition. Defer the root addition. Version 0.2.0/build 2 then creates the local cloud store on Vision Pro, and the user can open the interface, select the iCloud folder and exchange versions. Four immutable records appear in the Mac's iCloud Drive, establishing real upload/propagation. The user reports four conflicts and disables sync; metadata inspection finds four save categories with one head each, so there is no actual branch conflict. The history/conflict heading was ambiguous. Version 0.2.1/build 3 now displays separate history-version and conflicting-slot counts and friendly edition/language/slot labels. User-disabled sync is preserved until the user reconnects the folder.

The user explicitly allows clearing all current WA2 test progress. Save backups remain local under build/backups. A CoreDevice reset using `copy to --remove-existing-content true` unexpectedly clears the entire Documents scope rather than only the supplied subdirectory, deleting on-device resource copies as well. This was reported immediately; stop this reset route and never use that flag for subdirectory cleanup. Original game sources and prepared transfer folders are intact. Restore both resource folders without removal flags and verify every path/size. iPad recovery is complete: 84 main plus 14 Special Contents files match. Vision Pro recovery currently has 84 main files matched, with Special Contents transfer in progress. The four known cloud test records were removed individually through NSFileCoordinator after matching their bytes against local diagnostic copies; the parent iCloud folder is retained.

Sideloadly installation remained at 88% while the previous WA2 process was running under CoreDevice. Terminating that exact application process allows installation to complete; the device reports 0.2.0/build 2. Future updates terminate WA2 before install and wait for file transfers to finish. User connected iPhone 16 Pro Max on iOS 26.6.2 and enabled Developer Mode after the first DDI mount was explicitly refused for being disabled. Physical iPhone packaging/resource/cloud validation is now in progress.

Resource recovery now completes on both iPad and Vision Pro: each has all 98 expected resource paths and byte sizes, verified again after 0.2.1/build 3 overwrite. No original local game sources were modified. Both applications launch the updated cloud interface. Physical iPhone 0.2.1 installs through Sideloadly with AutoRefresh selected; the user explicitly trusts its developer profile. Special Contents transfer completes and main-game transfer is running. The user authorizes continuing cloud validation after temporarily disabling it; initial cross-device verification will use a fresh iPad save, with the other devices held at the launcher to avoid creating independent initial system-save branches. NativeAOT cloud fixtures also pass all 20 cases. Desktop C# compilation has zero warnings/errors; signed Mac and Vision artifacts pass strict/deep verification. Current unsigned IPA: 54,677,685 bytes, SHA-256 b66ebdfe4b2d7db10c970bde1a00dfe6decf8a8eb69121e2cc2788790b7003a6.
