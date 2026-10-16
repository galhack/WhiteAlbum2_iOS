#!/bin/bash
# SPDX-License-Identifier: Apache-2.0
# Build unsigned release packages for iPhone/iPad/Vision Pro, Apple TV and Mac.
#
# Required environment:
#   GODOT          Godot 4.5.1 .NET editor binary
#   TVOS_ENGINE    pinned tvOS Godot source tree with bin/ built (see APPLE_TV.md)
#   FFMPEG_SOURCE  ffmpeg-7.1.5.tar.xz, attached to the release for the LGPL
# Optional: SKIP_TVOS=1, SKIP_MACOS=1
set -euo pipefail
cd "$(dirname "$0")/.."
: "${GODOT:?set GODOT}" "${FFMPEG_SOURCE:?set FFMPEG_SOURCE}"
export DEVELOPER_DIR="${DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"
OUT=build/release
VERSION=$(sed -n 's/^application\/short_version="\(.*\)"/\1/p' export_presets.cfg | head -1)
rm -rf "$OUT" && mkdir -p "$OUT"

python3 tools/make_notices.py

echo "== iPhone / iPad / Vision Pro"
rm -rf build/ios build/ios-derived && mkdir -p build/ios
"$GODOT" --headless --path . --export-release iOS build/ios/WA2.ipa
python3 tools/prepare_ios_project.py build/ios
xcodebuild -project build/ios/WA2.xcodeproj -scheme WA2 -configuration Release -sdk iphoneos \
  -destination 'generic/platform=iOS' -derivedDataPath build/ios-derived \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO DEVELOPMENT_TEAM= build -quiet
python3 tools/package_ipa.py build/ios-derived/Build/Products/Release-iphoneos/WA2.app "$OUT/WA2-iPhone-iPad.ipa"

if [ "${SKIP_TVOS:-0}" != 1 ]; then
  echo "== Apple TV"
  : "${TVOS_ENGINE:?set TVOS_ENGINE}"
  TVOS_GODOT="$TVOS_ENGINE/bin/godot.macos.editor.arm64.mono"
  python3 tools/prepare_tvos.py plugin
  python3 tools/prepare_tvos.py template "$TVOS_ENGINE"
  rm -rf build/tvos-project build/tvos build/tvos-derived && mkdir -p build/tvos
  python3 tools/prepare_tvos.py stage
  "$TVOS_GODOT" --headless --path build/tvos-project --editor --import
  "$TVOS_GODOT" --headless --path build/tvos-project --export-release tvOS "$PWD/build/tvos/WA2.ipa"
  python3 tools/prepare_tvos.py project build/tvos
  xcodebuild -project build/tvos/WA2.xcodeproj -scheme WA2 -configuration Release -sdk appletvos \
    -destination 'generic/platform=tvOS' -derivedDataPath build/tvos-derived \
    CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO DEVELOPMENT_TEAM= build -quiet
  python3 tools/package_ipa.py build/tvos-derived/Build/Products/Release-appletvos/WA2.app "$OUT/WA2-AppleTV.ipa" --platform tvos
fi

if [ "${SKIP_MACOS:-0}" != 1 ]; then
  echo "== Mac"
  rm -rf build/WA2.app build/dmg && mkdir -p build/dmg
  "$GODOT" --headless --path . --export-release macOS build/WA2.app
  # Ad-hoc signature: the app is not notarized (see README for Gatekeeper).
  codesign --force --deep --sign - build/WA2.app
  codesign --verify --deep --strict build/WA2.app
  ditto build/WA2.app build/dmg/WA2.app
  ln -s /Applications build/dmg/Applications
  cp assets/legal/THIRD_PARTY_LICENSES.txt build/dmg/
  hdiutil create -quiet -volname "WHITE ALBUM 2" -srcfolder build/dmg -format UDZO -ov "$OUT/WA2-macOS.dmg"
fi

echo "== Licence material"
cp assets/legal/THIRD_PARTY_LICENSES.txt "$OUT/"
cp "$FFMPEG_SOURCE" "$OUT/ffmpeg-7.1.5.tar.xz"
(cd "$OUT" && shasum -a 256 -- * > SHA256SUMS.txt)
echo "Release $VERSION packages:"
ls -la "$OUT"
