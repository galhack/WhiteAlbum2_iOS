// SPDX-License-Identifier: Apache-2.0
using Godot;

// Sizes for the port's code-built menus on the 1280x720 canvas. One canvas
// pixel is ~0.6 pt on iPhone, ~1.07 pt on iPad and 1.5 pt on a 1080p TV.
public static class Wa2UiMetrics
{
    public static bool Embedded => OS.GetName() is "iOS" or "tvOS";
    public static int FontSize => OS.GetName() switch { "iOS" => 30, "tvOS" => 28, _ => 20 };
    public static int SmallFontSize => System.Math.Max(18, FontSize - 8);
    public static Vector2 ButtonSize => new(0, Embedded ? 72 : 44);

    public static Theme CreateTheme(int? fontSize = null) => new()
    {
        DefaultFont = ResourceLoader.Load<Font>("res://assets/ui_fonts/NotoSansCJKsc-Regular.otf"),
        DefaultFontSize = fontSize ?? FontSize
    };
}
