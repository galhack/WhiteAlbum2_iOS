#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Generate the port's original app icons (geometric snowflake, no game artwork).

Writes SVG sources to assets/app_icon/ and renders PNGs with Godot's SVG
rasterizer:  python3 tools/make_app_icons.py /path/to/godot
"""
import math
import random
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "assets/app_icon"
SIZES = {  # name: (width, height, flake centre x fraction, radius fraction of height)
    "app_icon_1024": (1024, 1024, 0.5, 0.36),
    "tvos_small_400x240": (400, 240, 0.5, 0.38),
    "tvos_large_1280x768": (1280, 768, 0.5, 0.38),
    "tvos_top_shelf_1920x720": (1920, 720, 0.76, 0.40),
    "tvos_top_shelf_wide_2320x720": (2320, 720, 0.78, 0.40),
}


def arm(cx, cy, r, angle):
    """One snowflake arm: spine plus two pairs of branches and a tip fork."""
    def point(dist, a):
        return cx + dist * math.cos(a), cy + dist * math.sin(a)
    segments = [(point(r * 0.16, angle), point(r, angle))]
    for at, length in ((0.42, 0.30), (0.68, 0.22)):
        base = point(r * at, angle)
        for side in (-1, 1):
            a = angle + side * math.radians(55)
            segments.append((base, (base[0] + r * length * math.cos(a), base[1] + r * length * math.sin(a))))
    tip = point(r * 0.88, angle)
    for side in (-1, 1):
        a = angle + side * math.radians(35)
        segments.append((tip, (tip[0] + r * 0.12 * math.cos(a), tip[1] + r * 0.12 * math.sin(a))))
    return segments


def flake(cx, cy, r, width, opacity=1.0):
    paths = []
    for i in range(6):
        for (x1, y1), (x2, y2) in arm(cx, cy, r, math.radians(-90 + 60 * i)):
            paths.append(f"M{x1:.1f} {y1:.1f}L{x2:.1f} {y2:.1f}")
    hexagon = " ".join(f"{'M' if i == 0 else 'L'}{cx + r * 0.16 * math.cos(math.radians(30 + 60 * i)):.1f} "
                       f"{cy + r * 0.16 * math.sin(math.radians(30 + 60 * i)):.1f}" for i in range(6)) + "Z"
    d = " ".join(paths) + " " + hexagon
    glow = f'<path d="{d}" fill="none" stroke="#9fe3ff" stroke-opacity="{0.16 * opacity:.2f}" stroke-width="{width * 2.0:.1f}" stroke-linecap="round" stroke-linejoin="round"/>'
    core = f'<path d="{d}" fill="none" stroke="#ffffff" stroke-opacity="{opacity:.2f}" stroke-width="{width:.1f}" stroke-linecap="round" stroke-linejoin="round"/>'
    return glow + core


def svg(width, height, fx, fr):
    cx, cy, r = width * fx, height / 2, height * fr
    rng = random.Random(2)  # deterministic background flakes
    small = ""
    for _ in range(int(width / height * 6)):
        x, y = rng.uniform(0.04, 0.96) * width, rng.uniform(0.1, 0.9) * height
        if math.hypot(x - cx, y - cy) < r * 1.15:
            continue  # keep the main flake clear
        small += flake(x, y, height * rng.uniform(0.03, 0.07), height * 0.006, 0.35)
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">
<defs>
<linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0a1630"/><stop offset="1" stop-color="#1d6b88"/></linearGradient>
<radialGradient id="light" cx="{fx}" cy="0.5" r="0.55"><stop offset="0" stop-color="#bfefff" stop-opacity="0.35"/><stop offset="1" stop-color="#bfefff" stop-opacity="0"/></radialGradient>
</defs>
<rect width="{width}" height="{height}" fill="url(#bg)"/>
<rect width="{width}" height="{height}" fill="url(#light)"/>
{small}
{flake(cx, cy, r, height * 0.028)}
</svg>
'''


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    OUT.mkdir(parents=True, exist_ok=True)
    jobs = []
    for name, (w, h, fx, fr) in SIZES.items():
        source = OUT / (name + ".svg")
        source.write_text(svg(w, h, fx, fr))
        jobs.append(f'["{source}", "{OUT / (name + ".png")}"]')
    script = OUT.parent.parent / "build/render_icons.gd"
    script.parent.mkdir(exist_ok=True)
    script.write_text(f'''extends SceneTree
func _initialize():
    for job in [{", ".join(jobs)}]:
        var image := Image.new()
        var err := image.load_svg_from_string(FileAccess.get_file_as_string(job[0]), 1.0)
        if err != OK: push_error("render failed " + job[0]); quit(1); return
        image.convert(Image.FORMAT_RGB8)
        image.save_png(job[1])
    quit(0)
''')
    subprocess.run([sys.argv[1], "--headless", "--script", str(script)], check=True)
    print("Rendered", len(jobs), "icons into", OUT)


if __name__ == "__main__":
    main()
