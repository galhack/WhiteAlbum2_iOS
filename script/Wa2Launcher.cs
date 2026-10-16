// SPDX-License-Identifier: Apache-2.0
using Godot;
using System;
using System.IO;

public partial class Wa2Launcher : Control
{
    private LineEdit _directory;
    private OptionButton _language;
    private Label _status;
    private Wa2AssetLoader _loader;
    private Wa2LaunchOptions _options;
    private readonly string[] _languages = { "ja", "zh-Hans", "en", "zh-Hant" };

    public override void _Ready()
    {
        string storageWarning = Wa2TvStorage.Restore();
        Callable.From(() => { Wa2RemoteNavigation.Ensure(GetTree().Root); Wa2TvStorage.Ensure(GetTree().Root); }).CallDeferred();
        bool embedded = Wa2UiMetrics.Embedded;
        int fontSize = Wa2UiMetrics.FontSize;
        var buttonSize = Wa2UiMetrics.ButtonSize;
        Theme = Wa2UiMetrics.CreateTheme();
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string edge in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 40);
        AddChild(margin);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 16);
        margin.AddChild(box);
        var title = new Label { Text = "WHITE ALBUM 2 · Apple", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", fontSize + 12);
        box.AddChild(title);
        box.AddChild(new Label { Text = "Game data folder / 游戏资源目录 / ゲームデータ" });
        // On iOS the data lives in the app's Documents folder; typing a path only
        // raises a keyboard that covers half of a landscape iPhone.
        _directory = new LineEdit { Editable = !embedded };
        box.AddChild(_directory);
        if (embedded)
        {
            var editions = new HBoxContainer();
            box.AddChild(editions);
            foreach (var edition in new[] { ("Main / 本体", "Wa2Res"), ("Special Contents", "Wa2SC") })
            {
                var button = new Button { Text = edition.Item1, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    CustomMinimumSize = buttonSize };
                button.Pressed += () => _directory.Text = Path.Combine(ProjectSettings.GlobalizePath("user://"), edition.Item2);
                editions.AddChild(button);
            }
        }
        var browse = new Button { Text = "Choose folder / 选择目录", CustomMinimumSize = buttonSize };
        browse.Pressed += Browse;
        box.AddChild(browse);
        // Sandboxed apps cannot read folders picked outside their container.
        if (embedded) browse.Hide();
        if (Wa2TvStorage.IsEnabled) _directory.Hide();
        _language = new OptionButton { CustomMinimumSize = buttonSize };
        foreach (string name in new[] { "日本語", "简体中文 · CK-GAL", "English · translation required", "繁體中文 · 需语言包" }) _language.AddItem(name);
        box.AddChild(_language);
        // Play and the licence viewer share a row so the launcher fits 720 px at phone font sizes.
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 16);
        box.AddChild(actions);
        var play = new Button { Text = "Play / 开始游戏 / 開始", CustomMinimumSize = buttonSize, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        play.Pressed += Play;
        actions.AddChild(play);
        var licenses = new Button { Text = "Licenses / 开源许可", CustomMinimumSize = buttonSize };
        actions.AddChild(licenses);
        var licensePanel = new Wa2LicensePanel();
        licenses.Pressed += () => licensePanel.Open(licenses);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", Wa2UiMetrics.SmallFontSize);
        box.AddChild(_status);
        AddChild(licensePanel);
        string documents = OS.GetName() is "iOS" or "tvOS" ? ProjectSettings.GlobalizePath("user://") : OS.GetSystemDir(OS.SystemDir.Documents);
        string defaultDirectory = Path.Combine(documents, "Wa2Res");
        var config = new ConfigFile();
        if (config.Load("user://launcher.cfg") == Error.Ok)
        {
            defaultDirectory = config.GetValue("game", "directory", defaultDirectory).AsString();
            if (OS.GetName() is "iOS" or "tvOS")
                defaultDirectory = Path.Combine(documents, Path.GetFileName(defaultDirectory.TrimEnd(Path.DirectorySeparatorChar)));
        }
        try
        {
            _options = Wa2LaunchOptions.Parse(OS.GetCmdlineUserArgs(), defaultDirectory);
            _directory.Text = _options.ResourceDirectory;
            string language = config.GetValue("game", "language", _options.Language).AsString();
            if (OS.GetCmdlineUserArgs().Length > 0) language = _options.Language;
            _language.Selected = Math.Max(0, Array.IndexOf(_languages, language));
            _status.Text = "Choose the main game folder (including IC/) or the separate Special Contents folder.\n选择包含 IC/ 的本体目录，或独立的 Special Contents 目录。存档按版本和语言分别保存。";
            if (Wa2TvStorage.IsEnabled) _status.Text += "\n使用 Mac 导入 Wa2Res／Wa2SC。方向键选择，Select 确认，Menu 打开游戏菜单。";
            if (storageWarning.Length > 0) _status.Text += "\n" + storageWarning;
            if (_options.AutoStart) Callable.From(Play).CallDeferred();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
    }

    private void Browse()
    {
        var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenDir, Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true, Title = "Choose WHITE ALBUM 2 data" };
        AddChild(dialog);
        dialog.DirSelected += path => { _directory.Text = path; dialog.QueueFree(); };
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCenteredRatio(0.8f);
    }

    private void Play()
    {
        Wa2EngineMain game = null;
        try
        {
            if (_options?.SaveProbePhase != null && !OS.IsDebugBuild())
                throw new InvalidOperationException("--verify-save requires a debug build.");
            var data = new Wa2GameData(_directory.Text, _languages[_language.Selected]);
            data.Mount();
            _loader = new Wa2AssetLoader();
            ResourceLoader.AddResourceFormatLoader(_loader, true);
            // Validate the selected fonts before loading scenes with external dependencies.
            // An index lookup is enough here; decoding the ~48 MB atlases twice slowed launch.
            if (!Wa2Resource.FileDic.ContainsKey("本体80.tga") || !Wa2Resource.FileDic.ContainsKey("袋影80.tga"))
                throw new InvalidDataException("Selected font archive is missing the dialogue font atlas.");
            var scene = ResourceLoader.Load<PackedScene>("res://main.tscn");
            if (scene == null) throw new InvalidDataException("Cannot load game scene. Check the engine log.");
            game = scene.Instantiate<Wa2EngineMain>();
            GetTree().Root.AddChild(game);
            GetTree().CurrentScene = game;
            var config = new ConfigFile();
            config.SetValue("game", "directory", data.Directory);
            config.SetValue("game", "language", data.Language);
            config.Save("user://launcher.cfg");
            if (!string.IsNullOrEmpty(_options?.StartScript))
            {
                game.InitGame();
                game.StartScript(_options.StartScript);
                game.UiMgr.OpenGame();
                GD.Print($"WA2 smoke: mounted {data.Language}, starting {_options.StartScript}, {Wa2Resource.FileDic.Count} resources.");
                if (_options.SaveProbePhase != null)
                    game.AddChild(new Wa2SavePersistenceProbe { Game = game, Phase = _options.SaveProbePhase });
            }
            else if (data.IsSpecialContents)
            {
                game.InitGame();
                game.UiMgr.OpenTitleMenu();
            }
            Hide();
        }
        catch (Exception ex)
        {
            if (game != null)
            {
                GetTree().CurrentScene = this;
                if (ReferenceEquals(Wa2EngineMain.Engine, game)) Wa2EngineMain.Engine = null;
                game.QueueFree();
            }
            Show();
            _status.Text = ex.Message;
            GD.PrintErr("WA2 launch failed: " + ex);
            if (_loader != null) { ResourceLoader.RemoveResourceFormatLoader(_loader); _loader.Dispose(); _loader = null; }
        }
    }
}
