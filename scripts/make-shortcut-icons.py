#!/usr/bin/env python3
"""Draws the four PWA shortcut icons (L-91): 96x96, blue #0047A0, a simple white symbol.

    python3 scripts/make-shortcut-icons.py

Writes src/Lugat.Web/wwwroot/icons/shortcut-*.png. Needs Pillow. The symbol keeps to the middle of the
square so it survives the round or rounded mask the launcher puts on it. After changing an icon, raise
?v= in manifest.webmanifest so phones fetch the new file.
"""
from pathlib import Path

from PIL import Image, ImageDraw

BLUE = (0, 71, 160, 255)
WHITE = (255, 255, 255, 255)
SIZE = 96
SCALE = 8  # drawn large and scaled down for smooth edges
OUT = Path(__file__).resolve().parent.parent / "src" / "Lugat.Web" / "wwwroot" / "icons"


def canvas():
    image = Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE), BLUE)
    return image, ImageDraw.Draw(image)


def s(*values):
    return [v * SCALE for v in values]


def line(draw, points, width=7):
    flat = s(*[c for p in points for c in p])
    draw.line(flat, fill=WHITE, width=width * SCALE, joint="curve")
    r = width * SCALE / 2
    for x, y in (points[0], points[-1]):
        draw.ellipse([x * SCALE - r, y * SCALE - r, x * SCALE + r, y * SCALE + r], fill=WHITE)


def takrorlash():
    # A circular arrow, like the "Takror" tab.
    image, draw = canvas()
    draw.arc(s(28, 28, 68, 68), start=200, end=520 - 30, fill=WHITE, width=7 * SCALE)
    draw.polygon(s(22, 34, 38, 34, 27, 48), fill=WHITE)
    return image


def yozish():
    # A pencil, tip at the bottom left.
    image, draw = canvas()
    draw.polygon(s(58, 24, 72, 38, 40, 70, 26, 56), fill=WHITE)
    draw.polygon(s(26, 56, 40, 70, 22, 74), fill=WHITE)
    draw.line(s(54, 28, 68, 42), fill=BLUE, width=3 * SCALE)
    return image


def tinglash():
    # Headphones.
    image, draw = canvas()
    draw.arc(s(26, 24, 70, 68), start=180, end=360, fill=WHITE, width=7 * SCALE)
    draw.rounded_rectangle(s(22, 46, 36, 72), radius=5 * SCALE, fill=WHITE)
    draw.rounded_rectangle(s(60, 46, 74, 72), radius=5 * SCALE, fill=WHITE)
    return image


def bolimlar():
    # Stacked layers, like the "Bo'limlar" tab.
    image, draw = canvas()
    draw.polygon(s(48, 22, 74, 36, 48, 50, 22, 36), fill=WHITE)
    line(draw, [(24, 50), (48, 62), (72, 50)], 6)
    line(draw, [(24, 62), (48, 74), (72, 62)], 6)
    return image


def main():
    for name, draw in {"takrorlash": takrorlash, "yozish": yozish, "tinglash": tinglash, "bolimlar": bolimlar}.items():
        path = OUT / f"shortcut-{name}.png"
        draw().resize((SIZE, SIZE), Image.LANCZOS).convert("RGB").save(path, optimize=True)
        print(path.relative_to(OUT.parent.parent.parent.parent))


if __name__ == "__main__":
    main()
