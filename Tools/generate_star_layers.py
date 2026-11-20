"""
Generates the two seamless star layers used by the parallax background (own work, no third-party art).

Realism rules (what the night sky actually looks like):
  * Brightness follows a power law: many faint stars, very few bright ones.
  * ~94% of the visible (near-layer) stars look white / blue-white / warm-white (colour is barely visible to the eye).
  * ~6% are visibly coloured and slightly bigger: blue giants (O/B), yellow (G), orange (K), red (M).
  * Colours are approximate black-body sRGB values per stellar class (Mitchell Charity's table).
  * The very brightest coloured stars get a soft glow; no lens-flare blobs.
Tiles seamlessly (stars wrap around the edges).

Usage: python -I generate_star_layers.py <output_dir>
"""
import sys

import numpy as np
from PIL import Image

SIZE = 2048
OUT = sys.argv[1]

STELLAR = {  # approximate black-body colours (sRGB 0..255)
    "O": (155, 176, 255), "B": (170, 191, 255), "A": (202, 215, 255),
    "F": (248, 247, 255), "G": (255, 244, 234), "K": (255, 210, 161), "M": (255, 204, 111),
}
WHITE_CLASSES = (["A", "F", "G", "B"], [0.35, 0.35, 0.2, 0.1])
COLOUR_CLASSES = (["B", "G", "K", "M", "O"], [0.25, 0.22, 0.28, 0.2, 0.05])


def saturate(rgb, amount):
    c = np.array(rgb, dtype=np.float32) / 255.0
    grey = c.mean()
    return np.clip(grey + (c - grey) * amount, 0, 1)


def splat(premult, alpha, x, y, sigma, intensity, colour):
    r = int(np.ceil(sigma * 3.2)) + 1
    xs = np.arange(-r, r + 1)
    fx = x - np.floor(x)
    fy = y - np.floor(y)
    gx = np.exp(-((xs - fx) ** 2) / (2 * sigma * sigma))
    gy = np.exp(-((xs - fy) ** 2) / (2 * sigma * sigma))
    k = np.outer(gy, gx) * intensity
    ix = (int(np.floor(x)) + xs) % SIZE
    iy = (int(np.floor(y)) + xs) % SIZE
    alpha[np.ix_(iy, ix)] += k
    for ch in range(3):
        premult[ch][np.ix_(iy, ix)] += k * colour[ch]


def make_layer(name, count, white_sigma, colour_share, colour_sigma, bright_bias, seed):
    rng = np.random.default_rng(seed)
    alpha = np.zeros((SIZE, SIZE), np.float32)
    premult = [np.zeros((SIZE, SIZE), np.float32) for _ in range(3)]

    for _ in range(count):
        x, y = rng.uniform(0, SIZE, 2)
        # power-law brightness: most stars faint
        b = min(1.0, (rng.pareto(2.2) + 1.0) ** -1.0 * bright_bias + 0.08)
        if rng.random() < colour_share:
            cls = rng.choice(COLOUR_CLASSES[0], p=COLOUR_CLASSES[1])
            colour = saturate(STELLAR[cls], 1.45)
            sigma = rng.uniform(*colour_sigma)
            intensity = min(1.0, b * 1.0 + 0.15)
            splat(premult, alpha, x, y, sigma, intensity, colour)
            if intensity > 0.85:  # soft glow for the brightest coloured stars
                splat(premult, alpha, x, y, sigma * 3.5, 0.12, colour)
        else:
            cls = rng.choice(WHITE_CLASSES[0], p=WHITE_CLASSES[1])
            colour = saturate(STELLAR[cls], 0.35)
            sigma = rng.uniform(*white_sigma)
            splat(premult, alpha, x, y, sigma, min(1.0, b * 1.15 + 0.12), colour)

    a = np.clip(alpha, 0, 1)
    rgb = np.stack([p / np.maximum(alpha, 1e-5) for p in premult], axis=-1)
    rgb = np.clip(rgb, 0, 1)
    img = np.dstack([rgb, a[..., None]])
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(f"{OUT}/{name}.png")
    print(name, "stars:", count)


# Far layer: dense faint dust, entirely white, tiny.
make_layer("StarDust_Far", count=7000, white_sigma=(0.45, 0.7), colour_share=0.0,
           colour_sigma=(0.8, 1.1), bright_bias=0.55, seed=11)
# Near layer: fewer, brighter stars; 6% coloured and slightly bigger.
make_layer("Stars_Near", count=1800, white_sigma=(0.8, 1.15), colour_share=0.06,
           colour_sigma=(1.0, 1.35), bright_bias=1.0, seed=23)
