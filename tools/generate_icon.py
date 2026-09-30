"""Generate Rounded Screen Corners icon and Microsoft Store asset set.

Run: python tools/generate_icon.py
Outputs:
  RoundedScreenCorners/Assets/AppIcon.ico
  RoundedScreenCorners/Assets/Square44x44Logo*.png  (+ targetsize variants)
  RoundedScreenCorners/Assets/Square71x71Logo*.png
  RoundedScreenCorners/Assets/Square150x150Logo*.png
  RoundedScreenCorners/Assets/Square310x310Logo*.png
  RoundedScreenCorners/Assets/Wide310x150Logo*.png
  RoundedScreenCorners/Assets/StoreLogo*.png
  RoundedScreenCorners/Assets/SplashScreen*.png

Design: transparent canvas. A clean Windows display silhouette is paired with a
bright rounded squircle that previews the app's corner-softening effect.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


SUPERSAMPLE = 1024
ASSETS = Path(__file__).resolve().parent.parent / "RoundedScreenCorners" / "Assets"
ICO_OUT = ASSETS / "AppIcon.ico"
ICO_SIZES = [256, 128, 64, 48, 32, 24, 16]
SCALES = [100, 125, 150, 200, 400]
SQUARE44_TARGETSIZES = [16, 24, 32, 48, 256]

SCREEN_FILL_TOP = (248, 251, 255, 255)
SCREEN_FILL_BOTTOM = (224, 233, 246, 255)
SCREEN_BORDER = (35, 52, 74, 255)
SCREEN_HIGHLIGHT = (255, 255, 255, 120)
STAND_FILL = (45, 59, 80, 255)
STAND_HIGHLIGHT = (92, 111, 137, 255)
SQUIRCLE_TOP = (43, 164, 255, 255)
SQUIRCLE_BOTTOM = (126, 95, 255, 255)
SQUIRCLE_STROKE = (255, 255, 255, 235)
CORNER_ACCENT = (0, 210, 190, 255)


def gradient_rect(size, top, bottom):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    px = img.load()
    h = max(1, size[1] - 1)
    for y in range(size[1]):
        t = y / h
        color = tuple(round(top[i] * (1 - t) + bottom[i] * t) for i in range(4))
        for x in range(size[0]):
            px[x, y] = color
    return img


def draw_masked_gradient(base, box, radius, top, bottom):
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    gradient = gradient_rect((w, h), top, bottom)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), radius=radius, fill=255)
    base.paste(gradient, (x0, y0), mask)
    return mask


def draw_corner_tick(draw, x, y, length, thickness, color, dx, dy):
    half = thickness // 2
    radius = max(1, half)
    if dx > 0:
        h_box = (x - half, y - half, x + length, y + half)
    else:
        h_box = (x - length, y - half, x + half, y + half)
    if dy > 0:
        v_box = (x - half, y - half, x + half, y + length)
    else:
        v_box = (x - half, y - length, x + half, y + half)
    draw.rounded_rectangle(h_box, radius=radius, fill=color)
    draw.rounded_rectangle(v_box, radius=radius, fill=color)


def render_master():
    s = SUPERSAMPLE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Main monitor shadow.
    shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    screen_box = (int(s * 0.12), int(s * 0.18), int(s * 0.88), int(s * 0.68))
    screen_radius = int(s * 0.075)
    sd.rounded_rectangle(
        (
            screen_box[0],
            screen_box[1] + int(s * 0.035),
            screen_box[2],
            screen_box[3] + int(s * 0.045),
        ),
        radius=screen_radius,
        fill=(0, 0, 0, 88),
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=s * 0.028))
    img.alpha_composite(shadow)

    # Screen body with a visible rounded-corner silhouette.
    draw_masked_gradient(img, screen_box, screen_radius, SCREEN_FILL_TOP, SCREEN_FILL_BOTTOM)
    border_w = max(3, int(s * 0.014))
    d.rounded_rectangle(screen_box, radius=screen_radius, outline=SCREEN_BORDER, width=border_w)

    # Soft inner highlight.
    inset = int(s * 0.035)
    d.arc(
        (
            screen_box[0] + inset,
            screen_box[1] + inset,
            screen_box[2] - inset,
            screen_box[3] - inset,
        ),
        start=198,
        end=338,
        fill=SCREEN_HIGHLIGHT,
        width=max(2, int(s * 0.006)),
    )

    # The squircle sits in front of the display, making the rounded-corner theme explicit.
    squircle_box = (int(s * 0.56), int(s * 0.36), int(s * 0.91), int(s * 0.71))
    squircle_radius = int(s * 0.095)
    sq_shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    sqd = ImageDraw.Draw(sq_shadow)
    sqd.rounded_rectangle(
        (
            squircle_box[0],
            squircle_box[1] + int(s * 0.018),
            squircle_box[2],
            squircle_box[3] + int(s * 0.026),
        ),
        radius=squircle_radius,
        fill=(0, 0, 0, 95),
    )
    sq_shadow = sq_shadow.filter(ImageFilter.GaussianBlur(radius=s * 0.018))
    img.alpha_composite(sq_shadow)

    draw_masked_gradient(img, squircle_box, squircle_radius, SQUIRCLE_TOP, SQUIRCLE_BOTTOM)
    d.rounded_rectangle(
        squircle_box,
        radius=squircle_radius,
        outline=SQUIRCLE_STROKE,
        width=max(3, int(s * 0.012)),
    )

    # Corner ticks suggest the overlay can target screen corners.
    tick_len = int(s * 0.105)
    tick_w = int(s * 0.024)
    pad = int(s * 0.045)
    draw_corner_tick(d, screen_box[0] + pad, screen_box[1] + pad, tick_len, tick_w, CORNER_ACCENT, +1, +1)
    draw_corner_tick(d, screen_box[2] - pad, screen_box[1] + pad, tick_len, tick_w, CORNER_ACCENT, -1, +1)
    draw_corner_tick(d, screen_box[0] + pad, screen_box[3] - pad, tick_len, tick_w, CORNER_ACCENT, +1, -1)

    sq_pad = int(s * 0.055)
    draw_corner_tick(
        d,
        squircle_box[2] - sq_pad,
        squircle_box[3] - sq_pad,
        int(s * 0.08),
        tick_w,
        (255, 255, 255, 230),
        -1,
        -1,
    )

    # Stand and base.
    neck = (
        int(s * 0.455),
        int(s * 0.68),
        int(s * 0.545),
        int(s * 0.79),
    )
    base = (
        int(s * 0.34),
        int(s * 0.78),
        int(s * 0.66),
        int(s * 0.84),
    )
    d.rounded_rectangle(neck, radius=int(s * 0.02), fill=STAND_FILL)
    d.rounded_rectangle(
        (neck[0] + int(s * 0.012), neck[1], neck[2] - int(s * 0.012), neck[1] + int(s * 0.018)),
        radius=int(s * 0.01),
        fill=STAND_HIGHLIGHT,
    )
    d.rounded_rectangle(base, radius=int(s * 0.03), fill=STAND_FILL)
    d.rounded_rectangle(
        (base[0] + int(s * 0.04), base[1] + int(s * 0.012), base[2] - int(s * 0.04), base[1] + int(s * 0.024)),
        radius=int(s * 0.01),
        fill=STAND_HIGHLIGHT,
    )

    return img


def render_simple(size):
    s = SUPERSAMPLE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    screen_box = (int(s * 0.13), int(s * 0.20), int(s * 0.87), int(s * 0.67))
    radius = int(s * 0.095)
    d.rounded_rectangle(screen_box, radius=radius, fill=SCREEN_FILL_TOP)
    d.rounded_rectangle(screen_box, radius=radius, outline=SCREEN_BORDER, width=int(s * 0.04))

    squircle_box = (int(s * 0.56), int(s * 0.39), int(s * 0.88), int(s * 0.71))
    d.rounded_rectangle(squircle_box, radius=int(s * 0.10), fill=SQUIRCLE_TOP)
    d.rounded_rectangle(squircle_box, radius=int(s * 0.10), outline=(255, 255, 255, 255), width=int(s * 0.025))

    if size >= 24:
        tick_w = int(s * 0.04)
        draw_corner_tick(d, int(s * 0.22), int(s * 0.29), int(s * 0.12), tick_w, CORNER_ACCENT, +1, +1)
        draw_corner_tick(d, int(s * 0.78), int(s * 0.29), int(s * 0.12), tick_w, CORNER_ACCENT, -1, +1)

    d.rounded_rectangle((int(s * 0.45), int(s * 0.67), int(s * 0.55), int(s * 0.79)), radius=int(s * 0.02), fill=STAND_FILL)
    d.rounded_rectangle((int(s * 0.34), int(s * 0.78), int(s * 0.66), int(s * 0.85)), radius=int(s * 0.035), fill=STAND_FILL)

    return img.resize((size, size), Image.LANCZOS)


def square_at(master, size):
    if size <= 32:
        return render_simple(size)
    return master.resize((size, size), Image.LANCZOS)


def rect_with_centered_icon(master, w, h):
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    icon_size = int(min(w, h) * 0.88)
    icon = square_at(master, icon_size)
    x = (w - icon_size) // 2
    y = (h - icon_size) // 2
    canvas.alpha_composite(icon, (x, y))
    return canvas


def write_scaled_set(name_stem, base_w, base_h, master, square=True):
    for scale in SCALES:
        w = round(base_w * scale / 100)
        h = round(base_h * scale / 100)
        img = square_at(master, w) if square and base_w == base_h else rect_with_centered_icon(master, w, h)
        img.save(ASSETS / f"{name_stem}.scale-{scale}.png", format="PNG", optimize=True)
    base = square_at(master, base_w) if square and base_w == base_h else rect_with_centered_icon(master, base_w, base_h)
    base.save(ASSETS / f"{name_stem}.png", format="PNG", optimize=True)


def write_targetsize_set(name_stem, master):
    for ts in SQUARE44_TARGETSIZES:
        img = square_at(master, ts)
        img.save(ASSETS / f"{name_stem}.targetsize-{ts}.png", format="PNG", optimize=True)
        img.save(ASSETS / f"{name_stem}.targetsize-{ts}_altform-unplated.png", format="PNG", optimize=True)


def write_ico(master):
    frames = [render_simple(s) if s <= 32 else master.resize((s, s), Image.LANCZOS) for s in ICO_SIZES]
    frames[0].save(
        ICO_OUT,
        format="ICO",
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=frames[1:],
    )
    print(f"wrote {ICO_OUT} ({', '.join(f'{s}x{s}' for s in ICO_SIZES)})")


def main():
    ASSETS.mkdir(parents=True, exist_ok=True)
    master = render_master()

    write_ico(master)

    tiles = [
        ("Square44x44Logo", 44, 44, True),
        ("Square71x71Logo", 71, 71, True),
        ("Square150x150Logo", 150, 150, True),
        ("Square310x310Logo", 310, 310, True),
        ("Wide310x150Logo", 310, 150, False),
        ("StoreLogo", 50, 50, True),
        ("SplashScreen", 620, 300, False),
    ]
    for stem, w, h, square in tiles:
        write_scaled_set(stem, w, h, master, square=square)
        print(f"wrote {stem} (base {w}x{h} + scales {SCALES})")

    write_targetsize_set("Square44x44Logo", master)
    print(f"wrote Square44x44Logo targetsize variants {SQUARE44_TARGETSIZES}")


if __name__ == "__main__":
    main()
