# WMV Video Third-Party Notices

This plugin is licensed under the MIT License in `LICENSE`.

It uses FFmpeg 7.1.5 libraries built under the GNU LGPL version 2.1 or later. GPL and nonfree features are disabled. Desktop and Android distributions use shared libraries. The optional iOS build uses static XCFramework dependencies and therefore requires the application distributor to provide a practical relinking mechanism or obtain a different FFmpeg license. The full LGPL text is in `licenses/FFmpeg-LGPL-2.1-or-later.txt`, and the exact upstream source is available at:

https://github.com/FFmpeg/FFmpeg/tree/n7.1.5

The GDExtension is built with godot-cpp from commit `27d9dd23c83871e0619fca5dc2cddfbfd69e926a`, licensed under MIT. Its license is in `licenses/godot-cpp-MIT.txt`.

The Windows package includes `libwinpthread-1.dll` from mingw-w64. Its MIT/BSD-style terms are reproduced in `licenses/libwinpthread-COPYING.txt`.
