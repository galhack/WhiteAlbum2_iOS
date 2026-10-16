#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Build assets/legal/THIRD_PARTY_LICENSES.txt, shown in the app and shipped in every package."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = "https://github.com/galhack/WhiteAlbum2_iOS"

HEADER = f"""WHITE ALBUM 2 Apple port - open-source notices
===============================================

Source code of this application: {REPOSITORY}
No WHITE ALBUM 2 game data is included; players supply their own purchased copy.

FFmpeg (LGPL-2.1-or-later)
--------------------------
This application uses FFmpeg 7.1.5 (https://ffmpeg.org/) to decode the
original WMV/WMA movies. FFmpeg is licensed under the GNU Lesser General
Public License version 2.1 or later. Only LGPL components are enabled
(no --enable-gpl, --enable-nonfree or --enable-version3); the build enables
the ASF demuxer, VC-1 parser, WMV1/2/3, VC-1 and WMA decoders, and the
avcodec, avformat, avutil, swresample and swscale libraries.

The iPhone/iPad/Vision Pro and Apple TV apps link FFmpeg statically; the Mac
app ships it as replaceable dynamic libraries in Contents/Frameworks.

Corresponding source: the unmodified FFmpeg 7.1.5 source archive is attached
to every release at {REPOSITORY}/releases and is available from
https://ffmpeg.org/releases/ffmpeg-7.1.5.tar.xz . The exact configure options
are in tools/build_ios_ffmpeg.py and addons/wmv_video/BUILDING.md.

Relinking: the complete source of this application and its build scripts are
published at the address above, so you can rebuild it with a modified FFmpeg
(see "FFmpeg and the LGPL" in README.md) and install the result on your own
device. You may modify this application for your own use and reverse
engineer it to debug such modifications.

"""

PARTS = [
    ("FFmpeg - GNU Lesser General Public License 2.1", "LICENSES/LGPL-2.1-or-later.txt"),
    ("Godot Engine - MIT License", "LICENSES/Godot-MIT.txt"),
    ("Godot Engine - third-party components", "LICENSES/Godot-COPYRIGHT.txt"),
    ("godot-cpp - MIT License", "addons/wmv_video/licenses/godot-cpp-MIT.txt"),
    (".NET runtime (NativeAOT) - MIT License", "LICENSES/dotnet-MIT.txt"),
    (".NET runtime - third-party notices", "LICENSES/dotnet-ThirdPartyNotices.txt"),
    ("YamlDotNet - MIT License", "LICENSES/YamlDotNet-MIT.txt"),
    ("Noto Sans CJK SC - SIL Open Font License 1.1", "assets/ui_fonts/OFL.txt"),
    ("WMV video plugin - MIT License", "addons/wmv_video/LICENSE"),
    ("WA2 Godot porting code and Apple port - Apache License 2.0", "LICENSES/Apache-2.0.txt"),
]


def main():
    text = HEADER
    for title, path in PARTS:
        text += "\n" + "=" * 78 + "\n" + title + "\n" + "=" * 78 + "\n\n"
        text += (ROOT / path).read_text(encoding="utf-8", errors="replace").strip() + "\n"
    output = ROOT / "assets/legal/THIRD_PARTY_LICENSES.txt"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(text, encoding="utf-8")
    print(f"Wrote {output.relative_to(ROOT)} ({len(text.encode())} bytes)")


if __name__ == "__main__":
    main()
