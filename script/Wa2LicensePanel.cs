// SPDX-License-Identifier: Apache-2.0
using Godot;

// Shows the bundled open-source notices (LGPL requires a prominent FFmpeg notice).
public partial class Wa2LicensePanel : PanelContainer
{
    public const string NoticePath = "res://assets/legal/THIRD_PARTY_LICENSES.txt";
    private TextEdit _text;
    private Control _returnFocus;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.05f, 0.07f, 0.1f, 0.97f) });
        var margin = new MarginContainer();
        foreach (string edge in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 32);
        AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);
        column.AddChild(new Label { Text = "Open-source licenses / 开源许可", HorizontalAlignment = HorizontalAlignment.Center });
        // TextEdit lays out only visible lines, unlike Label, so ~220 KB stays responsive.
        _text = new TextEdit { Editable = false, SizeFlagsVertical = SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.Boundary,
            ContextMenuEnabled = false };
        _text.AddThemeFontSizeOverride("font_size", Wa2UiMetrics.SmallFontSize);
        column.AddChild(_text);
        var close = new Button { Text = "Close / 关闭", CustomMinimumSize = Wa2UiMetrics.ButtonSize };
        close.Pressed += Close;
        column.AddChild(close);
        Hide();
    }

    public void Open(Control returnFocus)
    {
        _returnFocus = returnFocus;
        if (_text.Text.Length == 0)
            _text.Text = FileAccess.FileExists(NoticePath) ? FileAccess.GetFileAsString(NoticePath) : "Notices file missing: " + NoticePath;
        Show();
        _text.GrabFocus();
    }

    public void Close()
    {
        Hide();
        if (GodotObject.IsInstanceValid(_returnFocus) && _returnFocus.IsVisibleInTree()) _returnFocus.GrabFocus();
    }

    // The focused TextEdit consumes Escape at the GUI stage, so watch raw input.
    public override void _Input(InputEvent input)
    {
        if (Visible && input.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
