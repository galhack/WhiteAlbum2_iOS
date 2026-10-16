// SPDX-License-Identifier: Apache-2.0
using Godot;
using System;
using System.IO;
using System.Linq;

public partial class Wa2LocalSaveMenu : PanelContainer
{
    private VBoxContainer _slots;
    private Label _status;

    public override void _Ready()
    {
        Theme = Wa2UiMetrics.CreateTheme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        foreach (string edge in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 40);
        AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        margin.AddChild(column);
        column.AddChild(new Label { Text = "Local saves / 本地存档", HorizontalAlignment = HorizontalAlignment.Center });
        column.AddChild(new Label { Text = "仅清理当前版本和语言的手动存档。保留通关、已读和设置。", HorizontalAlignment = HorizontalAlignment.Center });
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(scroll);
        _slots = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_slots);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_status);
        var back = new Button { Text = "Back / 返回", CustomMinimumSize = Wa2UiMetrics.ButtonSize };
        back.Pressed += Close;
        column.AddChild(back);
        Hide();
    }

    public void Open()
    {
        Refresh();
        Wa2EngineMain.Engine.UiMgr.UiQueue.Push(this);
        Show();
    }

    private void Refresh()
    {
        foreach (Node child in _slots.GetChildren()) { _slots.RemoveChild(child); child.QueueFree(); }
        _status.Text = "";
        string directory = ProjectSettings.GlobalizePath(Wa2EngineMain.Engine.SavPath);
        var paths = Directory.EnumerateFiles(directory, "sav??.sav").OrderBy(p => p, StringComparer.Ordinal).ToArray();
        if (paths.Length == 0) _status.Text = "没有手动存档 / No manual saves";
        foreach (string path in paths)
        {
            string relative = Path.GetRelativePath(ProjectSettings.GlobalizePath("user://"), path).Replace(Path.DirectorySeparatorChar, '/');
            if (!Wa2LocalSnapshot.IsSavePath(relative)) continue;
            var button = new Button { Text = $"Delete / 删除 {Path.GetFileName(path)} · {File.GetLastWriteTime(path):yyyy-MM-dd HH:mm}", CustomMinimumSize = Wa2UiMetrics.ButtonSize };
            button.Pressed += () => Wa2EngineMain.Engine.UiMgr.OpenConfirm("删除 " + Path.GetFileName(path) + "？此操作无法撤销。", "", true, () => {
                try { Wa2TvStorage.DeleteSave(relative); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _status.Text = ex.Message; GD.PrintErr(ex); }
                Wa2EngineMain.Engine.UiMgr.UIConfirm.Close();
                string error = _status.Text;
                Refresh();
                _status.Text = error;
            });
            _slots.AddChild(button);
        }
    }

    public void Close() { Wa2EngineMain.Engine.UiMgr.ReturnScene(); QueueFree(); }
}
