"""ODLET icon + store art generator.

Everything is drawn at native pixel resolution and upscaled nearest-neighbour, so every output is
true pixel art. Re-run after changing the design:

    python store/tools/make_art.py
    python store/tools/make_art.py --store-only   # store/media only; leaves client/ untouched

Outputs:
  client/Assets/Ronriku/Art/Icon/*.png   app icon master, adaptive layers, legacy + round sizes
  store/media/*.png                      dApp Store / Play icon, banner, feature graphics, screenshots

Requires Python 3.10+ and Pillow.
"""
from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
ICON_DIR = ROOT / "client" / "Assets" / "Ronriku" / "Art" / "Icon"
MEDIA_DIR = ROOT / "store" / "media"
SHOTS_DIR = MEDIA_DIR / "screenshots"
EVIDENCE = ROOT / "docs" / "evidence"
FONT_DIR = ROOT / "client" / "Assets" / "Ronriku" / "Fonts"
SILK = FONT_DIR / "Silkscreen-Regular.ttf"
SILK_BOLD = FONT_DIR / "Silkscreen-Bold.ttf"
PIXELIFY = FONT_DIR / "PixelifySans.ttf"


def hexc(value: str, alpha: int = 255) -> tuple[int, int, int, int]:
    value = value.lstrip("#")
    return int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), alpha


BG = hexc("07080B")
BG_1 = hexc("101218")
LINE = hexc("1E2230")
TEAL = hexc("11C5B3")
TEAL_HI = hexc("6FF2E2")
TEAL_DK = hexc("0B7F75")
TEAL_DEEP = hexc("135B73")
PINK = hexc("FF4FD8")
PINK_HI = hexc("FF9BEA")
PINK_DK = hexc("B02A94")
GOLD = hexc("FFC83D")
GOLD_HI = hexc("FFE79A")
GOLD_DK = hexc("C98A12")
VIOLET = hexc("2A1450")
WHITE = hexc("E7E8E5")
MUTED = hexc("8B93A7")
OUTLINE = hexc("07080B")
CLEAR = (0, 0, 0, 0)

BAYER4 = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(4))


# ---------------------------------------------------------------------------------------------
# primitives (all operate on native-resolution RGBA images)
# ---------------------------------------------------------------------------------------------

def fill(img: Image.Image, color) -> None:
    img.paste(color, (0, 0, img.width, img.height))


def dither_glow(img: Image.Image, cx: float, cy: float, radius: float, color, strength: float = 0.55,
                steps: int = 3, ry: float | None = None) -> None:
    """Radial glow quantised to a few colour steps with 4x4 ordered dithering between them."""
    px = img.load()
    ry = ry or radius
    for y in range(img.height):
        for x in range(img.width):
            d = math.hypot((x + 0.5 - cx) / radius, (y + 0.5 - cy) / ry)
            if d >= 1:
                continue
            level = (1 - d) ** 1.2 * steps
            base = int(level)
            frac = level - base
            # clean pixel-art bands: a 1-cell checkerboard only in the outer part of each band edge
            if frac > 0.8 and (x + y) % 2 == 0:
                base += 1
            if base <= 0:
                continue
            t = min(base, steps) / steps * strength
            px[x, y] = lerp(px[x, y], color, t)


