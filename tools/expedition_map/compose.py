"""Compose one detailed native expedition map with restrained atmosphere.

Preserve the logical layout. Composite at source resolution so a close camera
view reveals native texture detail. Mist stays near the ground; smoke, bloom
and motes leave landmarks legible. No generated bitmap inputs.
"""
import json, math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import argparse, hashlib
parser = argparse.ArgumentParser(description='Compose one native DD2 expedition overview at source resolution.')
parser.add_argument('--kit', required=True, type=Path)
parser.add_argument('--render', required=True, type=Path)
parser.add_argument('--layout', required=True, type=Path)
parser.add_argument('--out', required=True, type=Path)
parser.add_argument('--scale', type=int, choices=(1, 2), default=2)
args = parser.parse_args()
KIT = args.kit.resolve(); R = args.render.resolve(); OUT = args.out.resolve()
if OUT.is_relative_to(Path(__file__).resolve().parents[2]):
    raise ValueError('Extracted game art must remain outside the repository')
OUT.mkdir(parents=True, exist_ok=True)
S = args.scale; W, H = 1920 * S, 1080 * S; BASE = 56.0
META = json.loads((R / 'map.json').read_text(encoding='utf-8'))
for view in META['views'].values():
    for spot in view['hubs'].values():
        spot['hub'] = [v * S for v in spot['hub']]
        if spot['pix'] is not None: spot['pix'] *= S
HAZE = np.array((58, 32, 44), np.float32)                 # far distance colour, toward the red sky
def arr(im): return np.asarray(im).astype(np.float32)
def load(p, mode='RGBA'):                                  # renders are supersampled; bring them to screen size
    im = Image.open(p)
    if mode == 'F': im = im.convert('F'); return im if im.size == (W, H) else im.resize((W, H), Image.BILINEAR)
    im = im.convert(mode); return im if im.size == (W, H) else im.resize((W, H), Image.LANCZOS)
