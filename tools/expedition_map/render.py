"""Render the native expedition valley without baked fog or interface.

Keeps the accepted v08 camera, hubs, native model geometry, UVs and paint.
Terrain uses restrained relief strokes and local native root/stubble detail.
Only the overview is needed by the persistent in-game camera. All output,
including game-derived textures, stays outside Git.
Run with Blender --background --python render.py -- --kit KIT --out OUTPUT.
"""
import sys, json, math
from pathlib import Path
import numpy as np
import bpy
import argparse
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--kit', required=True, type=Path)
parser.add_argument('--out', required=True, type=Path)
parser.add_argument('--scale', type=float, choices=(1.0, 2.0), default=2.0)
parser.add_argument('--samples', type=int, default=16)
parser.add_argument('--vendor', type=Path, help='Optional Blender-Python-compatible Pillow install')
parser.add_argument('--quick', action='store_true')
parser.add_argument('--terrain-plates', action='store_true', help='Also render terrain, visible scenery coverage and road protection for painting')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
if not 1 <= args.samples <= 64: parser.error('--samples must be between1 and64')
if args.quick and args.terrain_plates: parser.error('--terrain-plates requires a full-resolution reference')
KIT = args.kit.resolve(); R = args.out.resolve()
if R.is_relative_to(Path(__file__).resolve().parents[2]):
    raise ValueError('Extracted game art must remain outside the repository')
R.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(KIT / 'tools'))
if args.vendor: sys.path.insert(0, str(args.vendor))
import bl_lib as L
from PIL import Image, ImageDraw, ImageFilter
QUICK = args.quick; DRY = False; ONLY = ['overview']
W, H = 1920, 1080; FOV = 40.0; K = (H / 2) / math.tan(math.radians(FOV / 2)); BASE = 56.0; DMAX = 2000.0
def sstep(a, b, x): t = np.clip((np.asarray(x, float) - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
def basis(pitch): p = math.radians(pitch); f = np.array([0, math.cos(p), -math.sin(p)]); r = np.array([1.0, 0, 0]); return f, r, np.cross(r, f)
def ray(px, py, pitch): f, r, u = basis(pitch); return f + r * (px - W / 2) / K + u * (H / 2 - py) / K
def project(p, loc, pitch):
    f, r, u = basis(pitch); v = np.array(p, float) - np.array(loc); z = v @ f; return float(W / 2 + K * (v @ r) / z), float(H / 2 - K * (v @ u) / z), float(z)
# ---- the land -------------------------------------------------------------------------------------------------
X0, X1, Z0, Z1, STEP = -576.0, 576.0, -150.0, 810.0, 2.0       # 6 x 5 texture tiles of 192 units
xs = np.arange(X0, X1 + .1, STEP); zs = np.arange(Z0, Z1 + .1, STEP); GX, GZ = np.meshgrid(xs, zs)
def fbm(seed, octaves=((90, 1.0), (40, .5), (18, .25), (8, .12))):
    rng = np.random.default_rng(seed); out = np.zeros(GX.shape, np.float32)
    for size, amp in octaves:
        g = rng.random((int((Z1 - Z0) / size) + 3, int((X1 - X0) / size) + 3)).astype(np.float32)
        out += amp * (np.asarray(Image.fromarray(g).resize((GX.shape[1], GX.shape[0]), Image.BICUBIC)) - .5)
    return out
# region: hub (x, depth), plateau height, cluster scale
HUBS = {'forest': (-24, 62, 5, 1.45), 'coast': (80, 215, 27, 2.1), 'farm': (-68, 205, 34, 1.7), 'city': (-27, 362, 74, 2.7), 'mountain': (112, 690, 128, 8.2)}
T = sstep(30, 620, GZ); AX = 18 * np.sin(GZ / 85.0); WID = 175 - 80 * sstep(150, 580, GZ)
SIDE = sstep(WID, WID + 150, np.abs(GX - AX)); N1 = fbm(3); N2 = fbm(8, ((30, 1.0), (12, .5), (5, .25)))
HT = 120 * T ** 1.1 + SIDE * (34 + 62 * T) + N1 * (7 + 26 * SIDE) + 46 * sstep(640, 790, GZ)
N3 = fbm(21, ((120, 1.0), (55, .5))); Q = HT + N1 * 16 + N3 * 10                    # warp the levels so the ledges wander and break up
ST = 15.0; STEPPED = np.floor(Q / ST) * ST + ST * sstep(.66, 1.0, (Q / ST) % 1.0) - (Q - HT); TW_ = np.clip(.04 + .55 * SIDE, 0, 1) * sstep(60, 140, GZ) * sstep(-.22, .12, N3)
HT = HT * (1 - TW_) + STEPPED * TW_                        # the slopes break into rock terraces
BAY = (1 - sstep(.55, 1.0, np.sqrt(((GX - 196) / 108) ** 2 + ((GZ - 196) / 102) ** 2)))            # the Shroud's water
HT = HT * (1 - BAY) + 10.0 * BAY; WATER = 13.5
PLAT = {}
for k, (hx, hz, hh, s) in HUBS.items():
    rr = BASE * s; d = np.sqrt(((GX - hx) / (rr * .66)) ** 2 + ((GZ - hz) / (rr * .5)) ** 2); w = 1 - sstep(.85, 1.7, d); PLAT[k] = w; HT = HT * (1 - w) + hh * w
HT = HT + N2 * 2.0 * (1 - np.clip(sum(PLAT.values()), 0, 1))
def hgt(x, z):
    fx = np.clip((x - X0) / STEP, 0, len(xs) - 1.001); fz = np.clip((z - Z0) / STEP, 0, len(zs) - 1.001); i, j = int(fx), int(fz); a, b = fx - i, fz - j
    return float(HT[j, i] * (1 - a) * (1 - b) + HT[j, i + 1] * a * (1 - b) + HT[j + 1, i] * (1 - a) * b + HT[j + 1, i + 1] * a * b)
def smooth_path(pts, n=400):
    pts = np.array(pts, float); t = np.r_[0, np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))]; u = np.linspace(0, t[-1], n)
    p = np.c_[np.interp(u, t, pts[:, 0]), np.interp(u, t, pts[:, 1])]; k = np.ones(31) / 31
    q = np.c_[np.convolve(np.pad(p[:, 0], 15, 'edge'), k, 'valid'), np.convolve(np.pad(p[:, 1], 15, 'edge'), k, 'valid')]; return q
