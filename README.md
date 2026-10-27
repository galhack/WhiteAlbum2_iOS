**简体中文** | [English](README_EN.md)

# WHITE ALBUM 2 · Apple 移植版

在 iPhone、iPad、Apple Vision Pro、Apple TV 和 Mac 上原生运行《WHITE ALBUM 2》。

本项目是一个**引擎移植**：基于 [dorakyuraduang/wa2-godot](https://github.com/dorakyuraduang/wa2-godot) 的 Godot 4 .NET 重写版引擎，增加了 Apple 平台适配、触屏与遥控器操作，并做了性能优化。

> **安装包不含原版游戏资源。** 剧本、图像、音频、影片和游戏字体都需要从你购买的正版 PC 游戏中读取。v0.4.1 内置独立的第三方中文字幕数据，来源与许可见[字幕说明](SUBTITLE_NOTICE.md)。

---

## 目录

- [WHITE ALBUM 2 · Apple 移植版](#white-album-2--apple-移植版)
  - [目录](#目录)
  - [支持的设备](#支持的设备)
  - [准备工作](#准备工作)
  - [第一步：准备游戏数据](#第一步准备游戏数据)
  - [第二步：获取安装包](#第二步获取安装包)
  - [第三步：安装到设备](#第三步安装到设备)
    - [iPhone / iPad（Sideloadly）](#iphone--ipadsideloadly)
    - [Apple Vision Pro（Xcode）](#apple-vision-proxcode)
    - [Apple TV（Xcode）](#apple-tvxcode)
    - [Mac](#mac)
  - [第四步：导入游戏数据](#第四步导入游戏数据)
    - [iPhone / iPad](#iphone--ipad)
    - [Vision Pro 和 Apple TV](#vision-pro-和-apple-tv)
    - [开始游戏](#开始游戏)
  - [操作方式](#操作方式)
  - [语言与字幕](#语言与字幕)
  - [存档与章节解锁](#存档与章节解锁)
  - [常见问题](#常见问题)
  - [从源码构建](#从源码构建)
  - [FFmpeg 与 LGPL](#ffmpeg-与-lgpl)
  - [致谢与许可](#致谢与许可)

---

## 支持的设备

| 设备 | 最低系统 | 运行方式 | 已实机验证 |
| --- | --- | --- | --- |
| iPhone | iOS 15 | 原生应用 | iPhone 16 Pro Max · iOS 26.6 |
| iPad | iPadOS 15 | 原生应用 | iPad Pro 13 英寸（M5）· iPadOS 26.4 |
| Apple Vision Pro | visionOS（兼容 iPad 应用） | 空间中的平面窗口，注视＋捏合操作 | Vision Pro 第一代 · visionOS 27.2 |
| Apple TV | tvOS 17 | 独立的 tvOS 原生应用，Siri Remote 操作 | Apple TV 4K（第三代，2022）· tvOS 17.6 |
| Mac | macOS 12，Apple 芯片 | 原生应用，窗口或全屏 | MacBook Pro（M1 Max）· macOS 26.6 |

所有版本均面向 Apple Silicon／ARM64。iPhone、iPad、Vision Pro 和 Apple TV 的 C# 使用 NativeAOT，不需要 AltJIT 或越狱；Mac 版附带 .NET 运行时，采用 OpenGL 兼容渲染。

## 准备工作

- **正版《WHITE ALBUM 2》PC 版**：需要本篇（含 IC/CC/Coda），可选择另购 Special Contents。
- 一台 **Mac**（推荐）或 Windows 电脑，用来签名安装应用、拷贝游戏数据。在 Mac 上玩则不需要其他设备。
- 一个 **Apple ID**。免费账户即可，但签名 7 天后过期，需要重签（见[常见问题](#常见问题)）。
- 设备上至少要有 **12 GB 可用空间**：本篇约 7.9 GB，Special Contents 约 3.5 GB。
- 安装工具：
  - iPhone / iPad：[Sideloadly](https://sideloadly.io/)（macOS / Windows）
  - Vision Pro / Apple TV：[Xcode](https://developer.apple.com/xcode/)（仅 macOS）

## 第一步：准备游戏数据

游戏数据放在两个文件夹里。**文件夹名必须完全一致**：

| 文件夹 | 内容 |
| --- | --- |
| `Wa2Res` | 本篇。PC 游戏安装目录里的 `.pak` 文件，以及 `IC/` 子文件夹 |
| `Wa2SC` | Special Contents（可选），其安装目录里的 `.pak` 文件 |

`Wa2Res` 的结构大致如下（不同版本、补丁的文件会略有差别）：

```text
Wa2Res/
├── BGM.PAK  SE.PAK  VOICE.PAK  bak.pak  char.pak  grp.pak
├── script.pak  fnt.pak          ← 日文剧本与字体
├── ck-gal.pak  fon.pak          ← 简体中文（CK-GAL 汉化补丁，可选）
├── mv*.pak  MV*.pak             ← 影片
└── IC/
    ├── BGM.PAK  SE.PAK  VOICE.PAK  bak.pak  char.pak  grp.pak  script.pak
    └── mv*.pak ...
```

**推荐**用仓库自带的脚本整理，它只复制需要的文件，跳过 Windows 程序，也不会修改原始文件：

```sh
python3 tools/prepare_game_data.py "/PC游戏安装目录" ~/Desktop/Wa2Res
python3 tools/prepare_game_data.py "/Special Contents 安装目录" ~/Desktop/Wa2SC
```

不用脚本也可以：直接把整个游戏安装文件夹复制一份，改名为 `Wa2Res`。**注意不要多套一层文件夹**，`Wa2Res` 里面应该直接就是 `.pak` 文件和 `IC/`。

## 第二步：获取安装包

当前移动设备版本：[v0.4.1 · Build 10](https://github.com/galhack/WhiteAlbum2_iOS/releases/tag/v0.4.1-build10)，适用于 iPhone、iPad 和 Vision Pro。Mac 保留 Build 8；Apple TV 暂停更新，除非维护者另行通知。

在本仓库的 [Releases](https://github.com/galhack/WhiteAlbum2_iOS/releases) 页面下载：

| 文件 | 用途 |
| --- | --- |
| [WA2-iPhone-iPad.ipa](https://github.com/galhack/WhiteAlbum2_iOS/releases/latest/download/WA2-iPhone-iPad.ipa) | iPhone / iPad，未签名，用 Sideloadly 以你自己的 Apple ID 签名安装 |
| [WA2-AppleTV.ipa](https://github.com/galhack/WhiteAlbum2_iOS/releases/download/v0.4.1/WA2-AppleTV.ipa) | Apple TV，Build 8，未签名；暂停更新 |
| [WA2-macOS.dmg](https://github.com/galhack/WhiteAlbum2_iOS/releases/download/v0.4.1/WA2-macOS.dmg) | Mac（Apple 芯片），Build 8 |
| [ffmpeg-7.1.5.tar.xz](https://github.com/galhack/WhiteAlbum2_iOS/releases/latest/download/ffmpeg-7.1.5.tar.xz) | 应用所用 FFmpeg 的对应源码（LGPL 要求随附，玩游戏用不到） |
| [SHA256SUMS.txt](https://github.com/galhack/WhiteAlbum2_iOS/releases/latest/download/SHA256SUMS.txt) | 校验值 |

安装包里**不含原版游戏资源**（内置独立的第三方中文字幕）。也可以按[从源码构建](#从源码构建)自己编译。

## 第三步：安装到设备

### iPhone / iPad（Sideloadly）

1. 用数据线连接设备，解锁，在设备上点"信任此电脑"。
2. 打开 Sideloadly，把 `WA2-iPhone-iPad.ipa` 拖进窗口。
3. 在 *iDevice* 里选中你的设备，填入 Apple ID，点击 **Start**，按提示输入密码和两步验证码。密码只发给 Apple，不要发给其他人。
4. 安装完成后，在设备上打开 **设置 → 通用 → VPN 与设备管理**，信任你的 Apple ID 对应的开发者证书。
5. iOS 16 及以上首次运行时，还要在 **设置 → 隐私与安全性 → 开发者模式** 中打开开发者模式，并按提示重启。

> 免费 Apple ID 最多同时安装 3 个自签应用，签名 7 天有效。在 Sideloadly 中勾选 *Automatic refresh*，电脑开着且设备在同一网络时会自动续签。升级时请继续使用**同一个 Apple ID**，否则会被当作另一个应用安装，导入的数据和存档就看不到了。

### Apple Vision Pro（Xcode）

Vision Pro 运行的是 iPad 兼容版，和 iPhone/iPad 是同一个应用。目前 Sideloadly 无法识别 Vision Pro，需要用 Xcode 安装：

1. 在 Vision Pro 上打开 **设置 → 通用 → 远程设备**，用 Xcode 配对（Mac 上：Xcode → Window → Devices and Simulators）。在 Vision Pro 的 **设置 → 隐私与安全性** 中打开开发者模式。
2. 按[从源码构建](#从源码构建)导出 iOS 工程，然后用你自己的团队签名，构建并安装（把 `你的团队ID` 换成 Xcode 账户里显示的 Team ID）：

```sh
xcodebuild -project build/ios/WA2.xcodeproj -scheme WA2 -configuration Release \
  -destination 'generic/platform=iOS' -derivedDataPath build/ios-signed \
  -allowProvisioningUpdates CODE_SIGN_STYLE=Automatic CODE_SIGN_IDENTITY="Apple Development" \
  DEVELOPMENT_TEAM=你的团队ID PRODUCT_BUNDLE_IDENTIFIER=org.local.wa2.apple.你的团队ID build
xcrun devicectl list devices          # 找到 Vision Pro 的设备 ID
xcrun devicectl device install app --device 设备ID build/ios-signed/Build/Products/Release-iphoneos/WA2.app
```

也可以在 Xcode 里打开 `build/ios/WA2.xcodeproj`，在 *Signing & Capabilities* 里选择你的团队，运行目标选 Vision Pro（Designed for iPad），然后点运行。

### Apple TV（Xcode）

1. 在 Apple TV 上打开 **设置 → 遥控器与设备 → 遥控 App 与设备**，然后在 Mac 的 Xcode → Devices and Simulators 中完成配对。
2. 按 [APPLE_TV.md](APPLE_TV.md) 构建 tvOS 工程，然后签名安装：

```sh
xcodebuild -project build/tvos/WA2.xcodeproj -scheme WA2 -configuration Release \
  -destination 'platform=tvOS,id=设备ID' -derivedDataPath build/tvos-signed \
  -allowProvisioningUpdates CODE_SIGN_STYLE=Automatic CODE_SIGN_IDENTITY="Apple Development" \
  DEVELOPMENT_TEAM=你的团队ID PRODUCT_BUNDLE_IDENTIFIER=org.local.wa2.tv.你的团队ID build
xcrun devicectl device install app --device 设备ID build/tvos-signed/Build/Products/Release-appletvos/WA2.app
```

> Sideloadly 新版也支持 Apple TV，但我们测试时它与这台电视的配对报 `Mux error (-8)`，所以目前以 Xcode 方式为准。

### Mac

1. 打开 `WA2-macOS.dmg`，把 **WA2** 拖进"应用程序"文件夹。
2. 本应用没有经过 Apple 公证。第一次打开时，如果系统提示无法验证开发者：打开 **系统设置 → 隐私与安全性**，在页面下方点 **仍要打开**；或者在终端执行：

```sh
xattr -dr com.apple.quarantine /Applications/WA2.app
```

3. Mac 版**不需要拷贝数据**：在启动界面点 **Choose folder / 选择目录**，直接选择 PC 游戏所在的文件夹（含 `IC/` 的本篇目录，或 Special Contents 目录）即可。也可以使用整理好的 `Wa2Res` / `Wa2SC`。
4. 窗口大小可以随意调整，点绿色按钮进入全屏。

## 第四步：导入游戏数据

### iPhone / iPad

任选一种方式：

- **Mac 访达**：连接设备 → 在访达侧栏选中设备 → **文件** 标签页 → 展开 **wa2** → 把 `Wa2Res`、`Wa2SC` **整个文件夹**拖进去。
- **Windows**：用"Apple 设备"App（或 iTunes）的 **文件共享**，同样拖入两个文件夹。
- **设备上的"文件"App**：应用会出现在 **我的 iPhone / 我的 iPad → wa2** 里，可以从 iCloud 云盘、U 盘或移动硬盘复制进来。

拷贝 10 GB 左右的数据需要一段时间，请等它完全结束再打开游戏。

### Vision Pro 和 Apple TV

这两种设备没有访达文件共享，需要用命令行拷贝。`resource-transfer` 文件夹里应该直接放 `Wa2Res` 和 `Wa2SC`：

```sh
# Vision Pro：拷入应用的 Documents
xcrun devicectl device copy to --device 设备ID --source ~/Desktop/resource-transfer \
  --destination Documents --domain-type appDataContainer --domain-identifier org.local.wa2.apple.你的团队ID

# Apple TV：拷入应用的 Library/Caches
xcrun devicectl device copy to --device 设备ID --source ~/Desktop/resource-transfer \
  --destination Library/Caches --domain-type appDataContainer --domain-identifier org.local.wa2.tv.你的团队ID
```

> **不要**加 `--remove-existing-content` 参数，它可能清空整个应用容器，包括存档。
>
> tvOS 没有可长期保存大文件的位置，存储空间紧张时系统可能清理游戏数据，届时需要重新导入。**存档和进度不受影响**：它们另外备份在系统偏好设置里，重新导入数据后会自动恢复。详见 [APPLE_TV.md](APPLE_TV.md)。

### 开始游戏

打开应用后，在启动界面点 **Main / 本体** 或 **Special Contents**，选择语言，再点 **开始游戏**。

启动前可关闭 **显示右上角菜单 / Show corner menu**，隐藏游戏画面的 **≡** 按钮。默认开启，点击开始游戏后记住选择；下次启动时可以重新开启。这个按钮和暂停面板由 Apple 移植版添加。隐藏后，原有存档、读档、设置、历史、自动、快进和隐藏文字按钮仍可使用；返回标题需使用暂停菜单，可在下次启动前重新开启按钮。

Special Contents 的九篇数字小说按下表顺序显示全称。菜单会随所选语言显示中文、日文或英文标题。

| 原菜单编号 | 中文全称 | 日文原名 |
| --- | --- | --- |
| 数字小说 1 | 他的神明、那家伙的救世主 | 彼の神様、あいつの救世主 |
| 数字小说 2 | 雪融化，然后直到再次飘落时 | 雪が解け、そして雪が降るまで |
| 数字小说 3 | 祭典之后～雪菜的三十分钟～ | 祭りの後～雪菜の三十分～ |
| 数字小说 4 | Twinkle Snow～梦想～ | Twinkle Snow～夢想～ |
| 数字小说 5 | 忘却歌声的偶像 | 歌を忘れた偶像（アイドル） |
| 数字小说 6 | 传达不到的爱恋，传达到了 | 届かない恋、届いた |
| 数字小说 7 | 雪菜公主的受难与大臣的计谋 | 雪菜姫の受難と大臣の悪巧み |
| 数字小说 8 | 台风过后的小春日和 | 台風一過の小春日和 |
| 数字小说 9 | 已经不再是白色相簿的季节了 | もう、ホワイトアルバムの季節じゃない |

日文原名可参照 [AQUAPLUS 特典目录](https://aquaplus.jp/wa2/special_early.html)；中文和英文菜单使用对应译名。

## 操作方式

| 操作 | 触屏（iPhone / iPad） | Vision Pro | Siri Remote（Apple TV） | 键盘鼠标 |
| --- | --- | --- | --- | --- |
| 推进文字 | 点击画面 | 注视后捏合 | 按下触控板（Select） | 左键 / Enter |
| 打开菜单 | 右上角 **≡** 按钮 | 注视 ≡ 后捏合 | **Menu / 返回** 键 | Esc，或点右上角 ≡ |
| 快进 | 菜单 → 快进；或长按画面（需在设置中开启） | 菜单 → 快进 | 菜单 → 快进 | 按住 Ctrl |
| 自动播放 | 菜单 → 自动，或底部快捷栏 | 同左 | **播放/暂停** 键，或菜单 → 自动 | 快捷栏 |
| 历史记录 | 在游戏画面空白处下拉（含对话框外），或点底部历史按钮／菜单 → 历史；打开后上下拖动翻页 | 底部历史按钮／菜单 → 历史 | 菜单 → 历史，上下键翻页 | 滚轮 |
| 存档 / 读档 | 菜单或底部快捷栏 | 同左 | 菜单 → 存档 / 读档 | 快捷栏 |
| 返回标题 | 菜单 → 标题 | 同左 | 菜单 → 标题 | Esc → 标题 |
| 关闭菜单 | 点击菜单外的空白处 | 同左 | Menu 键 | Esc |

- Apple TV 上，方向键或在触控板上滑动来移动蓝色焦点框；在启动界面和标题主菜单按 Menu 键会回到 Apple TV 主屏幕。
- 底部快捷栏和设置菜单里的小按钮，在 iPhone、iPad 和 Vision Pro 上都扩大了点击范围，但不会抢占相邻按钮的位置。
- 游戏画面轻点松手后推进文字，下拉打开历史时不会推进。全屏数字小说也支持下拉；选项、影片和其他菜单不会被历史手势接管，原有快捷按钮继续可用。
- 横屏两个方向都可以，翻转设备时画面会跟着转。

## 语言与字幕

| 语言 | 需要的文件 |
| --- | --- |
| 日本語 | 原版 `script.pak` + `fnt.pak` |
| 简体中文 | CK-GAL 汉化补丁的 `ck-gal.pak` + `fon.pak`（Special Contents 用 `script_cn.pak` + `fon.pak`） |
| English / 繁體中文 | 需要自备语言包和配置文件，格式见 [APPLE_PORT.md](APPLE_PORT.md) |

**v0.4.1 已内置简体中文字幕**，iPhone、iPad、Vision Pro、Apple TV 和 Mac 使用同一套字幕实现，选择**简体中文**后自动启用，无需额外复制字幕或安装 Windows DLL。

- 来源：[nxs7/WA2-CHS-Subtitle-Patch](https://github.com/nxs7/WA2-CHS-Subtitle-Patch)，保留原项目 MIT 许可与翻译署名，详见 [SUBTITLE_NOTICE.md](SUBTITLE_NOTICE.md)。
- 本体：6 组音效字幕和 40 组语音字幕，覆盖原补丁提供的内容。
- 本体的追加字幕显示在**画面顶部中央**，例如 CC 开头的“峰城广播站”和随后广播采访的对白；普通剧情对话继续使用所选语言的剧本。
- Special Contents：在篇章列表选择 **广播剧 / Radio dramas**，再选择广播剧与章节。支持原补丁提供的 23 章对白字幕及 5 组歌词，同步显示对白和歌曲文字；支持暂停、重播、下一章及同一广播剧内自动连播。音频仍读取自己的游戏资源。
- 原补丁未提供『2016 年的除夕』Talk Comment 章节字幕，本版也不会凭空补译。没有字幕的音轨仍可播放。
- 日文、英文和繁体中文模式不会叠加简体中文字幕。中日文剧本仍需相应的游戏资源／汉化包。
- 如需自定义，将 YAML 放到 `<游戏目录>/subtitles/zh-Hans.yaml`，优先于对应版本的内置字幕。本体支持原项目的 `soundEffect/voice` 及旧版 `soundSubtitle/voiceSubtitle` 格式；Special Contents 支持 `voiceDrama/song`。两套资源与时间轴互不混用。

## 存档与章节解锁

- 存档和进度只保存在本机，不同设备之间不同步。本篇和 Special Contents、每种语言各自独立保存。
- 本篇一开始只能选 IC；通关 IC 后开放 CC；达成 CC 中解锁 Coda 的结局后开放 Coda，和原作一致。
- iPhone / iPad 上可以用访达或"文件"App 备份 `wa2/saves` 文件夹。**删除应用会同时删除存档和导入的数据**，删除前请先备份。
- 用同一个 Apple ID 和同一个应用标识覆盖安装新版本，存档和数据都会保留。
- PC 剧情存档与移植版内部格式不同，不能只改文件名；可使用[PC 存档转换工具](PC_SAVE_IMPORT.md)。Mac 本体简体中文存档目录为 `~/Library/Application Support/Godot/app_userdata/wa2/saves/zh-Hans/`。

## 常见问题

**打开后提示找不到资源 / Missing game resource**
数据文件夹名称或层级不对。确认应用文件里直接就是 `Wa2Res/BGM.PAK`，而不是 `Wa2Res/Wa2Res/BGM.PAK`，也不是 `WHITE ALBUM2/BGM.PAK`。

**选了简体中文却显示乱码或报错**
本篇的中文需要 CK-GAL 补丁的 `ck-gal.pak` 和 `fon.pak`，请确认它们在 `Wa2Res` 里。

**应用打开就闪退**
多半是免费签名过期（7 天）。用 Sideloadly 以同一个 Apple ID 重新安装一次即可，数据和存档会保留。

**Sideloadly 提示达到应用数量上限**
免费账户同一台设备最多 3 个自签应用，需要先删除其他自签应用。注意 App ID 的数量限制和设备上的安装数量限制是两回事。

**Apple TV 上游戏数据不见了**
tvOS 在存储空间不足时清理了缓存。重新导入游戏数据即可，存档会自动恢复。

**卡顿或发热**
请使用 Release 版（本仓库默认导出 Release）。0.4.0 版已消除换表情、换背景、切换 BGM 时的重复解码，等待点击时也不再每帧重绘文字。

## 从源码构建

需要 macOS（Apple Silicon）、Xcode、[Godot 4.5.1 .NET](https://godotengine.org/download/) 及其导出模板、.NET SDK 9、Python 3 和 SCons。

```sh
# 1. 编译检查和测试
dotnet build wa2.csproj
dotnet run --project tests/LaunchOptions/LaunchOptions.csproj
python3 -m unittest discover -s tests -p 'test_*.py'

# 2. iPhone / iPad / Vision Pro：导出 Xcode 工程并生成未签名 IPA
godot --headless --path . --export-release iOS build/ios/WA2.ipa
python3 tools/prepare_ios_project.py build/ios
xcodebuild -project build/ios/WA2.xcodeproj -scheme WA2 -configuration Release -sdk iphoneos \
  -destination 'generic/platform=iOS' -derivedDataPath build/ios-derived \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO DEVELOPMENT_TEAM= build
python3 tools/package_ipa.py build/ios-derived/Build/Products/Release-iphoneos/WA2.app build/WA2-iPhone-iPad.ipa
```

- 一次性生成全部发行包（iOS / tvOS / Mac、许可文件、FFmpeg 源码与校验值）：`GODOT=… TVOS_ENGINE=… FFMPEG_SOURCE=… tools/build_release.sh`，输出在 `build/release/`。
- FFmpeg 视频插件和 iOS 模板的准备步骤见 [APPLE_PORT.md](APPLE_PORT.md) 和 [addons/wmv_video/BUILDING.md](addons/wmv_video/BUILDING.md)。
- Apple TV 使用锁定版本的 Godot tvOS 开发分支和本仓库的引擎补丁，完整步骤见 [APPLE_TV.md](APPLE_TV.md)。
- 应用图标由 `tools/make_app_icons.py` 生成，是原创的几何雪花图案。

## FFmpeg 与 LGPL

应用使用 [FFmpeg](https://ffmpeg.org/) 7.1.5 播放原作的 WMV/WMA 影片。FFmpeg 采用 GNU LGPL 2.1 或更高版本授权，本项目的构建只启用 LGPL 组件（未使用 `--enable-gpl`、`--enable-nonfree`、`--enable-version3`），只包含 ASF 解封装器和 WMV/VC-1/WMA 解码器。

- **Mac 版**：FFmpeg 以动态库形式放在 `WA2.app/Contents/Frameworks`，可以直接替换。
- **iPhone / iPad / Vision Pro / Apple TV 版**：FFmpeg 静态链接进应用。为满足 LGPL 的要求，本仓库公开了应用的全部源码和构建脚本，每个 Release 都附带对应的 FFmpeg 源码包，应用内（启动界面 → **Licenses / 开源许可**）也附有完整的 LGPL 文本。

用修改过的 FFmpeg 重新构建 iOS 版：

```sh
# 1. 用你修改过的 FFmpeg 源码构建静态库（配置选项见脚本）
python3 tools/build_ios_ffmpeg.py /path/to/your/ffmpeg --jobs 8
# 2. 用新库重新编译视频插件
python3 addons/wmv_video/tools/build.py --platform ios --target both --jobs 8
# 3. 按"从源码构建"重新导出、编译、打包 IPA，再用 Sideloadly 安装
```

Apple TV 版的对应步骤见 [APPLE_TV.md](APPLE_TV.md)。你可以为自用目的修改本应用，并为调试这些修改而对其进行逆向工程。

## 致谢与许可

- 引擎重写：[dorakyuraduang/wa2-godot](https://github.com/dorakyuraduang/wa2-godot) 及其贡献者。
- [Godot Engine](https://godotengine.org/)（MIT）、[FFmpeg](https://ffmpeg.org/)（LGPL 2.1+）、godot-cpp、.NET 运行时、YamlDotNet、Noto Sans CJK（OFL），各自保留原许可证，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 和 [LICENSES/](LICENSES/)。
- 本仓库按文件分别授权：上游移植代码和 Apple 移植新增的代码、工具、文档采用 **Apache-2.0**；`addons/wmv_video` 以及两个派生自 Godot / godot-cpp 的文件采用 **MIT**；FFmpeg 采用 **LGPL-2.1-or-later**。完整范围见 [LICENSE](LICENSE)。

《WHITE ALBUM 2》及其全部内容的著作权和商标归 Leaf / AQUAPLUS 所有。本项目与权利人没有任何关联，也未获得其认可。本项目不分发原版游戏资源；内置的第三方字幕保留各自的来源与许可。引擎仅供已购买正版游戏的用户在自己的设备上游玩。请支持正版。