def rgba(a, alpha): return Image.merge('RGBA', (*Image.fromarray(a.clip(0, 255).astype(np.uint8)).split(), alpha))
def noise(seed, cells=(20, 44), blur=26 * S):
    r = np.random.default_rng(seed); return arr(Image.fromarray((r.random(cells) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC).filter(ImageFilter.GaussianBlur(blur))) / 255
def scene(view):
    V = META['views'][view]; info = V['hubs']; d = R / view; show = V['regions']; yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    pix = {t: info[t]['pix'] or 0 for t in info}; mx, my = info['mountain']['hub']; mw = pix['mountain'] * BASE * 1.1
    # sky: DD2's cloud plate graded red, with the Mountain's glow behind its peak
    sky = arr(Image.open(KIT / 'exp/scenes_scenes_main_menu/tex-18.png').convert('RGB').resize((W, H), Image.LANCZOS)) * np.array((1.25, 0.5, 0.5))
    a = sky * 0.5 + np.array((22, 6, 10)); gl = np.exp(-(((xx - mx) / (mw * .8)) ** 2 + ((yy - (my - mw * .36)) / (mw * .6)) ** 2))[..., None]
    a += gl * np.array((170, 46, 22)) * .7; img = Image.fromarray(a.clip(0, 255).astype(np.uint8)).convert('RGBA')
    # side ranges as cold silhouettes, then the Mountain: lifted, warmed, rimmed by the glow, lit at its foot
    rg = load(d / 'range.png'); ra = arr(rg.convert('RGB')) * np.array((.5, .5, .68)) * .9 + HAZE * .5; img.alpha_composite(rgba(ra, rg.getchannel('A')))
    mt = load(d / 'mountain.png'); al = mt.getchannel('A'); m = arr(mt.convert('RGB'))
    lum = m.mean(-1, keepdims=True); m = m * np.array((1.2, .8, .86)) + np.clip(lum - 40, 0, 255) * np.array((.75, .2, .1))
    rim = arr(ImageChops.subtract(al, al.filter(ImageFilter.MinFilter(10 * S + 1)).filter(ImageFilter.GaussianBlur(6 * S)))) / 255
    core = np.exp(-(((xx - mx) / (mw * .5)) ** 2 + ((yy - my) / (mw * .22)) ** 2))
    m += rim[..., None] * np.array((255, 96, 44)) * .85 + core[..., None] * np.array((150, 44, 20)) * .8
    m = m * (1 - core[..., None] * .35) + (HAZE + np.array((60, 14, 6))) * core[..., None] * .35                    # haze swallows the foot
    img.alpha_composite(rgba(m, al))
    # the land: distance haze from the depth pass, then mist lying in the valley
    be = load(d / 'beauty.png'); c = arr(be.convert('RGB')); dist = arr(load(d / 'depth.png', 'F')) / 65535 * META['dmax']
    c = 255 * (c / 255) ** .86 * 1.16                                                                             # the backlit ground needs lifting
    hz = ((1 - np.exp(-dist / 1400.0)) * .65)[..., None]; c = c * (1 - hz) + (HAZE + np.array((26, 6, 2))) * hz
    ref = max(info[view]['depth'] if view in info and view != 'overview' else 420, 60)
    mist = noise(9) * np.clip((dist - ref * .7) / (ref * 1.2), 0, 1) * np.clip(1 - (dist - ref * 3.2) / (ref * 2), 0, 1) * .10
    c = c * (1 - mist[..., None]) + np.array((96, 74, 92)) * mist[..., None]
    img.alpha_composite(rgba(c, be.getchannel('A'))); a = arr(img.convert('RGB'))
    if 'forest' in show:                                                                                             # lantern at the Tangle's checkpoint, teal air round the lair
        fx, fy = info['forest']['hub']; p = pix['forest']
        w = np.exp(-(((xx - (fx - 13 * p)) / (13 * p)) ** 2 + ((yy - (fy - 2 * p)) / (9 * p)) ** 2))[..., None]
        t = np.exp(-(((xx - fx) / (34 * p)) ** 2 + ((yy - (fy - 6 * p)) / (17 * p)) ** 2))[..., None]
        a = a * (1 + w * np.array((.6, .36, .08)) + t * np.array((-.04, .2, .18))) + w * np.array((14, 8, 2)) + t * np.array((0, 9, 8))
    if 'farm' in show:
        fx, fy = info['farm']['hub']; p = pix['farm']; t = np.exp(-(((xx - fx) / (34 * p)) ** 2 + ((yy - (fy - 5 * p)) / (16 * p)) ** 2))[..., None]; a = a * (1 + t * np.array((.2, .16, -.05)))
    if 'coast' in show:
        fx, fy = info['coast']['hub']; p = pix['coast']; t = np.exp(-(((xx - fx) / (34 * p)) ** 2 + ((yy - (fy - 5 * p)) / (16 * p)) ** 2))[..., None]; a = a * (1 + t * np.array((-.1, .04, .16)))
    def blobs(points, blur):                                                                                         # soft additive shapes from (x, y, radius, strength)
        m = Image.new('L', (W, H), 0); dr_ = ImageDraw.Draw(m)
        for x, y, r, s in points: dr_.ellipse([x - r, y - r, x + r, y + r], fill=int(255 * min(1, s)))
        return arr(m.filter(ImageFilter.GaussianBlur(blur))) / 255
    smoke = None
    wisp = np.clip((noise(31, (36, 12), 18 * S) - .36) * 3.4, 0, 1); grain = np.clip((noise(47, (60, 106), 7 * S) - .38) * 3.6, 0, 1)
    if 'coast' in show:                                                                                              # the Shroud: a bank of sea mist over the harbour and the bay
        fx, fy = info['coast']['hub']; p = pix['coast']
        m = np.exp(-(((xx - (fx + 14 * p)) / (50 * p)) ** 2 + ((yy - (fy + 4 * p)) / (11 * p)) ** 2)) * (0.35 + 0.9 * wisp)
        m += np.exp(-(((xx - (fx + 2 * p)) / (30 * p)) ** 2 + ((yy - (fy + 6 * p)) / (5 * p)) ** 2)) * 0.5 * (0.4 + grain)
        m = np.clip(m * .13, 0, .16)[..., None]; a = a * (1 - m) + np.array((150, 164, 180)) * m
    if 'farm' in show:                                                                                               # the Foetor: sour yellow air hanging over the fields
        fx, fy = info['farm']['hub']; p = pix['farm']
        m = np.exp(-(((xx - fx) / (36 * p)) ** 2 + ((yy - (fy + 2 * p)) / (13 * p)) ** 2)) * (0.2 + 1.0 * wisp) * .26
        m = np.clip(m * .4, 0, .12)[..., None]; a = a * (1 - m) + np.array((158, 150, 52)) * m
    if 'forest' in show:                                                                                             # the Tangle: drifting spores
        fx, fy = info['forest']['hub']; p = pix['forest']; rs = np.random.default_rng(5)
        pts = [(rs.normal(fx, 24 * p), fy - abs(rs.normal(5 * p, 9 * p)), rs.uniform(.9, 2.2) * max(p, 7) / 11, 1) for _ in range(32)]
        mo = blobs(pts, 1.0 * S)[..., None]; a += mo * np.array((90, 255, 215)) * .85 + arr(Image.fromarray((mo[..., 0] * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(6 * S)))[..., None] / 255 * np.array((40, 170, 150)) * 1.2
    if 'city' in show:                                                                                               # the Sprawl: smoke columns leaning with the wind
        hx, hy = info['city']['hub']; p = pix['city']; rs = np.random.default_rng(23); pts = []
        for dx in (-17, 4, 21):
            x0, y0 = hx + dx * p, hy - rs.uniform(7, 12) * p; lean = rs.uniform(.25, .5); tall = rs.uniform(26, 40) * p
            for k in np.linspace(0, 1, 26):
                pts.append((x0 + lean * tall * k ** 1.5 + rs.normal(0, 1.2 * p) * k, y0 - tall * k, (2.0 + 7.5 * k) * p, (1 - k) ** .5 * .9 * min(1, k * 5)))
        sm = np.clip(blobs(pts, max(3, 1.6 * p)) * .55 * (0.6 + 0.6 * wisp), 0, .30)[..., None]
        lit_ = np.exp(-((yy - hy + 10 * p) / (16 * p)) ** 2)[..., None]                                                # the columns glow where they leave the fire
        smoke = (sm, lit_)                                                                                         # laid over the image after the fire's bloom, so it stays dark
    rs = np.random.default_rng(77)                                                                                   # ash and embers on the wind from the Mountain
    emb = blobs([(rs.uniform(0, W), rs.uniform(0, H * .6) ** 1.0, rs.uniform(.7, 1.6) * S, 1) for _ in range(28)], .8)[..., None]
    a += emb * np.array((255, 130, 50)) * .55
    if 'city' in show:                                                                                               # the Sprawl burns: ember light, bloom and sparks
        hx, hy = info['city']['hub']; p = pix['city']
        d2 = ((xx - hx) / (32 * p)) ** 2 + ((yy - hy + 8 * p) / (18 * p)) ** 2; glow = np.exp(-d2 * 1.4)[..., None]
        a = a * (1 + glow * np.array((0.5, 0.1, -0.1))) + glow * np.array((34, 10, 2))
        rng = np.random.default_rng(11); sp = Image.new('RGB', (W, H)); dr = ImageDraw.Draw(sp)
        for _ in range(48):
            x = rng.normal(hx, 15 * p); y = hy - abs(rng.normal(9 * p, 10 * p)); r = rng.uniform(.8, 2.4) * max(p, 7) / 12; dr.ellipse([x - r, y - r, x + r, y + r], fill=(255, int(rng.uniform(110, 190)), 40))
        a += arr(sp.filter(ImageFilter.GaussianBlur(1.2))) * 0.9 + arr(sp.filter(ImageFilter.GaussianBlur(7))) * 0.8
    warm = np.clip((a[..., 0] - 130) / 90, 0, 1) * np.clip((a[..., 0] - a[..., 2] - 50) / 60, 0, 1); gl = Image.fromarray((a * warm[..., None]).clip(0, 255).astype(np.uint8))
    a = a * 1.02 + arr(gl.filter(ImageFilter.GaussianBlur(6 * S))) * .15 + arr(gl.filter(ImageFilter.GaussianBlur(22 * S))) * .20
    if smoke is not None: a = a * (1 - smoke[0] * .62) + (np.array((26, 17, 21)) + smoke[1] * np.array((46, 16, 6))) * smoke[0] * .62
    vx = (np.arange(W) - 960 * S) / (1080 * S); vy = (np.arange(H) - 520 * S) / (720 * S); a *= (1 - .5 * np.clip(vx[None, :] ** 2 + vy[:, None] ** 2 - .45, 0, 1))[..., None]
    a *= (1 - .5 * np.clip((np.arange(H) - 930 * S) / (200 * S), 0, 1))[:, None, None]                                         # the nearest ground falls into shadow
    return Image.fromarray(a.clip(0, 255).astype(np.uint8)).convert('RGBA')

image = scene('overview').convert('RGB')
image.save(OUT / 'map3d.png')
# Controls and camera continue to use exactly the accepted logical layout.
(OUT / 'map3d.json').write_bytes(args.layout.read_bytes())
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
(OUT / 'provenance.json').write_text(json.dumps({
    'ai_generated_art': False, 'size': list(image.size), 'logical_canvas': [1920, 1080],
    'layout_sha256': sha(args.layout),
    'source_passes': {p.name: sha(p) for p in (R / 'overview').glob('*.png')},
    'native_ground_fields': {name: sha(KIT / 'roads/_tex' / name) for name in (
        'tex-1115460631562147575-512x512.png', 'tex-5448216684445144371-2048x2048.png',
        'tex-8137179675899024447-2048x2048.png', 'tex--8852572910149451100-2048x2048.png')},
    'native_model_loader': sha(KIT / 'tools/bl_lib.py'),
    'recipes': {p.name: sha(p) for p in Path(__file__).resolve().parent.glob('*.py')},
}, indent=2), encoding='utf-8')
print('Composed native overview', image.size, flush=True)
