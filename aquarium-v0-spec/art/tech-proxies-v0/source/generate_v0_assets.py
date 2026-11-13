#!/usr/bin/env python3
"""Generate original procedural technical-proxy 3D samples for a 60x30x36 cm tank.

These are NOT real species reconstructions and NOT final art.
Coordinate system: meters, Y-up, +Z forward, right-handed (glTF).
Tank: width X=0.60, height Y=0.36, depth Z=0.30; origin at inner-floor center.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC = Path(__file__).resolve().parent
sys.path.insert(0, str(SRC))

from glb_writer import GLBWriter, MeshData, NodeSpec, q_from_axis_angle, q_mul, q_normalize, mat4_trs
from mesh_lib import (
    bezier_centers, box, compute_normals, compute_tangents, fbm, height_to_normal_map,
    hollow_box, icosphere, leaf_blade, merge_meshes, orm_map, plane_displaced, rot_x, rot_y,
    rot_z, skin_chain_weights, sweep_tube, tex_fish, tex_glass, tex_leaf, tex_sand, tex_stone,
    tex_water, tex_wood, transform_mesh, triangle_count,
)

OUT_GLB = ROOT / "export" / "glb"
OUT_OBJ = ROOT / "export" / "obj"
OUT_TEX = ROOT / "export" / "textures"
TANK_W, TANK_D, TANK_H = 0.60, 0.30, 0.36
GLASS_T = 0.005
SAND_H = 0.030
WATER_H = 0.330  # fill line from inner floor

EXTRAS_BASE = {
    "project": "观赏鱼布景养成游戏",
    "role": "technical_proxy",
    "not_final_art": True,
    "not_real_species": True,
    "units": "meter",
    "up": "+Y",
    "forward": "+Z",
    "handed": "right",
    "tank_outer_m": [TANK_W, TANK_H, TANK_D],
    "tank_outer_cm": [60, 36, 30],
}


def write_png(img: Image.Image, name: str) -> Path:
    path = OUT_TEX / name
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, "PNG")
    return path


def add_common_textures(w: GLBWriter):
    maps = {}
    # albedo
    maps["fish_bc"] = tex_fish()
    maps["leaf_fg_bc"] = tex_leaf(seed=5, tint=(0.30, 0.52, 0.24))
    maps["leaf_md_bc"] = tex_leaf(seed=8, tint=(0.22, 0.42, 0.20))
    maps["wood_bc"] = tex_wood()
    maps["stone_bc"] = tex_stone()
    maps["sand_bc"] = tex_sand()
    maps["glass_bc"] = tex_glass()
    maps["water_bc"] = tex_water()
    # height-derived normals / orm
    maps["wood_n"] = height_to_normal_map(fbm(512, 256, seed=7, octaves=5, scale=3), 6.0)
    maps["stone_n"] = height_to_normal_map(fbm(256, 256, seed=11, octaves=5, scale=4), 8.0)
    maps["sand_n"] = height_to_normal_map(fbm(512, 512, seed=13, octaves=5, scale=12), 3.0)
    maps["fish_n"] = height_to_normal_map(fbm(256, 128, seed=3, octaves=3, scale=6), 2.0)
    maps["leaf_n"] = height_to_normal_map(fbm(256, 256, seed=5, octaves=3, scale=5), 2.5)
    maps["wood_orm"] = orm_map(512, 256, 1.0, 0.62, 0.0, seed=7)
    maps["stone_orm"] = orm_map(256, 256, 1.0, 0.78, 0.0, seed=11)
    maps["sand_orm"] = orm_map(512, 512, 1.0, 0.88, 0.0, seed=13)
    maps["fish_orm"] = orm_map(256, 128, 1.0, 0.45, 0.02, seed=3)
    maps["leaf_orm"] = orm_map(256, 256, 1.0, 0.55, 0.0, seed=5)
    for name, img in maps.items():
        write_png(img, f"{name}.png")
        srgb = name.endswith("_bc")
        w.add_png_texture(name, img, srgb=srgb)
    # materials
    w.add_material("mat_fish", base_color=(1, 1, 1, 1), metallic=0.02, roughness=0.45,
                   base_color_tex="fish_bc", normal_tex="fish_n", orm_tex="fish_orm")
    w.add_material("mat_leaf_fg", base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.55,
                   base_color_tex="leaf_fg_bc", normal_tex="leaf_n", orm_tex="leaf_orm",
                   alpha_mode="MASK", alpha_cutoff=0.4, double_sided=True)
    w.add_material("mat_leaf_md", base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.55,
                   base_color_tex="leaf_md_bc", normal_tex="leaf_n", orm_tex="leaf_orm",
                   alpha_mode="MASK", alpha_cutoff=0.4, double_sided=True)
    w.add_material("mat_wood", base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.62,
                   base_color_tex="wood_bc", normal_tex="wood_n", orm_tex="wood_orm")
    w.add_material("mat_stone", base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.78,
                   base_color_tex="stone_bc", normal_tex="stone_n", orm_tex="stone_orm")
    w.add_material("mat_sand", base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.88,
                   base_color_tex="sand_bc", normal_tex="sand_n", orm_tex="sand_orm")
    w.add_material("mat_glass", base_color=(0.90, 0.95, 0.97, 0.16), metallic=0.0, roughness=0.08,
                   base_color_tex="glass_bc", alpha_mode="BLEND", double_sided=True,
                   transmission=0.9)
    w.add_material("mat_water", base_color=(0.35, 0.60, 0.55, 0.14), metallic=0.0, roughness=0.2,
                   base_color_tex="water_bc", alpha_mode="BLEND", double_sided=True,
                   transmission=0.7)
    w.add_material("mat_rim", base_color=(0.12, 0.13, 0.14, 1), metallic=0.1, roughness=0.5)
    return maps


def write_obj(path: Path, pos, nrm, uv, idx, name="mesh"):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w") as f:
        f.write(f"# technical proxy OBJ, meters, Y-up +Z forward\n# {name}\n")
        for p in pos:
            f.write(f"v {p[0]:.6f} {p[1]:.6f} {p[2]:.6f}\n")
        for n in nrm:
            f.write(f"vn {n[0]:.6f} {n[1]:.6f} {n[2]:.6f}\n")
        for u in uv:
            f.write(f"vt {u[0]:.6f} {u[1]:.6f}\n")
        for a, b, c in idx.reshape(-1, 3):
            a += 1; b += 1; c += 1
            f.write(f"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n")


def meshdata(name, pos, nrm, uv, idx, material, joints=None, weights=None):
    tan = compute_tangents(pos, nrm, uv, idx)
    return MeshData(name, pos.astype(np.float32), nrm.astype(np.float32), uv.astype(np.float32),
                    idx.astype(np.uint32), material, joints, weights, tan)


def make_tank():
    # outer from floor y=0 (outer bottom). Origin later shifted so inner floor center is origin.
    # We'll build with inner floor at y=0, inner x in [-0.295,0.295], z in [-0.145,0.145]
    ix0, ix1 = -TANK_W / 2 + GLASS_T, TANK_W / 2 - GLASS_T
    iz0, iz1 = -TANK_D / 2 + GLASS_T, TANK_D / 2 - GLASS_T
    iy0, iy1 = 0.0, TANK_H - GLASS_T  # open rim at outer top
    ox0, ox1 = -TANK_W / 2, TANK_W / 2
    oz0, oz1 = -TANK_D / 2, TANK_D / 2
    # External size is 60x36x30 cm including bottom glass: outer y from -t to H-t
    oy0, oy1 = -GLASS_T, TANK_H - GLASS_T
    glass = hollow_box((ox0, ox1, oy0, oy1, oz0, oz1), (ix0, ix1, iy0, iy1, iz0, iz1), skip_top=True)
    # top rim (thin rectangle frame)
    rim_t = 0.008
    rim_h = 0.006
    parts = [glass]
    # front/back rim
    parts.append(box(ox0, ox1, iy1, iy1 + rim_h, iz1, oz1 + 0.0001))
    parts.append(box(ox0, ox1, iy1, iy1 + rim_h, oz0 - 0.0001, iz0))
    parts.append(box(ix1, ox1, iy1, iy1 + rim_h, iz0, iz1))
    parts.append(box(ox0, ix0, iy1, iy1 + rim_h, iz0, iz1))
    # ignore tiny extra - actually merge glass only for glass mat, rim separate
    pos, nrm, uv, idx = glass
    # water volume (inside, from sand top to fill line) — included for wiring, not an engine-pass
    wx0, wx1, wz0, wz1 = ix0 + 0.0005, ix1 - 0.0005, iz0 + 0.0005, iz1 - 0.0005
    wpos, wn, wu, wi = box(wx0, wx1, SAND_H, WATER_H, wz0, wz1)
    return (pos, nrm, uv, idx), (wpos, wn, wu, wi), {
        "inner_x": [ix0, ix1], "inner_z": [iz0, iz1], "inner_y": [iy0, iy1],
        "outer_whd_m": [TANK_W, TANK_H, TANK_D],
    }


def make_sand():
    ix, iz = TANK_W - 2 * GLASS_T - 0.002, TANK_D - 2 * GLASS_T - 0.002

    def h(x, z):
        # gentle mounds, lower in open-right swimming area (triangle layout hint)
        nx = (x + 0.1) / 0.3
        nz = z / 0.15
        return SAND_H * 0.75 + 0.006 * np.sin(nx * 3.1) * np.cos(nz * 2.4) + 0.004 * np.sin(nx * 8 + nz * 5)

    pos, nrm, uv, idx = plane_displaced(ix, iz, 48, 28, h, y0=0.0)
    # solid fill from y=0 up to the lowest surface point so the bed meets the inner floor
    y_fill = float(max(0.002, pos[:, 1].min()))
    slab = box(-ix / 2, ix / 2, 0.0, y_fill + 0.0008, -iz / 2, iz / 2)
    pos, nrm, uv, idx = merge_meshes([(pos, nrm, uv, idx), slab])
    nrm = compute_normals(pos, idx)
    return pos, nrm, uv, idx


def make_wood():
    # Diagonal main wood, left-rear rising toward mid-front, span ~0.36 m (~60% of 60cm)
    p0 = np.array([-0.22, 0.01, -0.04])
    p1 = np.array([-0.12, 0.06, 0.00])
    p2 = np.array([0.00, 0.10, 0.02])
    p3 = np.array([0.14, 0.035, 0.06])
    n = 28
    centers = bezier_centers(p0, p1, p2, p3, n)
    # radius larger at base
    t = np.linspace(0, 1, n)
    radii = 0.022 * (1 - 0.62 * t) + 0.006
    # slight flatten
    pos, nrm, uv, idx = sweep_tube(centers, radii, segs=12, closed_caps=True, uv_tiles=(1, 2))
    # a short side branch
    b0 = centers[8]
    b3 = b0 + np.array([0.03, 0.05, -0.07])
    b1 = b0 + np.array([0.01, 0.03, -0.02])
    b2 = b0 + np.array([0.02, 0.05, -0.05])
    bc = bezier_centers(b0, b1, b2, b3, 12)
    br = np.linspace(0.010, 0.004, 12)
    p2_, n2, u2, i2 = sweep_tube(bc, br, segs=8, closed_caps=True, uv_tiles=(1, 1))
    pos, nrm, uv, idx = merge_meshes([(pos, nrm, uv, idx), (p2_, n2, u2, i2)])
    nrm = compute_normals(pos, idx)
    mn, mx = pos.min(0), pos.max(0)
    pivot = np.array([(mn[0] + mx[0]) * 0.5, mn[1], (mn[2] + mx[2]) * 0.5], dtype=np.float32)
    pos = (pos - pivot).astype(np.float32)
    pivot[1] = 0.0  # sit on inner floor; do not keep a negative buried offset
    return pos, nrm, uv, idx, pivot


def make_stones():
    specs = [
        (0.028, 2, 0.70, 21, 0.22, np.array([-0.18, 0, -0.02])),
        (0.020, 2, 0.80, 22, 0.18, np.array([-0.14, 0, 0.05])),
        (0.016, 1, 0.75, 23, 0.20, np.array([-0.10, 0, -0.06])),
        (0.018, 2, 0.78, 24, 0.16, np.array([0.04, 0, 0.07])),
        (0.014, 1, 0.72, 25, 0.20, np.array([0.10, 0, 0.04])),
    ]
    parts = []
    for r, sub, fy, seed, noise, t in specs:
        p, n, u, i = icosphere(radius=r, subdiv=sub, flatten_y=fy, seed=seed, noise=noise)
        p = p + t
        parts.append((p, n, u, i))
    pos, nrm, uv, idx = merge_meshes(parts)
    nrm = compute_normals(pos, idx)
    mn, mx = pos.min(0), pos.max(0)
    pivot = np.array([(mn[0] + mx[0]) * 0.5, mn[1], (mn[2] + mx[2]) * 0.5], dtype=np.float32)
    pos = (pos - pivot).astype(np.float32)
    pivot[1] = 0.0  # sit on inner floor; do not keep a negative buried offset
    return pos, nrm, uv, idx, pivot


def make_carpet_clump():
    """Foreground carpet proxy: many small blades, 4 sway bones (root + 3 stems)."""
    rng = np.random.default_rng(42)
    parts = []
    # record per-vertex later via merge
    blade_count = 36
    for k in range(blade_count):
        ang = 2 * np.pi * k / blade_count + rng.normal(0, 0.15)
        rad = 0.008 + 0.028 * (rng.random() ** 0.7)
        t = np.array([np.cos(ang) * rad, 0.0, np.sin(ang) * rad])
        length = 0.028 + rng.random() * 0.018  # 2.8–4.6 cm, ~8–13% of 36cm
        width = 0.0035 + rng.random() * 0.002
        p, n, u, i = leaf_blade(length=length, width=width, drop=0.002, segs_l=5, segs_w=2, bend=0.2)
        # blades mostly upright, slight lean out
        R = rot_y(ang) @ rot_x(0.15 + 0.2 * rng.random())
        p, n = transform_mesh(p, n, R=R, t=t)
        parts.append((p, n, u, i))
    pos, nrm, uv, idx = merge_meshes(parts)
    nrm = compute_normals(pos, idx)
    # joints: root at origin, then 3 tuft stems around
    joint_pos = np.array([
        [0, 0.0, 0],
        [0.012, 0.018, 0.000],
        [-0.008, 0.018, 0.010],
        [-0.004, 0.018, -0.012],
    ], dtype=np.float64)
    joints, weights = skin_chain_weights(pos, joint_pos, k=4)
    # boost height influence: taller verts lean on stem bones
    return pos, nrm, uv, idx, joint_pos, joints, weights


def make_mid_plant():
    """Midground leaf plant proxy (Anubias-like silhouette, not a species scan)."""
    rng = np.random.default_rng(9)
    parts = []
    # short rhizome
    c = np.stack([np.linspace(-0.015, 0.02, 8), np.full(8, 0.006), np.zeros(8)], axis=1)
    r = np.full(8, 0.004)
    parts.append(sweep_tube(c, r, segs=8, closed_caps=True))
    # 6 leaves
    leaf_defs = [
        (0.00, 0.15, 0.0, 0.055, 0.028),
        (0.35, 0.25, 0.01, 0.062, 0.030),
        (-0.40, 0.20, -0.008, 0.050, 0.026),
        (0.8, 0.35, 0.012, 0.048, 0.024),
        (-0.9, 0.30, -0.01, 0.058, 0.027),
        (0.15, 0.45, 0.004, 0.042, 0.022),
    ]
    for yaw, tilt, xoff, length, width in leaf_defs:
        p, n, u, i = leaf_blade(length=length, width=width, drop=0.008, segs_l=7, segs_w=4, bend=0.25)
        R = rot_y(yaw) @ rot_x(tilt)
        p, n = transform_mesh(p, n, R=R, t=np.array([xoff, 0.008, 0.0]))
        parts.append((p, n, u, i))
    pos, nrm, uv, idx = merge_meshes(parts)
    nrm = compute_normals(pos, idx)
    joint_pos = np.array([
        [0, 0.0, 0],
        [0.0, 0.02, 0.0],
        [0.01, 0.04, 0.005],
        [-0.01, 0.04, -0.004],
    ], dtype=np.float64)
    joints, weights = skin_chain_weights(pos, joint_pos, k=4)
    return pos, nrm, uv, idx, joint_pos, joints, weights


def make_fish(lod=0):
    """Tetra-like technical proxy. Length ~3.8 cm. Forward +Z, head at +Z."""
    length = 0.038
    if lod == 0:
        rings, segs = 18, 12
        fin_l, fin_w = 7, 3
    elif lod == 1:
        rings, segs = 10, 8
        fin_l, fin_w = 5, 2
    else:
        rings, segs = 6, 6
        fin_l, fin_w = 3, 1
    z = np.linspace(-0.45, 0.55, rings) * length  # tail at -z, head at +z
    # radius profile (height/width later)
    zn = (z - z.min()) / (z.max() - z.min())
    # snout at zn~1, tail peduncle zn~0
    # remap so zn=0 tail, 1 head
    # body fullest around 0.55 from tail
    profile = np.where(
        zn < 0.15,
        0.25 + 2.2 * zn,  # tail peduncle up
        np.where(
            zn < 0.75,
            0.55 + 0.15 * np.sin((zn - 0.15) / 0.6 * np.pi),
            0.58 * np.cos((zn - 0.75) / 0.25 * np.pi / 2) + 0.08,
        ),
    )
    height_r = profile * 0.0058
    width_r = profile * 0.0036
    centers = np.stack([np.zeros_like(z), np.zeros_like(z), z], axis=1)
    # build elliptical rings
    pos, nrm, uv, idx = [], [], [], []
    for i, (c, hy, hx) in enumerate(zip(centers, height_r, width_r)):
        v = i / (rings - 1)
        for j in range(segs):
            a = 2 * np.pi * j / segs
            off = np.array([np.cos(a) * hx, np.sin(a) * hy, 0.0])
            pos.append(c + off)
            n = off / (np.linalg.norm(off) + 1e-8)
            nrm.append(n)
            uv.append((j / segs, v))
    for i in range(rings - 1):
        for j in range(segs):
            a = i * segs + j
            b = i * segs + (j + 1) % segs
            c = (i + 1) * segs + j
            d = (i + 1) * segs + (j + 1) % segs
            idx.extend([a, c, d, a, d, b])
    # caps
    for end, sign in ((0, -1), (rings - 1, 1)):
        base = len(pos)
        pos.append(centers[end] + np.array([0, 0, sign * 0.0004]))
        nrm.append([0, 0, sign])
        uv.append((0.5, 0 if end == 0 else 1))
        ring = end * segs
        for j in range(segs):
            j2 = (j + 1) % segs
            if sign > 0:
                idx.extend([base, ring + j, ring + j2])
            else:
                idx.extend([base, ring + j2, ring + j])
    pos = np.array(pos, np.float64)
    nrm = np.array(nrm, np.float64)
    uv = np.array(uv, np.float32)
    idx = np.array(idx, np.uint32)

    def add_fin(origin, length, height, segs_l, segs_w, axis_up, thin_z=True):
        fp, fu = [], []
        for i in range(segs_l + 1):
            v = i / segs_l
            y = v * length
            wp = height * np.sin(np.pi * v) ** 0.7
            for j in range(segs_w + 1):
                u = j / segs_w
                x = (u - 0.5) * wp
                fp.append((x, y, 0.0))
                fu.append((u, v))
        fi = []
        cols = segs_w + 1
        for i in range(segs_l):
            for j in range(segs_w):
                a = i * cols + j
                b = a + 1
                c = a + cols
                d = c + 1
                fi.extend([a, c, d, a, d, b])
        fp = np.array(fp, np.float64)
        fn = compute_normals(fp.astype(np.float32), np.array(fi, np.uint32)).astype(np.float64)
        return fp, fn, np.array(fu, np.float32), np.array(fi, np.uint32)

    # caudal fin at tail ( -Z ), in YZ plane (vertical)
    caudal, cn, cu, ci = add_fin(None, 0.010, 0.012, fin_l, fin_w, None)
    # rotate so fin extends to -Z, spread in Y
    R = rot_x(-np.pi / 2)
    caudal, cn = caudal @ R.T, cn @ R.T
    caudal = caudal + np.array([0, 0, z.min() + 0.0005])
    # dorsal on top
    dorsal, dn, du, di = add_fin(None, 0.007, 0.006, max(3, fin_l - 2), 2, None)
    Rd = rot_x(-0.3)
    dorsal, dn = dorsal @ Rd.T, dn @ Rd.T
    dorsal = dorsal + np.array([0, height_r.max() * 0.85, 0.002])
    # pectorals
    def pectoral(side):
        p, n, u, i = add_fin(None, 0.006, 0.004, max(3, fin_l - 2), 2, None)
        R = rot_z(side * -0.9) @ rot_y(side * 0.3)
        p, n = p @ R.T, n @ R.T
        p = p + np.array([side * width_r.max() * 0.9, -0.0005, 0.006])
        return p, n, u, i

    pl = pectoral(1)
    pr = pectoral(-1)
    pos, nrm, uv, idx = merge_meshes([
        (pos.astype(np.float32), nrm.astype(np.float32), uv, idx),
        (caudal.astype(np.float32), cn.astype(np.float32), cu, ci),
        (dorsal.astype(np.float32), dn.astype(np.float32), du, di),
        (pl[0].astype(np.float32), pl[1].astype(np.float32), pl[2], pl[3]),
        (pr[0].astype(np.float32), pr[1].astype(np.float32), pr[2], pr[3]),
    ])
    nrm = compute_normals(pos, idx)
    # joints along Z, root at body center 0
    if lod == 0:
        joint_pos = np.array([
            [0, 0, 0.010],   # 0 fh_head
            [0, 0, 0.000],   # 1 fh_root / spine_01
            [0, 0, -0.006],  # 2 spine_02
            [0, 0, -0.012],  # 3 spine_03
            [0, 0, -0.018],  # 4 tail_01
            [0, 0, -0.026],  # 5 tail_02
            [0.003, 0, 0.005],  # 6 pectoral L
            [-0.003, 0, 0.005], # 7 pectoral R
        ], dtype=np.float64)
        names = ["fh_head", "fh_spine_01", "fh_spine_02", "fh_spine_03", "fh_tail_01", "fh_tail_02", "fh_pectoral_L", "fh_pectoral_R"]
        parents = [1, -1, 1, 2, 3, 4, 1, 1]  # spine_01 is skeleton root
    else:
        joint_pos = np.array([
            [0, 0, 0.006],
            [0, 0, 0.000],
            [0, 0, -0.010],
            [0, 0, -0.022],
        ], dtype=np.float64)
        names = ["fh_head", "fh_spine_01", "fh_spine_02", "fh_tail_01"]
        parents = [1, -1, 1, 2]
    joints, weights = skin_chain_weights(pos, joint_pos, k=4)
    return pos, nrm, uv, idx, joint_pos, joints, weights, names, parents, length


def chain_global_bind(joint_pos, parents):
    """Local translation = joint_pos[i] - joint_pos[parent]; rotation identity."""
    J = len(joint_pos)
    local_t = np.zeros((J, 3))
    for i in range(J):
        if parents[i] < 0:
            local_t[i] = joint_pos[i]
        else:
            local_t[i] = joint_pos[i] - joint_pos[parents[i]]
    # global = bind positions already given; IBM = inverse of bind global
    ibm = np.zeros((J, 4, 4), dtype=np.float64)
    for i in range(J):
        g = mat4_trs(joint_pos[i], (0, 0, 0, 1), (1, 1, 1))
        ibm[i] = np.linalg.inv(g)
    return local_t, ibm


def fish_anims(parents, names, fps=30):
    """Return dict name -> (times, list of (F,4) local quats per joint)."""
    J = len(names)
    name_i = {n: i for i, n in enumerate(names)}

    def base_rots(F):
        return {i: np.tile(np.array([0, 0, 0, 1.0]), (F, 1)) for i in range(J)}

    # swim_tail_loop 2.0s
    dur = 2.0
    F = int(dur * fps) + 1
    times = np.linspace(0, dur, F)
    rots = base_rots(F)
    amp = {
        "fh_head": 0.06,
        "fh_spine_01": 0.10,
        "fh_spine_02": 0.18,
        "fh_spine_03": 0.28,
        "fh_tail_01": 0.42,
        "fh_tail_02": 0.55,
    }
    phase = {
        "fh_head": 0.2,
        "fh_spine_01": 0.0,
        "fh_spine_02": 0.4,
        "fh_spine_03": 0.8,
        "fh_tail_01": 1.2,
        "fh_tail_02": 1.6,
    }
    for n, a in amp.items():
        if n not in name_i:
            continue
        i = name_i[n]
        for f, t in enumerate(times):
            ang = a * np.sin(2 * np.pi * (t / dur) + phase.get(n, 0))
            rots[i][f] = q_from_axis_angle((0, 1, 0), ang)
    # pectoral flap
    for n, side in (("fh_pectoral_L", 1), ("fh_pectoral_R", -1)):
        if n not in name_i:
            continue
        i = name_i[n]
        for f, t in enumerate(times):
            ang = 0.25 * np.sin(2 * np.pi * (t / (dur / 2))) * side
            rots[i][f] = q_from_axis_angle((0, 0, 1), ang)
    swim = (times, rots)

    # turn_left 1.5s in-place, no root translation; body yaw then recover (loopable)
    dur2 = 1.5
    F2 = int(dur2 * fps) + 1
    times2 = np.linspace(0, dur2, F2)
    rots2 = base_rots(F2)
    # envelope 0->1->0
    env = np.sin(np.pi * times2 / dur2)
    yaw_amp = {
        "fh_head": 0.25,
        "fh_spine_01": 0.45,
        "fh_spine_02": 0.35,
        "fh_spine_03": 0.40,
        "fh_tail_01": 0.55,
        "fh_tail_02": 0.70,
    }
    for n, a in yaw_amp.items():
        if n not in name_i:
            continue
        i = name_i[n]
        lag = phase.get(n, 0) * 0.15
        for f, t in enumerate(times2):
            e = np.sin(np.pi * max(0, min(1, (t - lag * 0.05) / dur2)))
            # left turn: positive Y (toward +X from +Z)
            ang = a * e
            # plus a small traveling tail wave
            ang += 0.15 * np.sin(2 * np.pi * t / 0.5 + phase.get(n, 0)) * e
            rots2[i][f] = q_from_axis_angle((0, 1, 0), ang)
    for n, side in (("fh_pectoral_L", 1), ("fh_pectoral_R", -1)):
        if n not in name_i:
            continue
        i = name_i[n]
        for f, t in enumerate(times2):
            rots2[i][f] = q_from_axis_angle((0, 0, 1), 0.2 * side * np.sin(2 * np.pi * t / 0.4))
    # Force a loopable clip: first and last keys are bind pose (no root motion).
    ident = np.array([0, 0, 0, 1.0])
    for i in range(J):
        rots2[i][0] = ident
        rots2[i][-1] = ident
    turn = (times2, rots2)
    return {"swim_tail_loop": swim, "turn_left": turn}


def plant_sway(n_joints, dur=3.0, fps=30, amp=0.12, seed=0):
    F = int(dur * fps) + 1
    times = np.linspace(0, dur, F)
    rots = {i: np.tile(np.array([0, 0, 0, 1.0]), (F, 1)) for i in range(n_joints)}
    for i in range(1, n_joints):  # root stays
        a = amp * (0.6 + 0.3 * i)
        for f, t in enumerate(times):
            ax = a * np.sin(2 * np.pi * t / dur + i * 0.4)
            az = a * 0.6 * np.sin(2 * np.pi * t / dur + 1.2 + i * 0.3)
            rots[i][f] = q_mul(q_from_axis_angle((1, 0, 0), ax), q_from_axis_angle((0, 0, 1), az))
    return times, rots


def add_skeleton_nodes(w: GLBWriter, names, parents, local_t, prefix_skip=None):
    """Create joint nodes (translations only). Returns joint node indices in joint order."""
    # create in order, then fill children
    idxs = []
    for i, name in enumerate(names):
        idxs.append(w.add_node(NodeSpec(name=name, translation=tuple(local_t[i]))))
    # children
    kids = {i: [] for i in range(len(names))}
    root_joints = []
    for i, p in enumerate(parents):
        if p < 0:
            root_joints.append(i)
        else:
            kids[p].append(i)
    for i in range(len(names)):
        w.set_node_children(idxs[i], [idxs[c] for c in kids[i]])
    return idxs, [idxs[i] for i in root_joints]


def build_skinned_asset(path, display_name, mesh: MeshData, joint_pos, names, parents,
                        clips: dict, extras, wrapper_translation=(0, 0, 0), unlit_ok=False):
    w = GLBWriter(extras=extras)
    add_common_textures(w)
    mid = w.add_mesh(mesh)
    local_t, ibm = chain_global_bind(joint_pos, parents)
    # wrapper
    # We'll add wrapper last once we know children. First add a placeholder approach:
    # Add skeleton container, joints, mesh node, then wrapper.
    skel_root_placeholder = None
    joint_idxs, joint_roots = add_skeleton_nodes(w, names, parents, local_t)
    # skeleton node? joint_roots already have parent -1 at bind translation
    skin_idx = w.add_skin(f"{display_name}_skin", joint_idxs, ibm, skeleton=joint_roots[0])
    mesh_node = w.add_node(NodeSpec(name=f"{display_name}_mesh", mesh=mid, skin=skin_idx))
    # wrapper
    wrapper = w.add_node(NodeSpec(
        name=display_name,
        translation=tuple(wrapper_translation),
        children=[joint_roots[0], mesh_node],
        extras={"role": "asset_root", "pivot": "documented_in_spec"},
    ))
    # clips
    for clip_name, (times, rots) in clips.items():
        node_rots = {joint_idxs[j]: rots[j] for j in rots}
        loop = "loop" in clip_name or clip_name.endswith("_loop") or clip_name == "turn_left"
        w.add_rotation_clip(
            clip_name, times, node_rots,
            extras={"loop": loop, "root_motion": False, "units": "seconds"},
        )
    w.save(str(path), scene_name=display_name, roots=[wrapper])
    return w, path


def build_static_asset(path, display_name, meshes: list[MeshData], extras, translation=(0, 0, 0),
                       also_obj=None):
    w = GLBWriter(extras=extras)
    add_common_textures(w)
    children = []
    for m in meshes:
        mid = w.add_mesh(m)
        children.append(w.add_node(NodeSpec(name=m.name, mesh=mid)))
    wrapper = w.add_node(NodeSpec(name=display_name, translation=tuple(translation), children=children))
    w.save(str(path), scene_name=display_name, roots=[wrapper])
    if also_obj:
        # first mesh only or merge? write each
        for m in meshes:
            write_obj(also_obj.parent / f"{m.name}.obj", m.positions, m.normals, m.uvs, m.indices, m.name)
    return w, path


def aabb(pos):
    return pos.min(axis=0), pos.max(axis=0)


def main():
    OUT_GLB.mkdir(parents=True, exist_ok=True)
    OUT_OBJ.mkdir(parents=True, exist_ok=True)
    OUT_TEX.mkdir(parents=True, exist_ok=True)
    catalog = []

    # ---- tank + water ----
    (gp, gn, gu, gi), (wp, wn, wu, wi), tank_meta = make_tank()
    write_obj(OUT_OBJ / "tk_v0_tank60_glass.obj", gp, gn, gu, gi, "tk_v0_tank60_glass")
    write_obj(OUT_OBJ / "tk_v0_water_proxy.obj", wp, wn, wu, wi, "tk_v0_water_proxy")
    build_static_asset(
        OUT_GLB / "tk_v0_tank60.glb", "tk_v0_tank60",
        [
            meshdata("tk_v0_tank60_glass", gp, gn, lo_u := gu, gi, "mat_glass"),
            meshdata("tk_v0_water_proxy", wp, wn, wu, wi, "mat_water"),
        ],
        extras={**EXTRAS_BASE, "asset": "tk_v0_tank60", "pivot": "inner_floor_center",
                "glass_thickness_m": GLASS_T, "open_top": True,
                "warning": "Glass/water use alpha+KHR_materials_transmission in glTF only. Unity URP transparency/refraction is NOT validated by this export."},
    )
    catalog.append({
        "id": "tk_v0_tank60", "kind": "tank+water_proxy", "lod": "L0",
        "tris_glass": triangle_count(gi), "tris_water": triangle_count(wi),
        "aabb_min": aabb(np.vstack([gp, wp]))[0].tolist(),
        "aabb_max": aabb(np.vstack([gp, wp]))[1].tolist(),
        "pivot": "inner_floor_center",
        "formats": ["glb", "obj"],
        "skinned": False, "animations": [],
    })

    # ---- sand ----
    sp, sn, su, si = make_sand()
    write_obj(OUT_OBJ / "sb_v0_sand.obj", sp, sn, su, si, "sb_v0_sand")
    build_static_asset(
        OUT_GLB / "sb_v0_sand.glb", "sb_v0_sand",
        [meshdata("sb_v0_sand", sp, sn, su, si, "mat_sand")],
        extras={**EXTRAS_BASE, "asset": "sb_v0_sand", "pivot": "inner_floor_center", "nominal_thickness_m": SAND_H},
    )
    catalog.append({
        "id": "sb_v0_sand", "kind": "sand", "lod": "L0",
        "tris": triangle_count(si),
        "aabb_min": aabb(sp)[0].tolist(), "aabb_max": aabb(sp)[1].tolist(),
        "pivot": "inner_floor_center", "formats": ["glb", "obj"], "skinned": False, "animations": [],
    })

    # ---- wood ----
    dp, dn, du, di, wood_pivot = make_wood()
    write_obj(OUT_OBJ / "wd_v0_driftwood.obj", dp, dn, du, di, "wd_v0_driftwood")
    build_static_asset(
        OUT_GLB / "wd_v0_driftwood.glb", "wd_v0_driftwood",
        [meshdata("wd_v0_driftwood", dp, dn, du, di, "mat_wood")],
        extras={**EXTRAS_BASE, "asset": "wd_v0_driftwood", "pivot": "bottom_center_local",
                "note": "Local-space piece. Scene file places it with translation = pre-center tank-space offset.",
                "scene_translation_m": [float(x) for x in wood_pivot]},
    )
    catalog.append({
        "id": "wd_v0_driftwood", "kind": "hardscape_wood", "lod": "L0",
        "tris": triangle_count(di),
        "aabb_min": aabb(dp)[0].tolist(), "aabb_max": aabb(dp)[1].tolist(),
        "span_m": float(np.linalg.norm(aabb(dp)[1] - aabb(dp)[0])),
        "pivot": "bottom_center_local", "formats": ["glb", "obj"], "skinned": False, "animations": [],
        "scene_translation_m": [float(x) for x in wood_pivot],
    })

    # ---- stones ----
    rp, rn, ru, ri, stone_pivot = make_stones()
    write_obj(OUT_OBJ / "rk_v0_stone_set.obj", rp, rn, ru, ri, "rk_v0_stone_set")
    build_static_asset(
        OUT_GLB / "rk_v0_stone_set.glb", "rk_v0_stone_set",
        [meshdata("rk_v0_stone_set", rp, rn, ru, ri, "mat_stone")],
        extras={**EXTRAS_BASE, "asset": "rk_v0_stone_set", "pivot": "bottom_center_local", "count": 5,
                "scene_translation_m": [float(x) for x in stone_pivot]},
    )
    catalog.append({
        "id": "rk_v0_stone_set", "kind": "hardscape_stone", "lod": "L0",
        "tris": triangle_count(ri),
        "aabb_min": aabb(rp)[0].tolist(), "aabb_max": aabb(rp)[1].tolist(),
        "pivot": "bottom_center_local", "formats": ["glb", "obj"], "skinned": False, "animations": [],
        "scene_translation_m": [float(x) for x in stone_pivot],
    })

    # ---- plants ----
    def plant_asset(file_id, kind, maker, mat, translation, sway_amp):
        p, n, u, i, jpos, jnt, wgt = maker()
        names = [f"{file_id}_root", f"{file_id}_stem_01", f"{file_id}_stem_02", f"{file_id}_stem_03"]
        parents = [-1, 0, 0, 0]
        # stem locals vs root: jpos already absolute; parents 1..3 -> 0
        clips = {"sway_loop": plant_sway(len(names), dur=3.0, amp=sway_amp)}
        md = meshdata(file_id, p, n, u, i, mat, jnt, wgt)
        # OBJ bind pose only (no skin)
        write_obj(OUT_OBJ / f"{file_id}.obj", p, n, u, i, file_id)
        build_skinned_asset(
            OUT_GLB / f"{file_id}.glb", file_id, md, jpos, names, parents, clips,
            extras={**EXTRAS_BASE, "asset": file_id, "pivot": "base_on_substrate",
                    "animation": ["sway_loop"], "root_motion": False},
            wrapper_translation=(0, 0, 0),
        )
        mn, mx = aabb(p)
        catalog.append({
            "id": file_id, "kind": kind, "lod": "L0",
            "tris": triangle_count(i), "bones": names,
            "aabb_min": mn.tolist(), "aabb_max": mx.tolist(),
            "height_m": float((p[:, 1] + translation[1]).max()),
            "pivot": "base_on_substrate", "formats": ["glb", "obj(bind_pose)"],
            "skinned": True, "animations": ["sway_loop"], "root_motion": False,
            "placement": list(translation),
        })
        return p, n, u, i, jpos, jnt, wgt, names, parents, translation

    plant_fg = plant_asset("pl_fg_v0_carpet_proxy", "plant_foreground_proxy",
                           make_carpet_clump, "mat_leaf_fg",
                           translation=(-0.16, SAND_H * 0.7, 0.07), sway_amp=0.10)
    plant_md = plant_asset("pl_md_v0_leaf_proxy", "plant_midground_proxy",
                           make_mid_plant, "mat_leaf_md",
                           translation=(-0.12, SAND_H * 0.7, -0.02), sway_amp=0.08)

    # ---- fish LODs ----
    fish_pack = {}
    for lod, suffix, budget in ((0, "L0", 2500), (1, "L1", 800), (2, "L2", 300)):
        p, n, u, i, jpos, jnt, wgt, names, parents, length = make_fish(lod=lod)
        md = meshdata(f"fh_v0_tetra_proxy_{suffix}", p, n, u, i, "mat_fish", jnt, wgt)
        write_obj(OUT_OBJ / f"fh_v0_tetra_proxy_{suffix}.obj", p, n, u, i, f"fh_v0_tetra_proxy_{suffix}")
        clips = fish_anims(parents, names)
        fid = f"fh_v0_tetra_proxy_{suffix}"
        # place later in scene; asset-local pivot at body center, y at mid body (not floor)
        build_skinned_asset(
            OUT_GLB / f"{fid}.glb", fid, md, jpos, names, parents, clips,
            extras={**EXTRAS_BASE, "asset": fid, "pivot": "body_center",
                    "forward": "+Z", "length_m": length,
                    "animations": ["swim_tail_loop", "turn_left"],
                    "root_motion": False,
                    "species_claim": "none_technical_proxy_only"},
        )
        mn, mx = aabb(p)
        catalog.append({
            "id": fid, "kind": "fish_proxy", "lod": suffix,
            "tris": triangle_count(i), "bones": names,
            "length_m": float(mx[2] - mn[2]),
            "aabb_min": mn.tolist(), "aabb_max": mx.tolist(),
            "pivot": "body_center", "forward": "+Z",
            "formats": ["glb", "obj(bind_pose)"],
            "skinned": True, "animations": ["swim_tail_loop", "turn_left"],
            "root_motion": False, "suggested_budget_tris": budget,
        })
        fish_pack[lod] = (p, n, u, i, jpos, jnt, wgt, names, parents, length)

    # ---- assembled scene (triangle-proxy layout, L0 only) ----
    w = GLBWriter(extras={**EXTRAS_BASE, "asset": "scn_v0_tank60_triangle_proxy",
                          "layout": "triangle-ish technical layout, not a template signoff",
                          "contains": ["tank", "water_proxy", "sand", "wood", "stones", "carpet", "midleaf", "one_fish"]})
    add_common_textures(w)
    # add all static meshes
    node_roots = []

    def add_static(name, pos, nrm, uv, idx, mat, t=(0, 0, 0)):
        mid = w.add_mesh(meshdata(name, pos, nrm, uv, idx, mat))
        nidx = w.add_node(NodeSpec(name=name, mesh=mid, translation=tuple(t)))
        return nidx

    node_roots.append(add_static("tk_v0_tank60_glass", gp, gn, gu, gi, "mat_glass"))
    node_roots.append(add_static("tk_v0_water_proxy", wp, wn, wu, wi, "mat_water"))
    node_roots.append(add_static("sb_v0_sand", sp, sn, su, si, "mat_sand"))
    node_roots.append(add_static("wd_v0_driftwood", dp, dn, du, di, "mat_wood", t=tuple(float(x) for x in wood_pivot)))
    node_roots.append(add_static("rk_v0_stone_set", rp, rn, ru, ri, "mat_stone", t=tuple(float(x) for x in stone_pivot)))

    def add_skinned(file_id, pos, nrm, uv, idx, mat, jpos, jnt, wgt, names, parents, translation, clips):
        mid = w.add_mesh(meshdata(file_id, pos, nrm, uv, idx, mat, jnt, wgt))
        local_t, ibm = chain_global_bind(jpos, parents)
        j_idxs, j_roots = add_skeleton_nodes(w, names, parents, local_t)
        skin_idx = w.add_skin(f"{file_id}_skin", j_idxs, ibm, skeleton=j_roots[0])
        mesh_node = w.add_node(NodeSpec(name=f"{file_id}_mesh", mesh=mid, skin=skin_idx))
        wrapper = w.add_node(NodeSpec(name=file_id, translation=tuple(translation), children=[j_roots[0], mesh_node]))
        for clip_name, (times, rots) in clips.items():
            node_rots = {j_idxs[j]: rots[j] for j in rots}
            w.add_rotation_clip(clip_name, times, node_rots, extras={"loop": True, "root_motion": False})
        return wrapper

    p, n, u, i, jpos, jnt, wgt, names, parents, trans = plant_fg[0], plant_fg[1], plant_fg[2], plant_fg[3], plant_fg[4], plant_fg[5], plant_fg[6], plant_fg[7], plant_fg[8], plant_fg[9]
    node_roots.append(add_skinned("pl_fg_v0_carpet_proxy", p, n, u, i, "mat_leaf_fg", jpos, jnt, wgt, names, parents, trans,
                                  {"sway_loop": plant_sway(len(names), amp=0.10)}))
    p, n, u, i, jpos, jnt, wgt, names, parents, trans = plant_md[0], plant_md[1], plant_md[2], plant_md[3], plant_md[4], plant_md[5], plant_md[6], plant_md[7], plant_md[8], plant_md[9]
    node_roots.append(add_skinned("pl_md_v0_leaf_proxy", p, n, u, i, "mat_leaf_md", jpos, jnt, wgt, names, parents, trans,
                                  {"sway_loop": plant_sway(len(names), amp=0.08, seed=2)}))
    p, n, u, i, jpos, jnt, wgt, names, parents, length = fish_pack[0]
    # fish in open water, mid-right, slightly above sand, facing +X (left-to-right from front? 
    # front camera looks -Z from +Z: +X is right. Fish forward +Z would face camera.
    # Rotate wrapper +90 deg Y so forward +Z becomes +X (swims to the right).
    mid = w.add_mesh(meshdata("fh_v0_tetra_proxy_L0", p, n, u, i, "mat_fish", jnt, wgt))
    local_t, ibm = chain_global_bind(jpos, parents)
    j_idxs, j_roots = add_skeleton_nodes(w, names, parents, local_t)
    skin_idx = w.add_skin("fh_v0_tetra_proxy_L0_skin", j_idxs, ibm, skeleton=j_roots[0])
    mesh_node = w.add_node(NodeSpec(name="fh_v0_tetra_proxy_L0_mesh", mesh=mid, skin=skin_idx))
    q_yaw = q_from_axis_angle((0, 1, 0), np.pi / 2)  # +Z -> +X
    fish_wrapper = w.add_node(NodeSpec(
        name="fh_v0_tetra_proxy_L0",
        translation=(0.08, SAND_H + 0.08, 0.02),
        rotation=tuple(q_yaw),
        children=[j_roots[0], mesh_node],
    ))
    clips = fish_anims(parents, names)
    for clip_name, (times, rots) in clips.items():
        node_rots = {j_idxs[j]: rots[j] for j in rots}
        w.add_rotation_clip(clip_name, times, node_rots, extras={"loop": True, "root_motion": False})
    node_roots.append(fish_wrapper)

    scene_path = OUT_GLB / "scn_v0_tank60_triangle_proxy.glb"
    w.save(str(scene_path), scene_name="scn_v0_tank60_triangle_proxy", roots=node_roots)
    catalog.append({
        "id": "scn_v0_tank60_triangle_proxy", "kind": "assembled_scene", "lod": "L0",
        "formats": ["glb"], "skinned": True,
        "animations": ["swim_tail_loop", "turn_left", "sway_loop"],
        "note": "One file containing tank, water proxy, sand, wood, stones, two plants, one fish. Clips share names; Unity should play per-renderer or split prefabs.",
    })

    (ROOT / "validation").mkdir(exist_ok=True)
    (ROOT / "validation" / "generation_catalog.json").write_text(json.dumps(catalog, indent=2))
    print("Generated:")
    for p in sorted(OUT_GLB.glob("*.glb")):
        print(f"  {p.name}  {p.stat().st_size} bytes")
    return catalog


if __name__ == "__main__":
    main()
