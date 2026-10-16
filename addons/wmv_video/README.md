# WMV Video GDExtension 0.6.0

FFmpeg-based ASF/WMV playback for Godot 4.5. The native `WMVPlayer` class accepts `.wmv` files and ASF/WMV content stored with a `.pak` extension. WA2 uses `script/VideoPlayback.cs` as a compatibility facade for the previous player API.

The complete plugin source and build tooling live in this directory. The build matrix covers Windows x86_64, Android ARM64/x86_64, Linux x86_64/ARM64, macOS universal, and iOS device/simulator XCFrameworks.

Checked-in Windows x86_64 and Android ARM64 binaries are the only builds currently verified. Linux, macOS, iOS, and Android x86_64 entries are build configurations, not tested release claims.

Runtime files in `bin` are distributable plugin artifacts and may be committed together with the license notices. Import libraries, object files, SDKs, build directories, original game media, signing keys, and local tool state must not be committed.

See `BUILDING.md` or `BUILDING.zh-CN.md` for commands, SDK layout, platform limitations, and LGPL obligations.
