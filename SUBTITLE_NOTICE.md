# Bundled Chinese subtitles (v0.4.1)

The following files are unmodified copies from
[nxs7/WA2-CHS-Subtitle-Patch](https://github.com/nxs7/WA2-CHS-Subtitle-Patch),
revision `bb3cbc2f459549f8afbcb56cafd4b861d763bd29`:

| Bundled file | Upstream source | Coverage |
| --- | --- | --- |
| `assets/subtitles/main.zh-Hans.yaml` | `script/sub.yaml` | 6 sound-effect and 40 voice entries |
| `assets/subtitles/special.zh-Hans.yaml` | `SpecialContents/script/sub.yaml` | 23 drama chapters and 5 song entries |

The upstream project declares MIT licensing, copyright (c) 2022 nxs7. Its full
notice is retained in `LICENSES/WA2-CHS-Subtitle-Patch-MIT.txt`, reproduced in
the application's Licenses screen and release notices. These data files are
separate from the Apple port's Apache-2.0 source code.

Original upstream credits are retained: the main patch credits the English
translation by [TodokanaiTL/WA2EnglishPatch](https://github.com/TodokanaiTL/WA2EnglishPatch);
YAML comments credit 萌娘百科 (Moegirlpedia) for certain lyrics. No license to the
underlying WHITE ALBUM 2 audio, lyrics or other original game material is granted
by this port. Users provide their own purchased game resources.

These are the upstream patch's supplied captions, not a complete new game
translation. As documented upstream, Special Contents does not include captions
for the 2016 New Year's Eve Talk Comment chapter.

Only Simplified Chinese mode enables these captions. User-provided files at
`<game data>/subtitles/zh-Hans.yaml` override the corresponding edition's bundled
file. Original main-game and Special Contents datasets are kept separate.

The old local `assets/sub.yaml` mixed-source file remains excluded from Git and
release packages. It is not the source of these newly bundled files.