MAIN = [(8, -150), (2, -40), (14, 60), (30, 130), (8, 210), (-4, 290), (30, 380), (62, 470), (84, 560), (112, 650)]
ROADS = [smooth_path(MAIN, 700), smooth_path([(12, 46), (0, 44), (-14, 40)], 80), smooth_path([(12, 196), (32, 203), (54, 206)], 120),
         smooth_path([(10, 205), (-25, 198), (-58, 188)], 150), smooth_path([(-2, 300), (-12, 330), (-30, 345)], 120)]
RP = np.concatenate(ROADS)
CAM0 = ((-6.0, -128.0, 47.0), 3.5)                          # overview camera and its pitch below the horizon
info0 = {k: project((v[0], v[1], v[2]), *CAM0) for k, v in HUBS.items()}
print('overview hubs', {k: (round(a), round(b), round(K / c * BASE * HUBS[k][3])) for k, (a, b, c) in info0.items()}, flush=True)
if DRY: sys.exit()
# ---- paint the land: biome colour, banded relief light, inked strata and hatching, water, road, regional light ------
NTX, NTZ, TP = 6, 5, 1024; TW, TH = NTX * TP, NTZ * TP
def up(a): return np.asarray(Image.fromarray(a.astype(np.float32)).resize((TW, TH), Image.BICUBIC))
gy, gx_ = np.gradient(HT, STEP); slope = np.sqrt(gx_ ** 2 + gy ** 2); nrm = np.dstack([-gx_, -gy, np.ones_like(HT)]); nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
lit = np.clip(nrm @ (np.array([-.5, .45, .74]) / np.linalg.norm([-.5, .45, .74])), 0, 1)
COL = {'valley': (44, 30, 20), 'forest': (10, 34, 31), 'farm': (62, 50, 20), 'coast': (40, 46, 54), 'city': (44, 20, 16), 'mountain': (34, 24, 30)}
wts = {k: np.exp(-(((GX - HUBS[k][0]) / (BASE * HUBS[k][3] * 1.25)) ** 2 + ((GZ - HUBS[k][1]) / (BASE * HUBS[k][3] * 1.0)) ** 2)) * 2.2 for k in HUBS}
wts['valley'] = np.full(HT.shape, .55); wsum = sum(wts.values())
col = sum((wts[k] / wsum)[..., None] * np.array(COL[k], np.float32) for k in wts)
snow = sstep(138, 172, HT + N1 * 20) * sstep(430, 560, GZ) * (1 - PLAT['mountain'] * .6); col = col * (1 - snow[..., None] * .8) + np.array((104, 108, 128), np.float32) * snow[..., None] * .8
rock = sstep(.5, 1.0, slope); col = col * (1 - rock[..., None] * .8) + np.array((40, 34, 42), np.float32) * rock[..., None] * .8
POOL = {'city': (200, 70, 24), 'farm': (120, 100, 36), 'coast': (40, 80, 104), 'forest': (20, 104, 90), 'mountain': (90, 22, 14)}
for k, c in POOL.items():
    hx, hz, hh, s = HUBS[k]; rr = BASE * s; d2 = ((GX - hx) / (rr * .7)) ** 2 + ((GZ - hz + rr * .05) / (rr * .55)) ** 2; col += np.exp(-d2 * 1.2)[..., None] * np.array(c, np.float32) * .3
