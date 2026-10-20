# Draws StoryHeart's icon: an open book with a heart above it, and sound waves coming from the heart.
# Writes src/StoryHeart/Assets/app.ico (every size Windows uses) and icon-256.png.
# Usage: python3 build/make-icon.py   (needs Pillow: pip install pillow)
import math
from pathlib import Path
from PIL import Image, ImageDraw

BLUE = (23, 71, 184, 255)
YELLOW = (255, 209, 0, 255)
WHITE = (255, 255, 255, 255)
LINES = (187, 200, 217, 255)
S = 4  # drawn at 4 times the size, then shrunk, for smooth edges
HEART = (128, 66)  # centre of the heart, on a 256-pixel grid


def p(x, y):
    return (x * S, y * S)


def draw(size_hint):
    img = Image.new("RGBA", (256 * S, 256 * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([p(6, 6), p(250, 250)], radius=44 * S, fill=BLUE)

    # The book: two pages, slightly raised at the spine, with lines of text.
    d.polygon([p(43, 118), p(125, 108), p(125, 203), p(43, 195)], fill=WHITE)
    d.polygon([p(131, 108), p(213, 118), p(213, 195), p(131, 203)], fill=WHITE)
    if size_hint >= 32:
        for i in range(4):
            y = 132 + i * 18
            d.line([p(57, y + 2), p(112, y - 4)], fill=LINES, width=6 * S)
            d.line([p(144, y - 4), p(199, y + 2)], fill=LINES, width=6 * S)

    # The heart.
    cx, cy = HEART
    points = []
    for i in range(200):
        t = 2 * math.pi * i / 200
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        points.append(p(cx + x * 2.6, cy - 4 - y * 2.6))
    d.polygon(points, fill=YELLOW)

    # Sound waves on both sides. Small icons get one thicker wave so it stays visible.
    waves = [(60, 9), (80, 9)] if size_hint >= 32 else [(66, 16)]
    for radius, width in waves:
        box = [p(cx - radius, cy - radius), p(cx + radius, cy + radius)]
        d.arc(box, 180 - 32, 180 + 32, fill=YELLOW, width=width * S)
        d.arc(box, -32, 32, fill=YELLOW, width=width * S)
    return img


def render(size):
    return draw(size).resize((size, size), Image.LANCZOS)


assets = Path(__file__).resolve().parent.parent / "src" / "StoryHeart" / "Assets"
render(256).save(assets / "icon-256.png")
sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
images = [render(s) for s in sizes]
images[-1].save(assets / "app.ico", sizes=[(s, s) for s in sizes], append_images=images[:-1])
print("Wrote", assets / "app.ico")
