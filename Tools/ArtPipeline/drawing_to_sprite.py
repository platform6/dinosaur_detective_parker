"""Convert a photographed marker drawing into a transparent, cropped, right-facing sprite PNG."""
import os
import sys
from collections import deque
from PIL import Image, ImageFilter, ImageOps

src, dst = sys.argv[1], sys.argv[2]
target_h = int(sys.argv[3]) if len(sys.argv) > 3 else 256
fill_rgb = (255, 236, 214)  # cream body fill

img = Image.open(src).convert("RGB")
img = ImageOps.exif_transpose(img)

# Optional 5th arg: "crop:x0,y0,x1,y1" keeps only that region, "erase:x0,y0,x1,y1" blanks it to paper.
for op in sys.argv[5:]:  # applied in order, coordinates relative to the image at that point
    mode, coords = op.split(":")
    box = tuple(int(v) for v in coords.split(","))
    if mode == "crop":
        img = img.crop(box)
    elif mode == "rotate":  # degrees counter-clockwise; corners filled with the paper colour
        border = sorted(img.getpixel((x, y)) for x in range(0, img.width, 7) for y in (2, img.height - 3))
        img = img.rotate(box[0], resample=Image.BICUBIC, expand=True, fillcolor=border[len(border) // 2])
    elif mode == "erase":
        paper = img.getpixel((5, img.height // 2))
        img.paste(paper, box)

# Ink strength: orange marker has R much higher than B; paper is near-neutral grey.
def ink_mask(im):
    r, g, b = im.split()
    diff = Image.eval(Image.merge("RGB", (r, g, b)).convert("RGB").split()[0], lambda v: v)
    # compute (R - B) per pixel
    px = im.load()
    w, h = im.size
    m = Image.new("L", (w, h))
    mp = m.load()
    for y in range(h):
        for x in range(w):
            R, G, B = px[x, y]
            s = R - B
            mp[x, y] = 0 if s < 40 else min(255, int((s - 40) * 255 / 80))
    return m

full_mask = ink_mask(img)
bbox = full_mask.point(lambda v: 255 if v > 40 else 0).filter(ImageFilter.MedianFilter(5)).getbbox()
pad = 12
bbox = (max(0, bbox[0] - pad), max(0, bbox[1] - pad),
        min(img.width, bbox[2] + pad), min(img.height, bbox[3] + pad))
img = img.crop(bbox)

# Face right (pass "nomirror" as 4th arg when the drawing already faces right).
if not (len(sys.argv) > 4 and sys.argv[4] == "nomirror"):
    img = ImageOps.mirror(img)

scale = target_h / img.height
img = img.resize((max(1, round(img.width * scale)), target_h), Image.LANCZOS)
w, h = img.size

# Adaptive ink strength: normalise R-B against this photo's paper tint and ink density.
spx = img.load()
svals = [spx[x, y][0] - spx[x, y][2] for y in range(h) for x in range(w)]
ordered = sorted(svals)
lo = ordered[len(ordered) // 2] + 15                     # paper level (median) + margin
inky = sorted(v for v in svals if v > lo + 30)
hi = inky[int(len(inky) * 0.35)] if inky else lo + 80     # solid-ink level
ink = Image.new("L", (w, h))
ikp = ink.load()
strong = []
for y in range(h):
    for x in range(w):
        v = svals[y * w + x]
        ikp[x, y] = int(255 * min(1.0, max(0.0, (v - lo) / max(1, hi - lo))) ** 0.6)  # gamma lifts faint strokes
        if v >= hi:
            strong.append(spx[x, y])
ink_rgb = (151, 73, 18)  # matches Player_Dino line colour
print(f"paper={lo} ink={hi} ink_rgb={ink_rgb}")
# Draw lines in one solid ink colour instead of the photo's paper-tinted pixels.
img = Image.new("RGB", (w, h), ink_rgb)

# Barrier for flood fill: ink, slightly thickened to close tiny gaps in the outline.
barrier = ink.point(lambda v: 255 if v > 60 else 0).filter(ImageFilter.MaxFilter(3))
bp = barrier.load()

outside = [[False] * w for _ in range(h)]
q = deque()
for x in range(w):
    for y in (0, h - 1):
        if not bp[x, y]:
            q.append((x, y))
for y in range(h):
    for x in (0, w - 1):
        if not bp[x, y]:
            q.append((x, y))
while q:
    x, y = q.popleft()
    if outside[y][x] or bp[x, y]:
        continue
    outside[y][x] = True
    for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
        if 0 <= nx < w and 0 <= ny < h and not outside[ny][nx] and not bp[nx, ny]:
            q.append((nx, ny))

out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
op, ip, sp = out.load(), ink.load(), img.load()
inside_count = 0
for y in range(h):
    for x in range(w):
        a = ip[x, y]
        if outside[y][x]:
            if a > 0:  # anti-aliased ink edge against transparency
                R, G, B = sp[x, y]
                op[x, y] = (R, G, B, a)
        else:
            inside_count += 1
            # blend ink over cream fill
            R, G, B = sp[x, y]
            t = a / 255
            op[x, y] = (round(R * t + fill_rgb[0] * (1 - t)),
                        round(G * t + fill_rgb[1] * (1 - t)),
                        round(B * t + fill_rgb[2] * (1 - t)), 255)

# Trim any transparent margin so the pivot sits at the feet.
out = out.crop(out.getbbox())
os.makedirs(os.path.dirname(os.path.abspath(dst)), exist_ok=True)
out.save(dst)
print(f"saved {dst} size={out.size} interior_pixels={inside_count} ({inside_count / (w * h):.0%})")
