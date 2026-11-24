"""Split a horizontal sprite sheet that already has transparency into clean, uniform frames:
drops stray red/yellow specks and fringe, keeps only the main figure per frame, aligns frames
on the body centre with a shared foot baseline."""
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

def is_fringe(p):
    r, g, b, a = p
    red = r > 180 and g < 90 and b < 90          # pure red glow
    yellow = r > 180 and g > 170 and b < 110      # yellow glow
    return red or yellow

def keep(p):
    return p[3] > 128 and not is_fringe(p)

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
top, bottom = ink_rows[0] - 6, ink_rows[-1] + 6
body_y0 = top + int((bottom - top) * 0.35)
body_y1 = top + int((bottom - top) * 0.55)
print("frames:", len(runs), "y range:", top, bottom)

frames = []
for (x0, x1) in runs:
    x0, x1 = max(0, x0 - 12), min(W - 1, x1 + 12)
    crop = im.crop((x0, top, x1 + 1, bottom + 1))
    cw, ch = crop.size
    cp = crop.load()
    mask = [[keep(cp[x, y]) for x in range(cw)] for y in range(ch)]

    # Largest 8-connected component = the figure; everything else is specks.
    seen = [[False] * cw for _ in range(ch)]
    best = []
    for sy in range(ch):
        for sx in range(cw):
            if not mask[sy][sx] or seen[sy][sx]:
                continue
            comp, q = [], deque([(sx, sy)])
            seen[sy][sx] = True
            while q:
                x, y = q.popleft()
                comp.append((x, y))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < cw and 0 <= ny < ch and mask[ny][nx] and not seen[ny][nx]:
                            seen[ny][nx] = True
                            q.append((nx, ny))
            if len(comp) > len(best):
                best = comp

    out = Image.new("RGBA", (cw, ch), (151, 73, 18, 0))
    op = out.load()
    xs = []
    for x, y in best:
        r, g, b, a = cp[x, y]
        op[x, y] = (r, g, b, 255)
        if body_y0 - top <= y <= body_y1 - top:
            xs.append(x)
    anchor = (min(xs) + max(xs)) / 2  # centre of the torso band
    frames.append((out, anchor, len(best)))

half = max(max(a, f.width - a) for f, a, _ in frames)
cell_w = int(half * 2) + 2
cell_h = bottom - top + 1
scale = target_h / cell_h
out_w = max(1, round(cell_w * scale))

for i, (f, a, n) in enumerate(frames):
    cell = Image.new("RGBA", (cell_w, cell_h), (151, 73, 18, 0))
    cell.alpha_composite(f, (int(round(cell_w / 2 - a)), 0))
    cell.resize((out_w, target_h), Image.LANCZOS).save(f"{out_dir}/{prefix}_{i}.png")
    print(f"frame {i}: kept {n} px")

sheet = Image.new("RGBA", (out_w * len(frames), target_h), (90, 140, 200, 255))
for i in range(len(frames)):
    sheet.alpha_composite(Image.open(f"{out_dir}/{prefix}_{i}.png"), (i * out_w, 0))
sheet.save(f"{out_dir}/{prefix}_contact.png")
print(f"saved {len(frames)} frames of {out_w}x{target_h}")
