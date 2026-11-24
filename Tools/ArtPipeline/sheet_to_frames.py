"""Split a horizontal walk-cycle sprite sheet (line art on a white/glowy background) into
clean, transparent, uniformly sized frames aligned on the body so the cycle doesn't jitter."""
import os
import sys
from collections import deque
from PIL import Image

src, out_dir, prefix = sys.argv[1], sys.argv[2], sys.argv[3]
target_h = int(sys.argv[4]) if len(sys.argv) > 4 else 255
os.makedirs(out_dir, exist_ok=True)

im = Image.open(src).convert("RGBA")
W, H = im.size
px = im.load()

def is_ink(p):
    r, g, b, a = p
    return a > 100 and r - b > 55 and (r + g + b) / 3 < 205

# 1. Find frames as runs of columns containing ink.
cols = [any(is_ink(px[x, y]) for y in range(0, H, 2)) for x in range(W)]
runs, s = [], None
for x, c in enumerate(cols + [False]):
    if c and s is None:
        s = x
    elif not c and s is not None:
        if x - s > 20:
            runs.append((s, x - 1))
        s = None
ink_rows = [y for y in range(H) if any(is_ink(px[x, y]) for x in range(0, W, 2))]
top, bottom = ink_rows[0] - 4, ink_rows[-1] + 4  # shared vertical range keeps foot motion intact
body_y0 = top + int((bottom - top) * 0.3)         # neck/body band used for horizontal alignment
body_y1 = top + int((bottom - top) * 0.6)
print("frames:", len(runs), "y range:", top, bottom)

frames = []
for (x0, x1) in runs:
    x0, x1 = max(0, x0 - 4), min(W - 1, x1 + 4)
    crop = im.crop((x0, top, x1 + 1, bottom + 1))
    cw, ch = crop.size
    cp = crop.load()
    barrier = [[is_ink(cp[x, y]) for x in range(cw)] for y in range(ch)]

    # 2. Flood-fill background from the crop border; everything reached becomes transparent.
    bg = [[False] * cw for _ in range(ch)]
    q = deque([(x, 0) for x in range(cw)] + [(x, ch - 1) for x in range(cw)] +
              [(0, y) for y in range(ch)] + [(cw - 1, y) for y in range(ch)])
    while q:
        x, y = q.popleft()
        if not (0 <= x < cw and 0 <= y < ch) or bg[y][x] or barrier[y][x]:
            continue
        bg[y][x] = True
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    out = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    op = out.load()
    ink_xs = []
    for y in range(ch):
        for x in range(cw):
            r, g, b, a = cp[x, y]
            if barrier[y][x]:
                op[x, y] = (r, g, b, 255)
                if body_y0 - top <= y <= body_y1 - top:
                    ink_xs.append(x)
            elif not bg[y][x] and r - b >= 15:  # enclosed cream fill (skip enclosed white gaps)
                op[x, y] = (r, g, b, 255)
    anchor = sum(ink_xs) / len(ink_xs)  # body centre
    frames.append((out, anchor))

# 3. Uniform cells, symmetric around each frame's body centre.
half = max(max(a, f.width - a) for f, a in frames)
cell_w = int(half * 2) + 2
cell_h = bottom - top + 1
scale = target_h / cell_h
out_w = max(1, round(cell_w * scale))
ink_rgb = (151, 73, 18)

for i, (f, a) in enumerate(frames):
    cell = Image.new("RGBA", (cell_w, cell_h), ink_rgb + (0,))  # transparent ink-coloured bg avoids fringes
    cell.alpha_composite(f, (int(round(cell_w / 2 - a)), 0))
    cell = cell.resize((out_w, target_h), Image.LANCZOS)
    cell.save(f"{out_dir}/{prefix}_{i}.png")

# Contact sheet for review.
sheet = Image.new("RGBA", (out_w * len(frames), target_h), (90, 140, 200, 255))
for i in range(len(frames)):
    fr = Image.open(f"{out_dir}/{prefix}_{i}.png")
    sheet.alpha_composite(fr, (i * out_w, 0))
sheet.save(f"{out_dir}/{prefix}_contact.png")
print(f"saved {len(frames)} frames of {out_w}x{target_h}")
