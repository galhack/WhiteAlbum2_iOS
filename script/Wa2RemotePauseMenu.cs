// SPDX-License-Identifier: Apache-2.0
using Godot;
using System;

public partial class Wa2RemotePauseMenu : PanelContainer
{
    private Control _column;

    public override void _Ready()
    {
        Name = "RemotePauseMenu";
        Theme = Wa2UiMetrics.CreateTheme(Wa2UiMetrics.Embedded ? 26 : 22);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.72f) });
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Pass };
        AddChild(center);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 20);
        center.AddChild(column);
        _column = column;
        column.AddChild(new Label { Text = "Pause / 暂停 / 一時停止", HorizontalAlignment = HorizontalAlignment.Center });
        // Two columns keep every action on one 720p screen with 64 px targets.
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 20);
        grid.AddThemeConstantOverride("v_separation", 14);
        column.AddChild(grid);
        var game = Wa2EngineMain.Engine;
        AddButton(grid, "Resume / 继续 / 再開", Close);
        AddButton(grid, "Backlog / 历史 / バックログ", () => { Close(); game.AdvMain.OnBackLogButtonDown(); });
        AddButton(grid, "Save / 存档 / セーブ", () => { Close(); game.UiMgr.OpenSaveMenu(); });
        AddButton(grid, "Load / 读档 / ロード", () => { Close(); game.UiMgr.OpenLoadMenu(); });
        // Open() stops auto and skip, so these toggles always switch them on.
        AddButton(grid, "Auto / 自动 / オート", () => { Close(); game.AdvMain.OnAutoButtonDown(); });
        AddButton(grid, "Skip / 快进 / スキップ", () => { Close(); game.AdvMain.OnSkipButtonDown(); });
        AddButton(grid, "Options / 设置 / 設定", () => { Close(); game.UiMgr.OpenOptionsMenu(); });
        AddButton(grid, "Hide text / 隐藏文字 / 非表示", () => { Close(); game.AdvMain.OnOffButtonDown(); });
        if (Wa2TvStorage.IsEnabled) AddButton(grid, "Local saves / 本地存档", () => {
            Close();
            var menu = new Wa2LocalSaveMenu();
            game.UiMgr.AddChild(menu);
            menu.Open();
        });
        AddButton(grid, "Title / 标题 / タイトル", () => {
            Close();
            game.UiMgr.OpenConfirm("返回标题？ / Return to title?", "", true, () => {
                game.UiMgr.UIConfirm.Close();
                game.SoundMgr.StopAll();
                game.UiMgr.OpenTitleMenu();
            });
        });
        string hint = Wa2TvStorage.IsEnabled ? "↑ ↓ ← → 选择 · Select 确认 · Menu 返回" : "点按空白处继续 · Tap outside to resume";
        column.AddChild(new Label { Text = hint, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.7f) });
        Hide();
    }

    private static void AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(380, 64) };
        button.Pressed += action;
        parent.AddChild(button);
    }

    public override void _GuiInput(InputEvent input)
    {
        // Touch users have no Menu key: a tap on the dimmed area resumes.
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click &&
            !_column.GetGlobalRect().HasPoint(GetGlobalTransform() * click.Position))
        {
            Close();
            AcceptEvent();
        }
    }

    public void Open()
    {
        var game = Wa2EngineMain.Engine;
        game.StopAutoMode();
        game.StopSkip();
        game.UiMgr.UiQueue.Push(this);
        Show();
    }

    public void Close() => Wa2EngineMain.Engine.UiMgr.ReturnScene();
}
