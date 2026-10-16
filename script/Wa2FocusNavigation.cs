// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;

public readonly record struct Wa2FocusRect(float X, float Y, float Width, float Height);

public static class Wa2FocusNavigation
{
    // Inputs are already filtered to visible, enabled controls in the active menu.
    public static int Next(IReadOnlyList<Wa2FocusRect> items, int current, int dx, int dy)
    {
        if (items.Count == 0) return -1;
        if (current < 0 || current >= items.Count) return 0;
        var origin = items[current];
        float best = float.PositiveInfinity;
        int result = current;
        for (int i = 0; i < items.Count; i++)
        {
            if (i == current) continue;
            var target = items[i];
            float x = target.X + target.Width / 2 - origin.X - origin.Width / 2;
            float y = target.Y + target.Height / 2 - origin.Y - origin.Height / 2;
            float forward = x * dx + y * dy;
            if (forward <= 0.01f) continue;
            float cross = Math.Abs(x * dy + y * dx);
            float gap = dx == 0
                ? Math.Max(0, Math.Max(target.X - origin.X - origin.Width, origin.X - target.X - target.Width))
                : Math.Max(0, Math.Max(target.Y - origin.Y - origin.Height, origin.Y - target.Y - target.Height));
            float score = forward + gap * 4 + cross * 0.25f;
            if (score < best) { best = score; result = i; }
        }
        return result;
    }
}
