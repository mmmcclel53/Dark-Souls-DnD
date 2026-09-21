"""Builds Resources/Images/Backgrounds/BonfireFloor.jpg: a ring-paved slate floor lit by the
fire, with the bowl from the reference render cut out and set in the centre-right.

Run from the project root:
    python Tools/BonfireFloor/generate.py Tools/BonfireFloor/bowl_reference.jpg Resources/Images/Backgrounds/BonfireFloor.jpg
"""
import sys, math
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

W, H = 1920, 1080
CX, CY = int(W * 0.545), int(H * 0.56)
# How far the camera stands back: 1.0 is the original framing, smaller pulls out.
ZOOM = 0.55
rng = np.random.default_rng(7)

def value_noise(w, h, cell, seed):
    r = np.random.default_rng(seed)
    gw, gh = w // cell + 2, h // cell + 2
    g = r.random((gh, gw)).astype(np.float32)
    big = np.array(Image.fromarray((g * 255).astype(np.uint8)).resize((gw * cell, gh * cell), Image.BICUBIC)).astype(np.float32) / 255.0
    return big[:h, :w]

def fbm(w, h, base, octaves, seed):
    out = np.zeros((h, w), np.float32); amp = 1.0; tot = 0.0; cell = base
    for i in range(octaves):
        out += value_noise(w, h, max(2, cell), seed + i) * amp
        tot += amp; amp *= 0.5; cell //= 2
    return out / tot

def hash2(a, b):
    return ((a * 73856093) ^ (b * 19349663)) & 0xFFFF

yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
dx, dy = xx - CX, yy - CY
r = np.sqrt(dx * dx + dy * dy)
theta = np.arctan2(dy, dx)

# Domain warp so slab edges are hewn rather than drafted.
warp = fbm(W, H, 96, 3, 11) - 0.5
r_w = r + warp * 22.0 * ZOOM
t_w = theta + (fbm(W, H, 128, 2, 23) - 0.5) * 0.06

RING = 92.0 * ZOOM
ring = np.floor((r_w + 30) / RING).astype(np.int32)
ring_r = ring * RING
n_slabs = np.maximum(10, np.round(2 * np.pi * np.maximum(ring_r, 60 * ZOOM) / (150.0 * ZOOM))).astype(np.int32)
ring_off = (ring * 0.37) % (2 * np.pi)
seg = (t_w + np.pi + ring_off) % (2 * np.pi)
slab = np.floor(seg / (2 * np.pi / n_slabs)).astype(np.int32)

# Mortar: distance to the nearest ring edge or slab edge.
d_ring = np.abs(((r_w + 30) % RING) - RING / 2)
d_ring = RING / 2 - d_ring
seg_w = 2 * np.pi / n_slabs
d_seg = np.abs((seg % seg_w) - seg_w / 2)
d_seg = (seg_w / 2 - d_seg) * np.maximum(ring_r, 60 * ZOOM)
d_edge = np.minimum(d_ring, d_seg)
mortar = np.clip(1.0 - d_edge / (4.5 * ZOOM + 1.0), 0, 1)
bevel = np.clip(1.0 - d_edge / (16.0 * ZOOM), 0, 1) * 0.35

