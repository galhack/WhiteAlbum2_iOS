// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;

// Touch helpers kept free of Godot types so they can run in the fixture project.
public static class Wa2TouchInput
{
    // Minimum hit size in 1280x720 canvas pixels: about 39 pt on iPhone 16 Pro Max
    // and 68 pt on a 13-inch iPad, without changing any button artwork.
    public const float MinimumTarget = 64f;

    // Pixels of vertical drag that scroll the backlog by one entry.
    public const float ScrollStep = 56f;

    public static Wa2FocusRect Expanded(Wa2FocusRect rect, float minimum = MinimumTarget)
    {
        float grow_x = Math.Max(0, (minimum - rect.Width) / 2);
        float grow_y = Math.Max(0, (minimum - rect.Height) / 2);
        return new Wa2FocusRect(rect.X - grow_x, rect.Y - grow_y, rect.Width + grow_x * 2, rect.Height + grow_y * 2);
    }

    public static bool Contains(Wa2FocusRect rect, float x, float y) =>
        x >= rect.X && y >= rect.Y && x < rect.X + rect.Width && y < rect.Y + rect.Height;

    // A point in the enlarged margin only counts when no other visible button
    // owns it, so dense menus never lose part of a neighbour's real area.
    public static bool Hit(Wa2FocusRect own, float x, float y, IEnumerable<Wa2FocusRect> others, float minimum = MinimumTarget)
    {
        if (Contains(own, x, y)) return true;
        if (!Contains(Expanded(own, minimum), x, y)) return false;
        foreach (var other in others)
            if (Contains(other, x, y)) return false;
        return true;
    }

    // Converts accumulated drag distance into whole scroll steps; the remainder
    // is kept so slow drags still scroll. Dragging down reveals older entries.
    public static int DragSteps(ref float accumulated, float deltaY, float step = ScrollStep)
    {
        accumulated += deltaY;
        int steps = (int)(accumulated / step);
        accumulated -= steps * step;
        return -steps;
    }
}
