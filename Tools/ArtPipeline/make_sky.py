"""Hand-drawn style sky backdrop: vertical blue gradient with puffy outlined cartoon clouds.
Horizontally seamless so it can scroll. Size matches the 80x10 backdrop face (8:1)."""
import random
import os
import sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

out = sys.argv[1]
W, H = 4096, 512
SS = 2  # supersample for smooth edges
w, h = W * SS, H * SS
random.seed(7)

top = (88, 150, 222)      # deeper blue at the top
horizon = (178, 216, 245)  # pale near the ground
sky = Image.new("RGB", (w, h))
px = sky.load()
for y in range(h):
    t = y / (h - 1)  # 0 top .. 1 bottom
    c = tuple(int(top[i] + (horizon[i] - top[i]) * t ** 1.3) for i in range(3))
    for x in range(w):
        px[x, y] = c

mask = Image.new("L", (w, h), 0)
shade = Image.new("L", (w, h), 0)
md, sd = ImageDraw.Draw(mask), ImageDraw.Draw(shade)

def puff(draw, cx, cy, r):
    for dx in (-w, 0, w):  # draw wrapped copies so the texture tiles horizontally
        draw.ellipse((cx + dx - r, cy - r, cx + dx + r, cy + r), fill=255)

# The visible band of the backdrop is roughly v 0.45..0.9 from the top (world y 0..4.5 of -1..9).
x = 0
while x < w:
    cx = x + random.randint(0, 300 * SS)
    cy = int(h * random.uniform(0.55, 0.80))
    width = random.randint(110, 240) * SS
    base_r = random.randint(16, 26) * SS
    n = random.randint(4, 7)
    for i in range(n):
        px_ = cx + int(width * (i / (n - 1) - 0.5))
        r = int(base_r * random.uniform(0.75, 1.3) * (1.25 if 0 < i < n - 1 else 0.85))
        py = cy - int(r * random.uniform(0.2, 0.6))
        puff(md, px_, py, r)
    # flat-ish cloud bottom
    for dx in (-w, 0, w):
        md.rounded_rectangle((cx + dx - width // 2, cy - base_r // 2, cx + dx + width // 2, cy + base_r // 2),
                             radius=base_r // 2, fill=255)
    x += width + random.randint(120, 420) * SS

outline = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(5)), mask)
shade = ImageChops.subtract(mask, ImageChops.offset(mask, 0, -7 * SS).crop((0, 0, w, h)))  # band along cloud bottoms
inner_shadow = ImageChops.subtract(mask, mask.filter(ImageFilter.MinFilter(9))).filter(ImageFilter.GaussianBlur(6))

white = Image.new("RGB", (w, h), (255, 255, 255))
shade_col = Image.new("RGB", (w, h), (226, 236, 250))
line_col = Image.new("RGB", (w, h), (120, 150, 190))

sky.paste(white, mask=mask)
sky.paste(shade_col, mask=shade.filter(ImageFilter.GaussianBlur(3 * SS)))
sky.paste(line_col, mask=outline.filter(ImageFilter.GaussianBlur(1)))

sky = sky.resize((W, H), Image.LANCZOS)
os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
sky.save(out)
print("saved", out, sky.size, "top colour", top)