h = hash2(ring, slab)
slab_tone = 0.62 + (h % 1000) / 1000.0 * 0.70
slab_warm = ((h // 1000) % 100) / 100.0

grain = fbm(W, H, int(24 * ZOOM), 4, 41)
fine = value_noise(W, H, 2, 99)

base_col = np.array([0.26, 0.29, 0.35], np.float32)
warm_col = np.array([0.40, 0.36, 0.33], np.float32)
col = base_col[None, None, :] * (1 - slab_warm[..., None] * 0.35) + warm_col[None, None, :] * (slab_warm[..., None] * 0.35)
col = col * slab_tone[..., None]
col = col * (0.55 + 0.75 * grain[..., None]) * (0.82 + 0.36 * fine[..., None])
col = col * (1 - bevel[..., None] * 0.6)
col = col * (1 - mortar[..., None] * 0.85)

# Pebbles and chips scattered on the flags.
pebbles = np.zeros((H, W), np.float32)
pimg = Image.new("L", (W, H), 0); pd = ImageDraw.Draw(pimg)
for _ in range(900):
    px, py = rng.integers(0, W), rng.integers(0, H)
    rr = rng.integers(1, 3)
    pd.ellipse([px - rr, py - rr, px + rr, py + rr], fill=int(rng.integers(90, 200)))
pebbles = np.asarray(pimg).astype(np.float32) / 255.0
col = col * (1 - pebbles[..., None] * 0.25) + pebbles[..., None] * 0.18

# Lighting: cool ambient with a strong vignette, warm firelight falling off from the pit.
vig = np.clip(1.0 - (np.sqrt(((xx - CX) / (W * 0.58)) ** 2 + ((yy - CY) / (H * 0.78)) ** 2)) ** 1.8, 0, 1)
ambient = np.array([0.62, 0.72, 0.92], np.float32)[None, None, :] * (0.14 + 0.62 * vig[..., None])
fire = np.exp(-(r / (430.0 * ZOOM)) ** 2)
flicker = 1.0 + (fbm(W, H, 160, 2, 77) - 0.5) * 0.25
firelight = np.array([1.0, 0.58, 0.26], np.float32)[None, None, :] * (fire * 1.9 * flicker)[..., None]
lit = col * (ambient + firelight)
bloom = np.array([1.0, 0.45, 0.14], np.float32)[None, None, :] * (np.exp(-(r / (170.0 * ZOOM)) ** 2) * 0.28)[..., None]
lit = lit + bloom

# Cast shadow under the bowl.
shadow = np.exp(-(((dx) / (300.0 * ZOOM)) ** 2 + ((dy - 40 * ZOOM) / (190.0 * ZOOM)) ** 2) ** 1.4)
lit = lit * (1 - shadow[..., None] * 0.55)

# Embers drifting near the fire.
eimg = Image.new("RGB", (W, H), 0); ed = ImageDraw.Draw(eimg)
for _ in range(140):
    ang = rng.random() * 2 * np.pi; dist = (abs(rng.normal(0, 260)) + 120) * ZOOM
    ex, ey = CX + math.cos(ang) * dist, CY + math.sin(ang) * dist * 0.75
    rr = rng.random() * 1.6 + 0.6
    c = (255, int(120 + rng.random() * 90), int(30 + rng.random() * 40))
    ed.ellipse([ex - rr, ey - rr, ex + rr, ey + rr], fill=c)
eglow = eimg.filter(ImageFilter.GaussianBlur(3))
lit = lit + np.asarray(eimg).astype(np.float32) / 255.0 * 0.9 + np.asarray(eglow).astype(np.float32) / 255.0 * 0.5

out = np.clip(lit, 0, 1)
out = out ** (1 / 1.05)
floor = Image.fromarray((out * 255).astype(np.uint8), "RGB").convert("RGBA")

# The bowl: key out the flat grey backdrop, keep the cast shadow only faintly.
ref = Image.open(sys.argv[1]).convert("RGB")
BOWL_CX, BOWL_CY, BOWL_RX, BOWL_RY = 288, 196, 150, 150
SWORD_A, SWORD_B, SWORD_W = (320, 150), (440, 10), 80
ra = np.asarray(ref).astype(np.float32)
bg = np.array([44, 44, 43], np.float32)
diff = np.abs(ra - bg).sum(axis=2)
from scipy import ndimage
keyed = ndimage.binary_closing(diff > 26, iterations=2)
keyed = ndimage.binary_fill_holes(keyed)
rh, rw = keyed.shape
shape = Image.new("L", (rw, rh), 0); sd = ImageDraw.Draw(shape)
sd.ellipse([BOWL_CX - BOWL_RX, BOWL_CY - BOWL_RY, BOWL_CX + BOWL_RX, BOWL_CY + BOWL_RY], fill=255)
sd.line([SWORD_A, SWORD_B], fill=255, width=SWORD_W)
keep = keyed & (np.asarray(shape) > 0)
m = Image.fromarray((keep * 255).astype(np.uint8))
m = m.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.0))
bowl = ref.convert("RGBA"); bowl.putalpha(m)
bowl = bowl.crop(bowl.getbbox())
scale = 580 * ZOOM / bowl.width
bowl = bowl.resize((int(bowl.width * scale), int(bowl.height * scale)), Image.LANCZOS)
bowl = bowl.filter(ImageFilter.UnsharpMask(radius=2, percent=60, threshold=2))
# Match the bowl to the floor's cooler, darker grade.
ba = np.asarray(bowl).astype(np.float32)
ba[..., :3] = np.clip(ba[..., :3] * np.array([0.98, 0.95, 0.96]) * 0.96, 0, 255)
bowl = Image.fromarray(ba.astype(np.uint8), "RGBA")

bx = CX - int(bowl.width * 0.46)
by = CY - int(bowl.height * 0.55)
floor.alpha_composite(bowl, (bx, by))

# Fire glow over the pit on top of everything, so the flames light the rim.
glow = Image.new("RGBA", (W, H), (0, 0, 0, 0)); gd = ImageDraw.Draw(glow)
gd.ellipse([CX - 130 * ZOOM, CY - 80 * ZOOM, CX + 130 * ZOOM, CY + 80 * ZOOM], fill=(255, 140, 50, 70))
glow = glow.filter(ImageFilter.GaussianBlur(60 * ZOOM))
floor = Image.alpha_composite(floor, glow)

floor.convert("RGB").save(sys.argv[2], quality=95)
# The flames sit at this fraction of the bowl cut-out; the scene needs it as a UV to hang the live fire on.
fire_uv = ((bx + bowl.width * 0.465) / W, (by + bowl.height * 0.505) / H)
print("saved", sys.argv[2], floor.size, "bowl", bowl.size, "at", (bx, by), "fire uv %.4f %.4f" % fire_uv)