def checker_floor(img: Image.Image, y0: int, color, alpha_top: float, alpha_bottom: float, cell: int = 4) -> None:
    px = img.load()
    for y in range(y0, img.height):
        t = (y - y0) / max(1, img.height - 1 - y0)
        a = alpha_top + (alpha_bottom - alpha_top) * t
        for x in range(img.width):
            if ((x // cell) + (y // cell)) % 2 == 0 and a * 16 > BAYER4[y % 4][x % 4] * 0.9:
                px[x, y] = lerp(px[x, y], color, 0.35)


def outline(img: Image.Image, mask: set[tuple[int, int]], color=OUTLINE) -> None:
    px = img.load()
    for (x, y) in list(mask):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if (nx, ny) not in mask and 0 <= nx < img.width and 0 <= ny < img.height:
                px[nx, ny] = color


# Runes carved into the cube faces (face-local u = along the edge, v = down the face).
RUNE_R = [  # bolt rune (the Rune Hand's Bolt suit)
    "..X",
    ".X.",
    "XXX",
    ".X.",
    "X..",
]
RUNE_SPARK = [  # diamond glyph for the pink face
    ".X.",
    "XXX",
    ".X.",
]


def iso_cube(img: Image.Image, cx: int, top: int, a: int, h: int, rune: bool = True,
             colors=None) -> set[tuple[int, int]]:
    """Pixel-art isometric cube (2:1). a = half width in px, h = side face height. Returns its mask."""
    top_c, left_c, right_c = colors or (GOLD, TEAL, PINK)
    top_hi = GOLD_HI if top_c == GOLD else lerp(top_c, WHITE, 0.4)
    left_dk = lerp(left_c, OUTLINE, 0.35)
    right_dk = lerp(right_c, OUTLINE, 0.35)
    px = img.load()
    mask: set[tuple[int, int]] = set()
    half = a / 2
    cy = top + half
    for y in range(top, top + a + h + 1):
        for x in range(cx - a, cx + a):
            pxc, pyc = x + 0.5, y + 0.5
            dx = pxc - cx
            if abs(dx) / a + abs(pyc - cy) / half <= 1.0:
                # top face: brighter toward the back corner, 1px highlight along the back edges
                edge = abs(dx) / a + (cy - pyc) / half
                c = top_hi if edge > 0.72 else top_c
                px[x, y] = c
                mask.add((x, y))
                continue
            lower = cy + (half - abs(dx) / 2)
            if lower < pyc <= lower + h:
                v = (pyc - lower) / h
                if dx < 0:
                    c = left_c if v < 0.62 else left_dk
                else:
                    c = right_c if v < 0.62 else right_dk
                px[x, y] = c
                mask.add((x, y))
    # vertical front edge highlight
    for y in range(int(cy + half) + 1, int(cy + half + h)):
        if (cx - 1, y) in mask:
            px[cx - 1, y] = lerp(px[cx - 1, y], WHITE, 0.25)
    if rune:
        _face_glyph(img, cx, cy, a, h, RUNE_R, left=True, color=WHITE, glow=TEAL_HI)
        _face_glyph(img, cx, cy, a, h, RUNE_SPARK, left=False, color=GOLD_HI, glow=GOLD)
    outline(img, mask)
    return mask


def _face_glyph(img, cx, cy, a, h, glyph, left: bool, color, glow) -> None:
    """Draws a glyph on a side face; columns follow the 2:1 slope so it sits on the face."""
    px = img.load()
    half = a / 2
    k = max(1, a // 8)
    gw, gh = len(glyph[0]) * k, len(glyph) * k
    u0 = (a - gw) // 2  # centre along the face
    v0 = max(1, (h - gh) // 2)
    for gy in range(gh):
        for gx in range(gw):
            if glyph[gy // k][gx // k] != "X":
                continue
            u = u0 + gx
            x = cx - (a - u) if left else cx + u
            dxc = x + 0.5 - cx
            lower = cy + (half - abs(dxc) / 2)
            y = int(math.floor(lower + v0 + gy + 0.5))
            if 0 <= x < img.width and 0 <= y < img.height:
                px[x, y] = color


# Voxel hero (front view), 8 x 11. Matches the in-game default hero: green hair, blue tunic, red cape.
HERO = [
    ".oooooo.",
    "oGGGGGGo",
    "oGgGGgGo",
    "oSESSESo",
    "oSSssSSo",
    "RbBBBBbo",
    "RBBYYBBo",
    "RbBBBBbo",
    ".oLooLo.",
    ".oLooLo.",
    ".oWooWo.",
]
HERO_COLORS = {
    "o": OUTLINE, "G": hexc("8BE35A"), "g": hexc("4FAE3E"), "S": hexc("D9A06A"), "s": hexc("A8703F"),
    "E": hexc("17181C"), "B": hexc("3A6BFF"), "b": hexc("2848B8"), "Y": GOLD, "R": hexc("FF3B5C"),
    "L": hexc("2A2C36"), "W": WHITE,
}


def sprite(img: Image.Image, x0: int, y0: int, rows: list[str], colors: dict, k: int = 1) -> None:
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch not in colors:
                continue
            for yy in range(k):
                for xx in range(k):
                    X, Y = x0 + x * k + xx, y0 + y * k + yy
                    if 0 <= X < img.width and 0 <= Y < img.height:
                        px[X, Y] = colors[ch]


def sparkle(img: Image.Image, x: int, y: int, color, size: int = 1) -> None:
    px = img.load()
    pts = [(0, 0)] + [(d, 0) for d in range(-size, size + 1) if d] + [(0, d) for d in range(-size, size + 1) if d]
    for dx, dy in pts:
        if 0 <= x + dx < img.width and 0 <= y + dy < img.height:
            px[x + dx, y + dy] = color if (dx, dy) == (0, 0) or size == 1 else lerp(color, BG, 0.3)


def up(img: Image.Image, scale: int) -> Image.Image:
    return img.resize((img.width * scale, img.height * scale), Image.NEAREST)


# ---------------------------------------------------------------------------------------------
# icon
# ---------------------------------------------------------------------------------------------

def icon_background(n: int) -> Image.Image:
    img = Image.new("RGBA", (n, n), BG)
    s = n / 32
    dither_glow(img, n * 0.5, n * 0.62, n * 0.62, TEAL_DEEP, strength=0.75, steps=3)
    dither_glow(img, n * 0.5, n * 0.58, n * 0.40, VIOLET, strength=0.9, steps=2)
    dither_glow(img, n * 0.5, n * 0.60, n * 0.26, hexc("4A1A6A"), strength=0.6, steps=2)
    # faint pixel "stars"
    px = img.load()
    for (x, y, c) in ((4, 5, TEAL), (27, 7, PINK), (6, 25, GOLD), (26, 24, TEAL), (24, 3, GOLD)):
        xx, yy = int(x * s), int(y * s)
        if 0 <= xx < n and 0 <= yy < n:
            px[xx, yy] = lerp(c, BG, 0.35)
    return img


def icon_motif(n: int, big: bool = True) -> Image.Image:
    """Transparent layer with the cube (+ hero when there is room). n = native grid size."""
    img = Image.new("RGBA", (n, n), CLEAR)
    if big:
        a = h = round(n * 0.28)
        cx = n // 2
        total = 10 + a / 2 + h + 1           # hero head to cube bottom (incl. outline)
        hero_top = int(round((n - total) / 2))
        top = int(round(hero_top + 10 - a / 2))
        iso_cube(img, cx, top, a, h)
        hero_x = cx - 4
        hero_y = hero_top  # feet on the middle of the top face
        sprite(img, hero_x, hero_y, HERO, HERO_COLORS)
        sparkle(img, cx + a - 1, hero_y + 1, GOLD, 1)
        sparkle(img, cx - a + 1, hero_y + 4, PINK, 1)
        img.load()[cx + a - 4, hero_y - 2] = TEAL_HI
    else:
        a, h = round(n * 0.34), round(n * 0.30)
        cx = n // 2
        top = (n - (a + h)) // 2 - 1
        iso_cube(img, cx, top, a, h)
        sparkle(img, cx + a - 1, top + 1, GOLD, 1)
    return img


def composite(bg: Image.Image, fg: Image.Image, dx: int = 0, dy: int = 0) -> Image.Image:
    out = bg.copy()
    out.alpha_composite(fg, (dx, dy))
    return out


def round_mask(img: Image.Image) -> Image.Image:
    n = img.width
    out = img.copy()
    px = out.load()
    r = n / 2
    for y in range(n):
        for x in range(n):
            if math.hypot(x + 0.5 - r, y + 0.5 - r) > r:
                px[x, y] = CLEAR
    return out


def build_icons(save: bool = True) -> dict[str, Image.Image]:
    out: dict[str, Image.Image] = {}

    # master 512 = 32 grid x16 (full-bleed square; stores apply their own mask)
    g32 = composite(icon_background(32), icon_motif(32))
    out["ronriku-icon-512.png"] = up(g32, 16)
    out["ronriku-icon-1024.png"] = up(g32, 32)

    # legacy sizes, each from an integer grid; small sizes use the simplified cube-only motif
    g24 = composite(icon_background(24), icon_motif(24, big=False))
    g36 = composite(icon_background(36), icon_motif(36))
    legacy = {192: up(g32, 6), 144: up(g36, 4), 96: up(g32, 3), 72: up(g24, 3), 48: up(g24, 2), 36: up(g36, 1)}
    for size, im in legacy.items():
        out[f"ronriku-icon-legacy-{size}.png"] = im
        out[f"ronriku-icon-round-{size}.png"] = round_mask(im)

    # adaptive: 108dp canvas, visible safe zone = centre 66dp. Grid 36 -> motif drawn in 24 cells.
    bg36 = icon_background(36)
    fg36 = Image.new("RGBA", (36, 36), CLEAR)
    fg36.alpha_composite(icon_motif(24), (6, 6))
    for size, scale in ((432, 12), (324, 9), (216, 6), (108, 3)):
        out[f"ronriku-adaptive-bg-{size}.png"] = up(bg36, scale)
        out[f"ronriku-adaptive-fg-{size}.png"] = up(fg36, scale)
    # 162 / 81 are not integer multiples of the 36 grid: downsample the 324 layers (2x box)
    for size, src in ((162, 324), (81, 162)):
        for layer in ("bg", "fg"):
            out[f"ronriku-adaptive-{layer}-{size}.png"] = out[f"ronriku-adaptive-{layer}-{src}.png"].resize((size, size), Image.BOX)

    if save:
        ICON_DIR.mkdir(parents=True, exist_ok=True)
        for name, im in out.items():
            im.save(ICON_DIR / name, optimize=True)
    return out


# ---------------------------------------------------------------------------------------------
# text
# ---------------------------------------------------------------------------------------------

def pixel_text(img: Image.Image, xy, text: str, font_path: Path, size: int, color, shadow=None,
               anchor: str = "la", spacing: int = 0) -> tuple[int, int, int, int]:
    """Aliased (1-bit) text at native resolution so it upscales to crisp pixels."""
    draw = ImageDraw.Draw(img)
    draw.fontmode = "1"
    font = ImageFont.truetype(str(font_path), size)
    x, y = xy
    if spacing:
        # manual letter spacing
        widths = [draw.textlength(ch, font=font) for ch in text]
        total = sum(widths) + spacing * (len(text) - 1)
        if anchor[0] == "m":
            x -= total / 2
        cur = x
        for ch, w in zip(text, widths):
            if shadow:
                draw.text((cur + 1, y + 1), ch, font=font, fill=shadow, anchor="l" + anchor[1])
            draw.text((cur, y), ch, font=font, fill=color, anchor="l" + anchor[1])
            cur += w + spacing
        return int(x), int(y), int(cur), int(y + size)
    if shadow:
        draw.text((x + 1, y + 1), text, font=font, fill=shadow, anchor=anchor)
    draw.text((x, y), text, font=font, fill=color, anchor=anchor)
    return draw.textbbox((x, y), text, font=font, anchor=anchor)


WORDMARK = "ODLET"

# Hand-drawn bitmap wordmark (2-cell strokes). A font "O" next to "D" reads as "QD"/"DD" at pixel
# sizes, so the letters are drawn here: round O, square-backed D.
WORDMARK_GLYPHS = {
    "O": ["..XXX..", ".XXXXX.", "XX...XX", "XX...XX", "XX...XX", "XX...XX", "XX...XX", ".XXXXX.", "..XXX.."],
    "D": ["XXXXX..", "XXXXXX.", "XX..XXX", "XX...XX", "XX...XX", "XX...XX", "XX..XXX", "XXXXXX.", "XXXXX.."],
    "L": ["XX....", "XX....", "XX....", "XX....", "XX....", "XX....", "XX....", "XXXXXX", "XXXXXX"],
    "E": ["XXXXXX", "XXXXXX", "XX....", "XX....", "XXXXX.", "XX....", "XX....", "XXXXXX", "XXXXXX"],
    "T": ["XXXXXX", "XXXXXX", "..XX..", "..XX..", "..XX..", "..XX..", "..XX..", "..XX..", "..XX.."],
}


def _wordmark_layer(img: Image.Image, x0: int, y0: int, k: int, color) -> None:
    px = img.load()
    x = x0
    for ch in WORDMARK:
        rows = WORDMARK_GLYPHS[ch]
        for gy, row in enumerate(rows):
            for gx, c in enumerate(row):
                if c != "X":
                    continue
                for yy in range(k):
                    for xx in range(k):
                        X, Y = x + gx * k + xx, y0 + gy * k + yy
                        if 0 <= X < img.width and 0 <= Y < img.height:
                            px[X, Y] = color
        x += (len(rows[0]) + 2) * k


def wordmark(img: Image.Image, cx: int, y: int, size: int) -> int:
    """ODLET bitmap wordmark (cap height ~ size * 1.1) with a teal/pink offset glow; centred on cx, top at y. Returns the bottom y (incl. glow)."""
    k = max(1, round(size / 8))
    width = sum((len(WORDMARK_GLYPHS[ch][0]) + 2) * k for ch in WORDMARK) - 2 * k
    x0 = cx - width // 2
    _wordmark_layer(img, x0 + 2, y + 2, k, PINK_DK)
    _wordmark_layer(img, x0 + 1, y + 1, k, PINK)
    _wordmark_layer(img, x0, y, k, TEAL_HI)
    return y + len(WORDMARK_GLYPHS["O"]) * k + 2


# ---------------------------------------------------------------------------------------------
# store graphics
# ---------------------------------------------------------------------------------------------

def scene_backdrop(w: int, h: int) -> Image.Image:
    img = Image.new("RGBA", (w, h), BG)
    r = max(w, h)
    dither_glow(img, w * 0.2, h * 0.3, r * 0.55, TEAL_DEEP, strength=0.6, steps=4)
    dither_glow(img, w * 0.8, h * 0.75, r * 0.6, VIOLET, strength=0.9, steps=4)
    checker_floor(img, int(h * 0.78), PINK_DK, 0.2, 0.9)
    return img


def hero_stage(img: Image.Image, cx: int, floor_y: int, a: int) -> None:
    """Cube + hero on a stage; floor_y = bottom of the cube."""
    h = a
    top = floor_y - (a + h)
    iso_cube(img, cx, top, a, h)
    k = max(1, a // 9)
    hero_x = cx - 4 * k
    hero_y = int(top + a / 2) - 10 * k
    sprite(img, hero_x, hero_y, HERO, HERO_COLORS, k)


def small_cube(img, cx, top, a, colors):
    iso_cube(img, cx, top, a, a, rune=False, colors=colors)


def feature_graphic(w_native: int, h_native: int, scale: int, tagline: str, layout: str) -> Image.Image:
    img = scene_backdrop(w_native, h_native)
    if layout == "wide":
        # wordmark left, stage right
        bottom = wordmark(img, int(w_native * 0.36), int(h_native * 0.26), 16 if h_native < 140 else 24)
        pixel_text(img, (int(w_native * 0.36), bottom + 6), tagline, SILK, 8, GOLD, shadow=OUTLINE, anchor="mt")
        pixel_text(img, (int(w_native * 0.36), bottom + 18), "BATTLES  RUNES  DASH  BEATS", SILK, 8, MUTED, anchor="mt")
        cx = int(w_native * 0.78)
        floor_y = int(h_native * 0.80)
        a = 18 if h_native < 140 else 20
        hero_stage(img, cx, floor_y, a)
        small_cube(img, cx - 34, floor_y - 12, 5, (TEAL, TEAL_DK, TEAL_DEEP))
        small_cube(img, cx + 32, floor_y - 18, 6, (PINK, PINK_DK, VIOLET))
        small_cube(img, cx + 22, floor_y - 58, 3, (GOLD, GOLD_DK, hexc("7A4E08")))
        for (sx, sy, c) in ((cx - 26, floor_y - 52, GOLD), (cx + 30, floor_y - 40, TEAL_HI), (cx - 30, floor_y - 34, PINK)):
            sparkle(img, sx, sy, c)
    else:  # square
        cx = w_native // 2
        bottom = wordmark(img, cx, int(h_native * 0.10), 24)
        pixel_text(img, (cx, bottom + 8), tagline, SILK, 8, GOLD, shadow=OUTLINE, anchor="mt")
        floor_y = int(h_native * 0.82)
        hero_stage(img, cx, floor_y, 36)
        small_cube(img, cx - 90, floor_y - 30, 10, (TEAL, TEAL_DK, TEAL_DEEP))
        small_cube(img, cx + 88, floor_y - 44, 12, (PINK, PINK_DK, VIOLET))
        small_cube(img, cx + 70, floor_y - 150, 6, (GOLD, GOLD_DK, hexc("7A4E08")))
        small_cube(img, cx - 76, floor_y - 130, 5, (GOLD, GOLD_DK, hexc("7A4E08")))
        for (sx, sy, c) in ((cx - 60, floor_y - 170, GOLD), (cx + 50, floor_y - 110, TEAL_HI), (cx - 100, floor_y - 80, PINK),
                            (cx + 110, floor_y - 120, GOLD)):
            sparkle(img, sx, sy, c)
        pixel_text(img, (cx, h_native - 18), "SOLANA SEEKER  ·  ANDROID", SILK, 8, MUTED, anchor="mt")
    return up(img, scale)


def build_store_graphics(icons: dict[str, Image.Image]) -> None:
    MEDIA_DIR.mkdir(parents=True, exist_ok=True)
    icons["ronriku-icon-512.png"].convert("RGB").save(MEDIA_DIR / "icon-512.png", optimize=True)
    # Google Play feature graphic 1024x500 (native 256x125, x4)
    feature_graphic(256, 125, 4, "PIXEL PUZZLE ARCADE", "wide").convert("RGB").save(MEDIA_DIR / "feature-graphic-1024x500.png", optimize=True)
    # dApp Store banner 1200x600 (native 300x150, x4)
    feature_graphic(300, 150, 4, "PIXEL PUZZLE ARCADE", "wide").convert("RGB").save(MEDIA_DIR / "banner-1200x600.png", optimize=True)
    # dApp Store feature graphic 1200x1200 (native 300x300, x4)
    feature_graphic(300, 300, 4, "PIXEL PUZZLE ARCADE", "square").convert("RGB").save(MEDIA_DIR / "feature-1200x1200.png", optimize=True)


# ---------------------------------------------------------------------------------------------
# screenshots
# ---------------------------------------------------------------------------------------------

SHOTS = [
    ("arcade-2-battle-hit.png", "FIGHT WITH YOUR MIND", "Fast puzzle attacks. Chain combos.", PINK),
    ("arcade-5-rune-scoring.png", "PLAY A RUNE HAND", "Combos, charms, chips x mult.", GOLD),
    ("arcade-6-ice-dash.png", "DASH ACROSS THE ICE", "Slide, grab gems, find the door.", TEAL),
    ("seeker-20x9-2-trial1-start.png", "THREE TRIALS A DAY", "Pattern, Shadow, Link. One streak.", PINK),
    ("seeker-20x9-0-shop.png", "COLLECT VOXEL HEROES", "Earn shards. Equip your figure.", GOLD),
]


def compose_shot(src: Path, title: str, sub: str, accent, width: int, height: int) -> Image.Image:
    # background at native /4 resolution with dithered glow, then upscaled
    s = 3
    nw, nh = width // s, height // s
    bg = Image.new("RGBA", (nw, nh), BG)
    dither_glow(bg, nw * 0.5, nh * 0.1, nw * 0.9, lerp(accent, BG, 0.55), strength=0.6, steps=3, ry=nh * 0.35)
    dither_glow(bg, nw * 0.5, nh * 0.9, nw * 0.9, VIOLET, strength=0.8, steps=3, ry=nh * 0.4)
    # caption: native text, 4x
    bar_h = 96 if height >= 2400 else 84
    pixel_text(bg, (nw // 2, 26), title, SILK_BOLD, 16, accent, shadow=OUTLINE, anchor="mt")
    pixel_text(bg, (nw // 2, 26 + 26), sub, PIXELIFY, 12, WHITE, anchor="mt")
    canvas = up(bg, s)

    shot = Image.open(src).convert("RGBA")
    top = bar_h * s
    avail_h = height - top - 40
    scale = avail_h / shot.height
    sw, sh = int(shot.width * scale), int(shot.height * scale)
    shot = shot.resize((sw, sh), Image.LANCZOS)
    x = (width - sw) // 2
    y = top
    d = ImageDraw.Draw(canvas)
    # pixel frame: 8px accent border with notched corners + hard shadow
    b = 8
    d.rectangle((x - b + 12, y - b + 12, x + sw + b + 12, y + sh + b + 12), fill=OUTLINE)
    d.rectangle((x - b, y - b, x + sw + b - 1, y + sh + b - 1), fill=accent)
    for cx_, cy_ in ((x - b, y - b), (x + sw, y - b), (x - b, y + sh), (x + sw, y + sh)):
        d.rectangle((cx_, cy_, cx_ + b - 1, cy_ + b - 1), fill=canvas.getpixel((cx_ - 1 if cx_ > 0 else 0, cy_)))
    canvas.alpha_composite(shot, (x, y))
    return canvas.convert("RGB")


def build_screenshots() -> None:
    for sub in ("1080x2400", "1080x1920"):
        (SHOTS_DIR / sub).mkdir(parents=True, exist_ok=True)
    for i, (src, title, sub, accent) in enumerate(SHOTS, 1):
        path = EVIDENCE / src
        if not path.exists():
            print(f"skip {src} (missing)")
            continue
        compose_shot(path, title, sub, accent, 1080, 2400).save(SHOTS_DIR / "1080x2400" / f"{i:02d}.png", optimize=True)
        compose_shot(path, title, sub, accent, 1080, 1920).save(SHOTS_DIR / "1080x1920" / f"{i:02d}.png", optimize=True)


if __name__ == "__main__":
    import sys
    store_only = "--store-only" in sys.argv
    icons = build_icons(save=not store_only)
    build_store_graphics(icons)
    build_screenshots()
    print("icons ->", "(not written: --store-only)" if store_only else ICON_DIR)
    print("store media ->", MEDIA_DIR)