wat = sstep(WATER + .6, WATER - .6, HT) * (BAY > .02); shore = np.exp(-((HT - WATER - .4) / 1.2) ** 2) * (BAY > .02)
rd = np.full(HT.shape, 1e9, np.float32); P_ = np.dstack([GX, GZ]).reshape(-1, 2)
for i in range(0, len(RP), 40): rd = np.minimum(rd, np.sqrt(((P_[:, None, :] - RP[None, i:i + 40, :]) ** 2).sum(-1)).min(1).reshape(HT.shape))
tex = np.dstack([up(col[..., i]) for i in range(3)]); del col
rng = np.random.default_rng(2)
fine = np.asarray(Image.fromarray((rng.random((TH, TW)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.6))).astype(np.float32) / 255
blot = np.asarray(Image.fromarray((rng.random((TH // 6, TW // 6)) * 255).astype(np.uint8)).resize((TW, TH), Image.BICUBIC)).astype(np.float32) / 255
litu = up(lit); slu = up(slope); htu = up(HT); n2u = up(N2)
band = np.floor(np.clip(litu + (blot - .5) * .16, 0, .999) * 4) / 3.0                                       # light in four flat bands, like inked washes
tex *= (0.66 + 0.62 * (0.3 * litu + 0.7 * band))[..., None] * (0.74 + 0.52 * fine)[..., None] * (0.86 + 0.3 * blot)[..., None]
strata = np.exp(-((((htu + n2u * 3.5) / 4.6) % 1.0 - .5) / .09) ** 2) * sstep(.42, .8, slu)                      # ink lines along the rock terraces
wx_ = np.linspace(X0, X1, TW, dtype=np.float32)[None, :]; wz_ = np.linspace(Z0, Z1, TH, dtype=np.float32)[:, None]
hatch = (np.sin((wx_ * .9 + wz_ * 1.5) * 1.9 + blot * 5) > .55) * sstep(.42, .25, litu) * sstep(.22, .5, slu)    # hatching on the shaded faces
edge = sstep(.5, .95, slu) * (fine > .52)                                                                        # broken ink on the steepest faces
tex *= (1 - np.clip(.16 * strata + .12 * hatch + .26 * edge, 0, .9))[..., None]; del strata, hatch, edge, band
wu = up(wat.astype(np.float32)); shu = up(shore.astype(np.float32)); ripple = (np.sin(wz_ * 2.6 + blot * 9 + wx_ * .25) > .82) * 1.0
tex = tex * (1 - wu[..., None]) + (np.array((11, 30, 44), np.float32) * (0.8 + .5 * blot)[..., None] + ripple[..., None] * np.array((26, 44, 54), np.float32)) * wu[..., None]
tex += shu[..., None] * np.array((70, 80, 88), np.float32) * .55 * (fine > .45)[..., None]
def tiled(path, period):                                   # a DD2 texture tiled in world space (period in units)
    im = Image.open(path).convert('RGB'); px = int(period * TW / (X1 - X0)); im = im.resize((px, px), Image.LANCZOS); a = np.asarray(im).astype(np.float32)
    return np.tile(a, (TH // px + 1, TW // px + 1, 1))[:TH, :TW]
tex += np.clip(tiled(KIT / 'roads/_tex/tex-1115460631562147575-512x512.png', 26) - np.array((40, 28, 28), dtype=np.float32), 0, 255) * 1.3 * (1 - wu[..., None])   # DD2's ground strokes
# Native painted roots and stubble remain local to their biomes. These are
# complete source fields, not colour noise or atlas fragments repainted by us.
for biome, name, period, amount in (
    ('forest', 'tex-8137179675899024447-2048x2048.png', 24, .30),
    ('farm', 'tex--8852572910149451100-2048x2048.png', 28, .36)):
    native = tiled(KIT / 'roads/_tex' / name, period)
    influence = np.clip(up(wts[biome] / wsum) * amount, 0, amount) * (1 - wu)
    # Retain relief colour and light while exposing the native brush marks.
    detail = np.clip(native.mean(-1) / max(float(native.mean()), 1), .25, 2)
    tex *= (1 - influence + influence * detail)[..., None]
    del native, influence, detail
rdu = up(rd); cob = tiled(KIT / 'roads/_tex/tex-5448216684445144371-2048x2048.png', 22); rw = (sstep(5.2, 3.4, rdu) * (0.75 + .25 * fine))[..., None]
tex = tex * (1 - rw) + cob * 1.25 * rw; tex *= (1 - .6 * np.exp(-((rdu - 5.5) / .8) ** 2))[..., None]; del cob
t8 = tex.clip(0, 255).astype(np.uint8); del tex
for tz in range(NTZ):
    for tx in range(NTX): Image.fromarray(t8[tz * TP:(tz + 1) * TP, tx * TP:(tx + 1) * TP][::-1]).save(R / f'terrain_{tx}_{tz}.png')
del t8, fine, blot, litu, slu, htu, n2u, wu, shu, rdu, rw, ripple, wx_, wz_; import gc; gc.collect()
print('terrain painted', flush=True)
# ---- build ---------------------------------------------------------------------------------------------------
L.reset(); L.DROP_MATS = L.DROP_MATS + ['ground_dark']
CX = (len(xs) - 1) // NTX; CZ = (len(zs) - 1) // NTZ
for tz in range(NTZ):
    for tx in range(NTX):                                  # one mesh and one 1024 texture per 192-unit tile, so the ground stays sharp in memory limits
        sx = slice(tx * CX, (tx + 1) * CX + 1); sz = slice(tz * CZ, (tz + 1) * CZ + 1); px_, pz_, ph_ = GX[sz, sx], GZ[sz, sx], HT[sz, sx]; n1, n0 = px_.shape
        verts = np.dstack([px_, pz_, ph_]).reshape(-1, 3); idx = np.arange(n1 * n0).reshape(n1, n0); faces = np.dstack([idx[:-1, :-1], idx[:-1, 1:], idx[1:, 1:], idx[1:, :-1]]).reshape(-1, 4)
        me = bpy.data.meshes.new(f'terrain_{tx}_{tz}'); me.from_pydata(verts.tolist(), [], faces.tolist()); me.update()
        u_ = (verts[:, 0] - px_[0, 0]) / (px_[0, -1] - px_[0, 0]); v_ = (verts[:, 1] - pz_[0, 0]) / (pz_[-1, 0] - pz_[0, 0]); e = .5 / TP
        uvv = np.c_[e + u_ * (1 - 2 * e), e + v_ * (1 - 2 * e)]; uv = me.uv_layers.new(name='UVMap'); uv.data.foreach_set('uv', uvv[faces.reshape(-1)].reshape(-1).astype(np.float32))
        to = bpy.data.objects.new(me.name, me); bpy.context.collection.objects.link(to); to['tag'] = 'terrain' if args.terrain_plates else 'world'
        tm = bpy.data.materials.new(me.name); tm.use_nodes = True; nt = tm.node_tree; nt.nodes.clear(); o = nt.nodes.new('ShaderNodeOutputMaterial'); em = nt.nodes.new('ShaderNodeBsdfDiffuse')   # lit by the sun below
        ti = nt.nodes.new('ShaderNodeTexImage'); ti.image = bpy.data.images.load(str(R / f'terrain_{tx}_{tz}.png')); ti.extension = 'EXTEND'
        nt.links.new(ti.outputs['Color'], em.inputs['Color']); nt.links.new(em.outputs[0], o.inputs[0]); me.materials.append(tm)
# a low sun from behind and to the left, where the Mountain's glow is: the unlit scenery models cast real shadows onto the lit ground
from mathutils import Vector
sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 2.3; sun.angle = math.radians(2.5); so = bpy.data.objects.new('sun', sun); bpy.context.collection.objects.link(so)
so.rotation_euler = Vector((-.5, .55, .42)).to_track_quat('Z', 'Y').to_euler(); AMBIENT = (.68, .64, .74, 1)
E, RD = 'exp', 'roads'; PROTO = {}
from mathutils import Matrix
def folder(a): return KIT / RD / a if (KIT / RD / a / 'scene.json').exists() else KIT / E / a
def put(asset, x, z, width, rot, shade, tag, sink=0.0, y=None, stretch=1.0):
    f = folder(asset); lo, hi = L.bbox(f); a = math.radians(rot); wx = abs((hi[0] - lo[0]) * math.cos(a)) + abs((hi[2] - lo[2]) * math.sin(a)); sc = width / wx
    M, size = L.place_matrix(f, (x, (hgt(x, z) if y is None else y) - (hi[1] - lo[1]) * sc * sink, z), rot, sc)
    if stretch != 1.0: S_ = np.eye(4); S_[0, 0] = stretch; S_[0, 3] = x * (1 - stretch); M = S_ @ M        # widen about its own centre
    shade = round(shade * 8) / 8
    if tag != 'world': L.build(f, M, shade=shade, tag=tag); return
    key = (asset, shade)                                    # planted scenery is instanced: one mesh per model and shade, many placements
    if key not in PROTO: PROTO[key] = L.build(f, None, shade=shade, tag='proto')[0]
    B = Matrix((L.CHANGE @ M @ L.CHANGE).tolist())
    for ob in PROTO[key]:
        o2 = ob.copy(); bpy.context.collection.objects.link(o2); o2.matrix_world = B; o2['tag'] = tag
SH = {'city': 1.7, 'farm': 1.25, 'coast': 1.5, 'forest': 2.9}
# parts: (asset, dx, dz (depth), width, rot, shade multiplier) in cluster units; each region has one dominant landmark
P = {
 'city': [('city_skirt_mid_03', -15, 13, 6.5, 0, .6), ('city_skirt_mid_03', -1, 15, 7, 180, .6), ('city_skirt_mid_03', 13, 13, 6.5, 0, .6),
          ('decor_tile_2X2_b', -9, 2, 24, 0, 1), ('decor_tile_2X2', 12, 3, 20, 0, 1), ('city_decor_05', 2, -9, 30, 0, 1),
          ('city_tower_02', -20, -4, 7, 0, 1.2), ('city_bigwall_main', 20, -8, 12, 0, 1.1), ('city_ruins_05', -22, -12, 7, 0, 1.2),
          ('city_tower_02', 1, 9, 12, 0, 1.15), ('decor_tile_1x1_any', -29, 5, 13, 0, 1), ('decor_tile_1x1_outside_corner', 29, 6, 13, 0, 1), ('decor_tile_1x1_outside_corner_05', -34, -5, 9, 0, 1.05),
          ('decor_tile_1x1_outside_corner_03', 33, -4, 10, 0, 1.05), ('city_decor_04', -27, 15, 16, 0, .8), ('city_decor_02', 27, 16, 13, 0, .8), ('city_building_destroyed_01', -31, -14, 8, 0, 1.15),
          ('city_tower_03', 14, 14, 5, 0, .9), ('city_tower_01', -12, 15, 5, 0, .9), ('decor_tile_1x1_outside_corner_04', 31, -14, 10, 0, 1.1)],
 # the Foetor: the great windmill on its rise, barns and houses around the yard, fields in front
 'farm': [('farm_windmill_01', 1, 10, 25, 0, 1.05), ('farm_bigbarn', -18, 7, 18, 0, 1.1), ('farm_farmhouse', 19, 6, 12, 0, .65), ('farm_bigbarn_destroyed_01', 31, 11, 10, 0, 1.0),
          ('farm_flathouse', -7, -4, 9, 0, 1.1), ('farm_cottage', 9, -5, 8, 0, 1.15), ('farm_tile_01', -25, -6, 10, 0, 1), ('farm_watertower', 27, -3, 6, 0, 1.2), ('farm_shed', -15, -11, 6, 0, 1.2),
          ('farm_farmhouse_destroyed_01', -31, 9, 9, 0, 1.0), ('farm_tile_33_crops', 18, -13, 12, 0, 1), ('farm_tile_33_crops', 2, -15, 12, 180, 1), ('farm_tile_33_crops', -28, -15, 12, 0, 1),
          ('farm_tile_33_crops', 31, -13, 11, 180, .95), ('farm_tile_33_crops', -34, -2, 10, 0, .9), ('farm_tile_33_crops', 36, 2, 10, 180, .9)],
 # the Shroud: a harbour street under a watch light, sheds and wrecks toward the water
 'coast': [('road_coast_straight_large_01', 0, 2, 44, 0, .34), ('watchtower_main', 30, 3, 7.5, 0, 1.35), ('coast_building_01', -3, 12, 10, 0, 1.3), ('coast_building_large_01', -17, 10, 15, 0, 1.25),
           ('coast_building_large_02', 12, 11, 13, 0, 1.15), ('coast_building_wide_01', -29, 4, 10, 0, 1.2), ('coast_building_03', 21, 4, 7, 0, 1.2), ('coast_building_destroyed_03', -36, 12, 8, 0, 1.1),
           ('coast_building_destroyed_01', -9, -1, 8, 0, 1.15), ('coast_tile_08', 24, -8, 13, 0, 1.1), ('coast_tile_07', 9, -12, 17, 0, 1.0), ('coast_shed_01', -20, -10, 11, 0, 1.2),
           ('coast_tile_15', -5, -14, 9, 0, 1.25), ('coast_tile_09', -33, -9, 13, 0, 1.1), ('coast_tile_13', 36, -13, 13, 0, 1.0)],
 # the Tangle: the lair tower in its thicket, the overgrown checkpoint, a camp, a shrine and graves
 'forest': [('forest_tile_07', -19, 15, 26, 0, .7), ('forest_tile_07', 21, 16, 24, 180, .7), ('forest_tile_12', 1, 19, 20, 0, .7), ('forest_tile_13', -32, 6, 18, 0, .75), ('forest_tile_09', 33, 5, 16, 0, .75),
            ('forest_node_dungeon', 6, 4, 31, 0, .85), ('forest_overgrown_checkpoint', -15, -7, 17, 0, 1.25), ('forest_tent_02', 18, -9, 8, 0, 1.3), ('forest_tent_01', 26, -5, 7, 0, 1.3),
            ('hero_shrine', -3, -12, 6, 0, 1.2), ('gravestone_group_02', -28, -11, 6, 0, 1.7), ('gravestone_group_03', -23, -15, 5, 0, 1.7), ('gravestone_group_01', 9, -16, 7, 0, 1.7),
            ('forest_tile_brambles_01', -34, -4, 9, 0, 1), ('forest_tile_brambles_02', 33, -13, 10, 0, 1), ('forest_tile_11', 3, -18, 10, 0, 1.1)],
}
for tag, parts in P.items():
    hx, hz, hh, s = HUBS[tag]
    for asset, dx, dz, width, rot, shm in parts: put(asset, hx + dx * s, hz + dz * s, width * s, rot, SH[tag] * shm, tag, y=hh)
mx, mz, mh, ms = HUBS['mountain']
put('mainmenu_mountain_mdl', mx, mz + 60, BASE * ms * 1.36, 0, 4.2, 'mountain', sink=.5, y=mh, stretch=1.5)
for dx, dz, w, rot, shd in [(-330, 60, 420, 180, .2), (330, 70, 420, 0, .2), (-170, 30, 280, 0, .26), (200, 26, 280, 180, .26), (-560, 110, 460, 0, .16), (580, 120, 460, 180, .16)]:
    put('mainmenu_light_midground_mtn_01', mx + dx, mz + dz, w, rot, shd, 'range', sink=.2, y=mh)
# ---- plant the land by rule, not at random -----------------------------------------------------------------------
rng = np.random.default_rng(42); placed = []; count = {}
def hubd(k, x, z): hx, hz, hh, s = HUBS[k]; rr = BASE * s; return math.hypot((x - hx) / (rr * .66), (z - hz) / (rr * .5))
def nearest(x, z): return min(HUBS, key=lambda k: hubd(k, x, z))
def road_d(x, z): return float(np.min(np.hypot(RP[:, 0] - x, RP[:, 1] - z)))
def free(x, z, r, tall=True, road=8.0, maxslope=.45, hub_min=1.02):
    if not (X0 + 20 < x < X1 - 20 and Z0 + 20 < z < Z1 - 20): return False
    i = int((x - X0) / STEP); j = int((z - Z0) / STEP)
    if slope[j, i] > maxslope or (BAY[j, i] > .02 and HT[j, i] < WATER + .8) or road_d(x, z) < road: return False
    for k, (hx, hz, hh, s) in HUBS.items():
        rr = BASE * s
        if hubd(k, x, z) < (hub_min if k != 'mountain' else .5): return False
        if tall and k != 'mountain' and abs(x - hx) < rr * .62 and hz - rr * 1.5 < z < hz - rr * .4: return False       # keep the view onto each region open
    return not any(math.hypot(x - px, z - pz) < r + pr for px, pz, pr in placed)
def plant(kind, asset, x, z, width, shade, rot=None):
    put(asset, x, z, width, (float(rng.choice([0, 180])) + float(rng.uniform(-10, 10))) if rot is None else rot, shade, 'world'); placed.append((x, z, width * .36)); count[kind] = count.get(kind, 0) + 1
def grow(z): return 1.0 + max(z, 0) / 330.0                 # far scenery is drawn larger so it still reads
LIVE = ['valley_skirt_low_02', 'valley_skirt_low_03', 'valley_skirt_low_04', 'valley_skirt_low_05', 'valley_skirt_low_06', 'valley_skirt_low_07', 'valley_skirt_low_08', 'valley_tile_01', 'valley_tile_02']
BUSH = ['valley_skirt_high_06', 'valley_skirt_high_07']; DEAD = ['valley_skirt_low_dead_01', 'valley_skirt_low_dead_02', 'valley_skirt_low_dead_03', 'valley_skirt_low_dead_04']
WOOD = ['forest_tile_01', 'forest_tile_02', 'forest_tile_03', 'forest_tile_08', 'forest_tile_13', 'forest_skirt_high_01']; THORN = ['forest_tile_brambles_01', 'forest_tile_brambles_02', 'forest_tile_brambles_04', 'forest_tile_11']
# 1 the road: lamp posts on alternating sides, a watchtower at the first bend, a shrine where the Mountain road begins, cottages at the forks
main = ROADS[0]; tan = np.gradient(main, axis=0); tan /= np.linalg.norm(tan, axis=1, keepdims=True); nor = np.c_[tan[:, 1], -tan[:, 0]]
for n_, i in enumerate(range(150, 640, 34)):
    p = main[i] + nor[i] * (7.5 if n_ % 2 else -7.5)
    if free(p[0], p[1], 2, tall=False, road=5): plant('lamp', 'common_prop_lamppost', float(p[0]), float(p[1]), 2.2 * grow(p[1]), 1.5, rot=0)
for asset, i, side, w in [('watchtower_main', 265, 15, 9), ('valley_cottage_01', 330, -16, 15), ('valley_cottage_02', 420, 17, 16), ('hero_shrine', 590, -16, 13), ('valley_academic_house', 455, 26, 24)]:
    p = main[i] + nor[i] * side
    if free(p[0], p[1], w * .4, road=6, tall=False, maxslope=.6): plant('roadside', asset, float(p[0]), float(p[1]), w * grow(p[1]), 1.35, rot=0)
# 2 the Foetor's fields: crop rows in rectangular plots on the terrace edge and the slope below it
fx, fz, fh, fs = HUBS['farm']; fr = BASE * fs
for px_, pz_, nx, nz in [(-fr * .95, -fr * .1, 3, 3), (fr * .82, -fr * .2, 2, 3), (-fr * .35, -fr * .78, 4, 2), (fr * .45, -fr * .8, 3, 2), (-fr * 1.1, fr * .5, 3, 2)]:
    for a_ in range(nx):
        for b_ in range(nz):
            x = fx + px_ + (a_ - (nx - 1) / 2) * 15.5; z = fz + pz_ + (b_ - (nz - 1) / 2) * 13.5
            if free(x, z, 3, tall=False, road=7, hub_min=.98): plant('field', 'farm_skirt_low_8_crops' if (a_ + b_) % 2 else 'farm_tile_33_crops', x, z, 17, 1.45 * (0.9 + .1 * ((a_ * 7 + b_ * 3) % 3)), rot=0 if b_ % 2 else 180)
for x, z, a_ in [(fx - fr * 1.25, fz - fr * .45, 'farm_cottage'), (fx + fr * 1.05, fz + fr * .35, 'farm_tile_03'), (fx - fr * .1, fz - fr * 1.12, 'farm_shed')]:
    if free(x, z, 6, tall=False): plant('farmstead', a_, x, z, 13, 1.5, rot=0)
# 3 the Shroud's shore: piers, boats and wrecks only where the land meets the water, mooring posts just offshore
shore_pts = [(float(GX[j, i]), float(GZ[j, i])) for j in range(0, HT.shape[0], 6) for i in range(0, HT.shape[1], 6) if BAY[j, i] > .02 and WATER + .3 < HT[j, i] < WATER + 2.2]
off_pts = [(float(GX[j, i]), float(GZ[j, i])) for j in range(0, HT.shape[0], 6) for i in range(0, HT.shape[1], 6) if BAY[j, i] > .02 and WATER - 2.2 < HT[j, i] < WATER - .5]
SHORE = ['coast_tile_09', 'coast_skirt_low_04', 'coast_tile_15', 'coast_skirt_low_07', 'coast_tile_07', 'coast_skirt_low_08']
for n_, k in enumerate(rng.permutation(len(shore_pts))):
    if count.get('shore', 0) >= 11: break
    x, z = shore_pts[k]
    if z > 280 or not all(math.hypot(x - a, z - b) > 20 + c for a, b, c in placed) or road_d(x, z) < 8 or hubd('coast', x, z) < 1.0: continue
    plant('shore', SHORE[count.get('shore', 0) % len(SHORE)], x, z, 16 * grow(z), 1.5)
for k in rng.permutation(len(off_pts))[:60]:
    if count.get('posts', 0) >= 9: break
    x, z = off_pts[k]
    if z < 280 and all(math.hypot(x - a, z - b) > 14 + c for a, b, c in placed): plant('posts', 'coast_skirt_low_02' if count.get('posts', 0) % 2 else 'coast_skirt_low_03', x, z, 11 * grow(z), 1.5)
# 4 the Tangle's wood: a ring of trees behind and beside the lair, thorns in front, and a dark fringe along the near edge of the map
tx_, tz_, th_, ts_ = HUBS['forest']; tr = BASE * ts_
for ang in np.linspace(-.25, 3.4, 13):
    x = tx_ + math.cos(ang) * tr * .98 + float(rng.uniform(-5, 5)); z = tz_ + math.sin(ang) * tr * .68 + float(rng.uniform(-4, 4))
    if free(x, z, 7, tall=False, road=9, hub_min=.9): plant('wood', WOOD[int(rng.integers(len(WOOD)))], x, z, float(rng.uniform(24, 32)), 2.1)
for x in np.arange(-190, 200, 23):
    z = float(rng.uniform(-16, 6)) + abs(x) * .16
    if abs(x - 8) > 22 and free(float(x), z, 6, tall=abs(x - tx_) < tr * .5, road=9): plant('fringe', (WOOD + THORN)[int(rng.integers(len(WOOD) + len(THORN)))], float(x), z, float(rng.uniform(20, 28)), 1.7)
for _ in range(60):
    if count.get('thorn', 0) >= 10: break
    x = tx_ + float(rng.uniform(-1.6, 1.6)) * tr * .66; z = tz_ - float(rng.uniform(.55, 1.2)) * tr * .5
    if free(x, z, 5, tall=False, road=7, hub_min=1.0): plant('thorn', THORN[int(rng.integers(len(THORN)))], x, z, float(rng.uniform(11, 15)), 2.0)
# 5 the Sprawl's outskirts: ruins and burning rubble on the bluff's shoulders and along the road up to it, scorched trees beyond
cx_, cz_, ch_, cs_ = HUBS['city']; cr = BASE * cs_
RUIN = ['city_ruins_01', 'city_ruins_02', 'city_ruins_03', 'city_ruins_04', 'city_ruins_05', 'city_building_destroyed_01']
for _ in range(120):                                       # scattered in loose clumps on the downhill side, thinning with distance
    if count.get('ruin', 0) >= 11: break
    ang = float(rng.uniform(3.3, 6.1)); r_ = 1.0 + abs(float(rng.normal(0, .28))); x = cx_ + math.cos(ang) * cr * .72 * r_; z = cz_ + math.sin(ang) * cr * .56 * r_
    if free(x, z, 4, tall=False, road=6, hub_min=.97, maxslope=.6): plant('ruin', RUIN[int(rng.integers(len(RUIN)))], x, z, float(rng.uniform(9, 17)), 1.4)
# 6 groves: trees stand in stands of one or two kinds; living autumn wood low in the valley, scorched and dead wood toward the Sprawl and the Mountain
groves = 0
for _ in range(4000):
    if groves >= (10 if QUICK else 46): break
    z = float(-10 + 640 * rng.random() ** 1.15); half = 140 + .6 * (z + 128); x = float(rng.uniform(-half, half) + 6)
    if not free(x, z, 14, road=14, maxslope=.32, hub_min=1.18): continue
    j = int((z - Z0) / STEP); i = int((x - X0) / STEP); k = nearest(x, z); burnt = (k == 'city' and hubd(k, x, z) < 2.0) or z > 455 or (k == 'mountain' and hubd(k, x, z) < 1.25)
    if k == 'coast' and hubd(k, x, z) < 1.7: continue                                                              # the salt flats stay bare
    kinds = DEAD if burnt else (WOOD if (k == 'forest' and hubd(k, x, z) < 2.0) else list(rng.choice(LIVE, 2)) + ([BUSH[int(rng.integers(2))]] if rng.random() < .5 else []))
    n = int(rng.integers(3, 7)) + (2 if SIDE[j, i] > .4 else 0); g = grow(z); made = 0
    for _t in range(n * 6):
        if made >= n: break
        a = float(rng.uniform(0, 6.283)); r_ = float(rng.uniform(0, 1)) ** .6 * 26 * g; px_, pz_ = x + math.cos(a) * r_, z + math.sin(a) * r_ * .7
        if free(px_, pz_, 5 * g, road=11, maxslope=.36, hub_min=1.12):
            plant('dead' if burnt else 'grove', kinds[int(rng.integers(len(kinds)))], px_, pz_, float(rng.uniform(16, 23)) * g, (1.5 if burnt else 1.25 if kinds is not WOOD else 2.1) * float(rng.uniform(.88, 1.1)) * (0.82 if SIDE[j, i] > .5 else 1)); made += 1
    groves += made > 0
print('planted', count, sum(count.values()), flush=True)
# ---- cameras and renders ---------------------------------------------------------------------------------------
views = {'overview': CAM0}
for tag, (px, py, wpx, pitch) in {'forest': (1030, 660, 920, 9), 'farm': (1030, 650, 920, 9), 'coast': (1030, 650, 920, 9), 'city': (1030, 650, 920, 8), 'mountain': (1030, 760, 1150, -7)}.items():
    hx, hz, hh, s = HUBS[tag]; z = K * BASE * s / wpx; views[tag] = (tuple(np.array([hx, hz, hh]) - ray(px, py, pitch) * z), pitch)
dm = bpy.data.materials.new('depth'); dm.use_nodes = True; nt = dm.node_tree; nt.nodes.clear(); o = nt.nodes.new('ShaderNodeOutputMaterial'); em = nt.nodes.new('ShaderNodeEmission')
cd = nt.nodes.new('ShaderNodeCameraData'); dv = nt.nodes.new('ShaderNodeMath'); dv.operation = 'DIVIDE'; dv.inputs[1].default_value = DMAX
nt.links.new(cd.outputs['View Distance'], dv.inputs[0]); nt.links.new(dv.outputs[0], em.inputs['Color']); nt.links.new(em.outputs[0], o.inputs[0])
def show(tags):
    for ob in bpy.data.objects:
        if ob.type == 'MESH': ob.hide_render = ob.get('tag') not in tags

def terrain_plates(directory, regions, loc, pitch):
    """Keep artist scenery intact while exposing the connected ground beneath it."""
    show(['terrain'])
    L.render(directory / 'terrain-only.png', samples=args.samples, transparent=True, bg=AMBIENT)
    bpy.context.view_layer.material_override = dm
    sc.view_settings.view_transform = 'Raw'; sc.render.image_settings.color_mode = 'BW'; sc.render.image_settings.color_depth = '16'
    sc.cycles.samples = 1; sc.render.filepath = str(directory / 'terrain-depth.png'); bpy.ops.render.render(write_still=True)
    bpy.context.view_layer.material_override = None; sc.render.image_settings.color_depth = '8'
    hold = bpy.data.materials.new('terrain-holdout'); hold.use_nodes = True
    nt = hold.node_tree; nt.nodes.clear(); output = nt.nodes.new('ShaderNodeOutputMaterial'); node = nt.nodes.new('ShaderNodeHoldout')
    nt.links.new(node.outputs[0], output.inputs[0])
    saved = []
    try:
        for ob in bpy.data.objects:
            if ob.type == 'MESH' and ob.get('tag') == 'terrain':
                saved.append((ob.data, list(ob.data.materials))); ob.data.materials.clear(); ob.data.materials.append(hold)
        show(['terrain', 'world'] + regions)
        L.render(directory / 'native-scenery-holdout.png', samples=args.samples, transparent=True, bg=AMBIENT)
    finally:
        for mesh, materials in saved:
            mesh.materials.clear()
            for material in materials: mesh.materials.append(material)
    scale = sc.render.resolution_x / W
    roads = Image.new('L', (sc.render.resolution_x, sc.render.resolution_y)); draw = ImageDraw.Draw(roads)
    for line in ROADS:
        for x, z in line:
            px, py, depth = project((x, z, hgt(x, z)), loc, pitch)
            if depth <= 1: continue
            radius = max(2, K * 8 / depth) * scale; cx, cy = px * scale, py * scale
            draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=255)
    roads.save(directory / 'roads-mask.png')
meta = {'screen': [W, H], 'fov': FOV, 'dmax': DMAX, 'hubs': {k: list(v) for k, v in HUBS.items()}, 'views': {}}
if ONLY and (R / 'map.json').exists(): meta['views'] = json.loads((R / 'map.json').read_text())['views']
sc = bpy.context.scene
SS = args.scale  # Keep 4K source detail for the single-image camera zoom.
for vname, (loc, pitch) in views.items():
    if ONLY and vname not in ONLY: continue
    t = np.array(loc) + basis(pitch)[0] * 100; c = L.camera(tuple(loc), tuple(t), fov=FOV, res=(int(W * SS), int(H * SS)) if not QUICK else (W // 2, H // 2)); c.data.clip_start = 1; c.data.clip_end = 20000
    d = R / vname; d.mkdir(exist_ok=True); regs = ['city', 'farm', 'coast', 'forest'] if vname == 'overview' else [vname] if vname != 'mountain' else []
    show(['range']); L.render(d / 'range.png', samples=4, transparent=True)
    show(['mountain']); L.render(d / 'mountain.png', samples=4, transparent=True)
    show(['terrain', 'world'] + regs); L.render(d / 'beauty.png', samples=4 if QUICK else args.samples, transparent=True, bg=AMBIENT)
    bpy.context.view_layer.material_override = dm; sc.view_settings.view_transform = 'Raw'; sc.render.image_settings.color_mode = 'BW'; sc.render.image_settings.color_depth = '16'
    sc.cycles.samples = 1; sc.render.image_settings.color_mode = 'BW'; sc.render.filepath = str(d / 'depth.png'); bpy.ops.render.render(write_still=True)
    bpy.context.view_layer.material_override = None; sc.render.image_settings.color_depth = '8'
    if args.terrain_plates: terrain_plates(d, regs, loc, pitch)
    info = {}
    for k, (hx, hz, hh, s) in HUBS.items():
        fx, fy, z = project((hx, hz, hh), loc, pitch); info[k] = dict(hub=[round(fx, 1), round(fy, 1)], pix=round(K / z * s, 4) if z > 0 else None, depth=round(z, 1))
    meta['views'][vname] = dict(pitch=pitch, camera=[round(float(v), 3) for v in loc], regions=regs, hubs=info)
    print('view', vname, flush=True)
(R / 'map.json').write_text(json.dumps(meta, indent=1))
