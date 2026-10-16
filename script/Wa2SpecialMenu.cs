// SPDX-License-Identifier: Apache-2.0
using Godot;
using System;

public partial class Wa2SpecialMenu : PanelContainer
{
    private Label _status;

    public override void _Ready()
    {
        Theme = Wa2UiMetrics.CreateTheme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        foreach (string edge in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 24);
        AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);
        column.AddChild(new Label { Text = "WHITE ALBUM 2 · Special Contents", HorizontalAlignment = HorizontalAlignment.Center });
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(scroll);
        var entries = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(entries);
        foreach (var entry in Wa2Content.SpecialEntries)
        {
            var button = new Button { Text = entry.Label(Wa2GameData.Current.Language), CustomMinimumSize = Wa2UiMetrics.ButtonSize };
            button.Pressed += () => Start(entry.Script);
            entries.AddChild(button);
        }
        var controls = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddChild(controls);
        AddButton(controls, "Load / 读档 / ロード", () => Wa2EngineMain.Engine.UiMgr.OpenLoadMenu());
        AddButton(controls, "Options / 设置 / 設定", () => Wa2EngineMain.Engine.UiMgr.OpenOptionsMenu());
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeFontSizeOverride("font_size", Wa2UiMetrics.SmallFontSize);
        column.AddChild(_status);
        Hide();
    }

    private static void AddButton(Container parent, string title, Action action)
    {
        var button = new Button { Text = title, CustomMinimumSize = Wa2UiMetrics.ButtonSize };
        button.Pressed += action;
        parent.AddChild(button);
    }

    private void Start(string script)
    {
        try
        {
            var game = Wa2EngineMain.Engine;
            game.StartScript(script);
            game.UiMgr.OpenGame();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            GD.PrintErr("Special Contents launch failed: " + ex);
        }
    }
}
