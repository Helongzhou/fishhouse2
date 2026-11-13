#!/usr/bin/env python3
"""v0.2 minimal plant proxies: Anubias-like and Java-fern-like silhouettes.

Technical proxies only. Not species reconstruction, not final art, not a new first-batch.
Does not write into /workspace/aquarium-art-v0/.
Units: meters. Y-up, +Z forward, right-handed (glTF).
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
    bezier_centers, compute_normals, compute_tangents, fbm, height_to_normal_map,
    merge_meshes, orm_map, rot_x, rot_y, rot_z, sweep_tube, transform_mesh, triangle_count,
)

OUT_GLB = ROOT / "export" / "glb"
OUT_OBJ = ROOT / "export" / "obj"
OUT_TEX = ROOT / "export" / "textures"

EXTRAS_BASE = {
    "project": "观赏鱼布景养成游戏",
    "role": "technical_proxy",
    "not_final_art": True,
    "not_real_species": True,
    "species_claim": "none_technical_proxy_only",
    "units": "meter",
    "up": "+Y",
    "forward": "+Z",
    "handed": "right",
    "tank_outer_cm": [60, 36, 30],
    "source": "original_procedural",
    "license": "project-original_procedural_no_exclusive_rights_claimed",
    "batch": "aquarium-art-v0.2",
}


def write_png(img: Image.Image, name: str) -> Path:
    path = OUT_TEX / name
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, "PNG")
    return path


def meshdata(name, pos, nrm, uv, idx, material, joints=None, weights=None):
    tan = compute_tangents(pos, nrm, uv, idx)
    return MeshData(name, pos.astype(np.float32), nrm.astype(np.float32), uv.astype(np.float32),
                    idx.astype(np.uint32), material, joints, weights, tan)


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


def chain_global_bind(joint_pos, parents):
    J = len(joint_pos)
    local_t = np.zeros((J, 3))
    for i in range(J):
        local_t[i] = joint_pos[i] if parents[i] < 0 else joint_pos[i] - joint_pos[parents[i]]
    ibm = np.zeros((J, 4, 4), dtype=np.float64)
    for i in range(J):
        ibm[i] = np.linalg.inv(mat4_trs(joint_pos[i], (0, 0, 0, 1), (1, 1, 1)))
    return local_t, ibm


def add_skeleton_nodes(w: GLBWriter, names, parents, local_t):
    idxs = []
    for i, name in enumerate(names):
        idxs.append(w.add_node(NodeSpec(name=name, translation=tuple(local_t[i]))))
    kids = {i: [] for i in range(len(names))}
    roots = []
    for i, p in enumerate(parents):
        if p < 0:
            roots.append(i)
        else:
            kids[p].append(i)
    for i in range(len(names)):
        w.set_node_children(idxs[i], [idxs[c] for c in kids[i]])
    return idxs, [idxs[i] for i in roots]


def leaf_profiled(length, width, segs_l, segs_w, profile, cup=0.003, tip_round=True):
    """Leaf in local space: origin at blade base, midrib +Y, width along X, slight cup +Z."""
    pos, uv = [], []
    for i in range(segs_l + 1):
        v = i / segs_l
        wp = float(profile(v))
        y = v * length
        z = -cup * (4 * v * (1 - v))
        for j in range(segs_w + 1):
            u = j / segs_w
            x = (u - 0.5) * 2 * (width * 0.5) * wp
            pos.append((x, y, z))
            uv.append((u, v))
    idx = []
    cols = segs_w + 1
    for i in range(segs_l):
        for j in range(segs_w):
            a = i * cols + j
            b = a + 1
            c = a + cols
            d = c + 1
            idx.extend([a, c, d, a, d, b])
    pos = np.array(pos, np.float32)
    nrm = compute_normals(pos, np.array(idx, np.uint32))
    return pos, nrm, np.array(uv, np.float32), np.array(idx, np.uint32)


def ovate_profile(v):
    # wider in lower-middle, rounded tip; almost 0 at base (petiole attach)
    # beta-like
    w = (v ** 0.55) * ((1 - v) ** 0.42)
    # normalize so max ~1
    # max of v^0.55*(1-v)^0.42 occurs near v~0.57
    return float(np.clip(w / 0.36, 0, 1.05))


def lanceolate_profile(v):
    # narrow, widest ~0.22-0.30, long taper to a point
    w = (v ** 0.32) * ((1 - v) ** 1.35)
    return float(np.clip(w / 0.22, 0, 1.05))


def tex_leaf_shaped(w, h, tint, seed, profile, name_hint):
    n = fbm(w, h, seed=seed, scale=5)
    xs = np.linspace(-1, 1, w)[None, :]
    ys = np.linspace(0, 1, h)[:, None]
    midrib = np.exp(-(xs / 0.05) ** 2) * 0.22
    vein = 0.06 * np.sin(ys * (16 if name_hint == "anubias" else 22) + xs * 4)
    g = np.clip(np.array(tint)[None, None, :] * (0.78 + 0.35 * n[..., None]) + midrib[..., None] * 0.12, 0, 1)
    g[..., 1] = np.clip(g[..., 1] + vein * 0.04, 0, 1)
    # alpha from profile (horizontal half-width vs x)
    alpha = np.zeros((h, w), dtype=np.float64)
    for i in range(h):
        v = i / max(h - 1, 1)
        half = profile(v)
        # slight edge irregularity
        edge = half * (0.96 + 0.06 * n[i])
        if name_hint == "java":
            edge = edge * (0.97 + 0.05 * np.sin(v * 28))
        xnorm = np.abs(np.linspace(-1, 1, w))
        a = np.clip((edge - xnorm) / 0.08, 0, 1)
        # soften tip / base
        a *= np.clip(v / 0.04, 0, 1) * np.clip((1 - v) / 0.03, 0, 1)
        alpha[i] = a
    rgba = np.dstack([g, np.clip(alpha, 0, 1)]) * 255
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def tex_rhizome(w=256, h=128, seed=21, tint=(0.38, 0.28, 0.16)):
    n = fbm(w, h, seed=seed, octaves=5, scale=6)
    col = np.array(tint)[None, None, :] * (0.7 + 0.5 * n[..., None])
    col = np.clip(col, 0, 1)
    rgba = np.dstack([col, np.ones((h, w))]) * 255
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def add_plant_materials(w: GLBWriter, kind: str):
    if kind == "anubias":
        bc = tex_leaf_shaped(512, 512, (0.22, 0.40, 0.18), 31, ovate_profile, "anubias")
        nrm = height_to_normal_map(fbm(512, 512, seed=31, octaves=4, scale=5), 3.0)
        orm = orm_map(512, 512, 1.0, 0.52, 0.0, seed=31)
        rz = tex_rhizome(256, 128, seed=41, tint=(0.34, 0.26, 0.15))
        rz_n = height_to_normal_map(fbm(256, 128, seed=41, octaves=4, scale=6), 5.0)
        rz_orm = orm_map(256, 128, 1.0, 0.70, 0.0, seed=41)
        write_png(bc, "anubias_leaf_bc.png"); write_png(nrm, "anubias_leaf_n.png"); write_png(orm, "anubias_leaf_orm.png")
        write_png(rz, "anubias_rhizome_bc.png"); write_png(rz_n, "anubias_rhizome_n.png"); write_png(rz_orm, "anubias_rhizome_orm.png")
        w.add_png_texture("anubias_leaf_bc", bc, srgb=True)
        w.add_png_texture("anubias_leaf_n", nrm, srgb=False)
        w.add_png_texture("anubias_leaf_orm", orm, srgb=False)
        w.add_png_texture("anubias_rhizome_bc", rz, srgb=True)
        w.add_png_texture("anubias_rhizome_n", rz_n, srgb=False)
        w.add_png_texture("anubias_rhizome_orm", rz_orm, srgb=False)
        w.add_material("mat_anubias_leaf", base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.52,
                       base_color_tex="anubias_leaf_bc", normal_tex="anubias_leaf_n", orm_tex="anubias_leaf_orm",
                       alpha_mode="MASK", alpha_cutoff=0.4, double_sided=True)
        w.add_material("mat_anubias_rhizome", base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.70,
                       base_color_tex="anubias_rhizome_bc", normal_tex="anubias_rhizome_n", orm_tex="anubias_rhizome_orm")
    else:
        bc = tex_leaf_shaped(512, 512, (0.28, 0.46, 0.20), 17, lanceolate_profile, "java")
        nrm = height_to_normal_map(fbm(512, 512, seed=17, octaves=4, scale=6), 2.8)
        orm = orm_map(512, 512, 1.0, 0.55, 0.0, seed=17)
        rz = tex_rhizome(256, 128, seed=27, tint=(0.30, 0.22, 0.13))
        rz_n = height_to_normal_map(fbm(256, 128, seed=27, octaves=4, scale=6), 5.0)
        rz_orm = orm_map(256, 128, 1.0, 0.72, 0.0, seed=27)
        write_png(bc, "java_fern_leaf_bc.png"); write_png(nrm, "java_fern_leaf_n.png"); write_png(orm, "java_fern_leaf_orm.png")
        write_png(rz, "java_fern_rhizome_bc.png"); write_png(rz_n, "java_fern_rhizome_n.png"); write_png(rz_orm, "java_fern_rhizome_orm.png")
        w.add_png_texture("java_fern_leaf_bc", bc, srgb=True)
        w.add_png_texture("java_fern_leaf_n", nrm, srgb=False)
        w.add_png_texture("java_fern_leaf_orm", orm, srgb=False)
        w.add_png_texture("java_fern_rhizome_bc", rz, srgb=True)
        w.add_png_texture("java_fern_rhizome_n", rz_n, srgb=False)
        w.add_png_texture("java_fern_rhizome_orm", rz_orm, srgb=False)
        w.add_material("mat_java_fern_leaf", base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.55,
                       base_color_tex="java_fern_leaf_bc", normal_tex="java_fern_leaf_n", orm_tex="java_fern_leaf_orm",
                       alpha_mode="MASK", alpha_cutoff=0.4, double_sided=True)
        w.add_material("mat_java_fern_rhizome", base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.72,
                       base_color_tex="java_fern_rhizome_bc", normal_tex="java_fern_rhizome_n", orm_tex="java_fern_rhizome_orm")


def skin_by_nearest_and_chain(positions, joint_pos, parents, primary, k=4):
    """primary: per-vertex preferred joint index (or -1 to use distance)."""
    J = len(joint_pos)
    W = np.zeros((len(positions), J), dtype=np.float64)
    for vi, p in enumerate(positions):
        if primary[vi] >= 0:
            j = int(primary[vi])
            W[vi, j] += 1.0
            # also a little to parent and first child-like along chain (parent)
            if parents[j] >= 0:
                W[vi, parents[j]] += 0.25
            # children of j
            for c, ppar in enumerate(parents):
                if ppar == j:
                    W[vi, c] += 0.15
        d = np.linalg.norm(joint_pos - p[None, :], axis=1)
        inv = 1.0 / np.maximum(d, 1e-4)
        W[vi] += 0.15 * inv
    W = W / np.maximum(W.sum(axis=1, keepdims=True), 1e-12)
    joints = np.zeros((len(positions), 4), dtype=np.uint16)
    weights = np.zeros((len(positions), 4), dtype=np.float32)
    for vi in range(len(positions)):
        order = np.argsort(-W[vi])[:k]
        ww = W[vi, order]
        ww = ww / max(ww.sum(), 1e-12)
        joints[vi, :len(order)] = order.astype(np.uint16)
        weights[vi, :len(order)] = ww
    return joints, weights


def make_anubias():
    """Creeping rhizome + petioles + thick ovate leaves. Not a carpet."""
    rng = np.random.default_rng(2026)
    # Rhizome along +X, sitting on y=0
    n = 16
    t = np.linspace(-0.5, 0.5, n)
    length = 0.080  # 8 cm creeping rhizome
    centers = np.stack([
        t * length,
        0.0045 + 0.0012 * np.sin(t * 6),
        0.002 * np.sin(t * 4.2),
    ], axis=1)
    radii = 0.0042 + 0.0008 * np.sin(t * 9)
    rz_p, rz_n, rz_u, rz_i = sweep_tube(centers, radii, segs=10, closed_caps=True, uv_tiles=(1, 2))
    # Leaves along the rhizome (not a radial lawn)
    leaf_defs = [
        # offset_t, yaw, lean, petiole_len, blade_len, blade_w
        # yaw: 0 = +Z (front), pi = -Z (back). Fan around the creeping rhizome, not a flat card.
        (-0.34,  2.20, 0.42, 0.028, 0.042, 0.028),
        (-0.20, -2.40, 0.38, 0.032, 0.048, 0.030),
        (-0.06,  0.35, 0.28, 0.036, 0.052, 0.032),
        (0.08,  2.90, 0.40, 0.030, 0.046, 0.029),
        (0.22, -0.50, 0.36, 0.034, 0.050, 0.031),
        (0.36,  3.40, 0.32, 0.027, 0.040, 0.026),
    ]
    parts_leaf = []
    attach = []  # (attach_pos, leaf_base_pos, mid_blade_pos)
    prim_rz = np.zeros(len(rz_p), dtype=np.int32)  # joint 0 root
    leaf_parts_meta = []
    for li, (tt, yaw, lean, plen, blen, bw) in enumerate(leaf_defs):
        # attach on top of rhizome
        ax = tt * length
        ay = 0.008
        az = 0.001 * np.sin(tt * 3)
        attach_pos = np.array([ax, ay, az], dtype=np.float64)
        # petiole direction: up + outward in XZ from yaw
        out = np.array([np.sin(yaw), 0.0, np.cos(yaw)])
        direc = np.array([0, 1, 0]) * np.cos(lean) + out * np.sin(lean)
        direc = direc / np.linalg.norm(direc)
        leaf_base = attach_pos + direc * plen
        # petiole tube
        pc = np.stack([attach_pos, attach_pos + direc * plen * 0.5, leaf_base])
        # make 6-point line
        u = np.linspace(0, 1, 8)[:, None]
        pc = (1 - u) * attach_pos + u * leaf_base
        pr = np.linspace(0.0020, 0.0013, 8)
        pp, pn, pu, pi = sweep_tube(pc, pr, segs=6, closed_caps=True)
        # ovate blade at leaf_base, midrib along direc
        bp, bn, bu, bi = leaf_profiled(blen, bw, segs_l=9, segs_w=6, profile=ovate_profile, cup=0.0035)
        # rotate +Y (blade midrib) to direc
        # also rotate around midrib for facing
        target = direc
        yaxis = np.array([0.0, 1.0, 0.0])
        if np.linalg.norm(np.cross(yaxis, target)) < 1e-8:
            R = np.eye(3)
        else:
            v = np.cross(yaxis, target)
            c = np.dot(yaxis, target)
            vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
            R = np.eye(3) + vx + vx @ vx * (1 / (1 + c))
        # slight twist
        R = rot_y(yaw * 0.15) @ R
        bp, bn = transform_mesh(bp, bn, R=R, t=leaf_base)
        mid_blade = leaf_base + direc * (blen * 0.55)
        attach.append((attach_pos, leaf_base, mid_blade))
        leaf_parts_meta.append((len(pp), len(bp)))  # counts after we'll merge carefully
        parts_leaf.append(("pet", pp, pn, pu, pi))
        parts_leaf.append(("bld", bp, bn, bu, bi))

    # build combined leaf+petiole mesh and rhizome separately (two primitives)
    pet_parts, bld_parts = [], []
    for typ, p, n, u, i in parts_leaf:
        (pet_parts if typ == "pet" else bld_parts).append((p, n, u, i))
    pet = merge_meshes(pet_parts)
    bld = merge_meshes(bld_parts)
    foliage = merge_meshes([pet, bld])
    foliage = (foliage[0], compute_normals(foliage[0], foliage[3]), foliage[2], foliage[3])

    # joints: 0 root, then for each leaf: petiole, blade
    names = ["pl_an_root"]
    parents = [-1]
    jpos = [np.array([0.0, 0.0, 0.0])]
    for li, (att, base, mid) in enumerate(attach, start=1):
        names += [f"pl_an_petiole_{li:02d}", f"pl_an_blade_{li:02d}"]
        parents += [0, len(names) - 2]  # careful
    # rebuild parents cleanly
    names = ["pl_an_root"]
    parents = [-1]
    jpos = [np.array([0.0, 0.0, 0.0], dtype=np.float64)]
    for li, (att, base, mid) in enumerate(attach, start=1):
        pi = len(names)
        names.append(f"pl_an_petiole_{li:02d}")
        parents.append(0)
        jpos.append(att.copy())
        names.append(f"pl_an_blade_{li:02d}")
        parents.append(pi)
        jpos.append(base.copy())
    jpos = np.array(jpos, dtype=np.float64)

    # primary joints for foliage verts: by nearest attach/base
    primary_f = np.full(len(foliage[0]), -1, dtype=np.int32)
    for vi, p in enumerate(foliage[0]):
        # distance to each petiole segment vs blade
        best = 0
        bd = 1e9
        for li, (att, base, mid) in enumerate(attach):
            # dist to petiole line
            ab = base - att
            t = np.clip(np.dot(p - att, ab) / (np.dot(ab, ab) + 1e-12), 0, 1)
            dpet = np.linalg.norm(p - (att + t * ab))
            dbla = np.linalg.norm(p - mid)
            if dpet < bd:
                bd, best = dpet, 1 + li * 2  # petiole index
            if dbla < bd:
                bd, best = dbla, 2 + li * 2  # blade index
        primary_f[vi] = best
    jf, wf = skin_by_nearest_and_chain(foliage[0], jpos, parents, primary_f)

    # rhizome verts -> root
    primary_r = np.zeros(len(rz_p), dtype=np.int32)
    jr, wr = skin_by_nearest_and_chain(rz_p, jpos, parents, primary_r)

    # sway clips: per-leaf phase, blade more than petiole, root still
    def sway(n_joints, dur=3.0, fps=30):
        F = int(dur * fps) + 1
        times = np.linspace(0, dur, F)
        rots = {i: np.tile(np.array([0, 0, 0, 1.0]), (F, 1)) for i in range(n_joints)}
        nleaf = len(attach)
        for li in range(nleaf):
            pet_i = 1 + li * 2
            bld_i = 2 + li * 2
            phase = li * 0.9
            for f, tt in enumerate(times):
                s = np.sin(2 * np.pi * tt / dur + phase)
                c = np.cos(2 * np.pi * tt / dur + phase * 0.7)
                # petiole modest
                ax = 0.045 * s
                az = 0.030 * c
                rots[pet_i][f] = q_mul(q_from_axis_angle((1, 0, 0), ax), q_from_axis_angle((0, 0, 1), az))
                # blade extra, lagged
                s2 = np.sin(2 * np.pi * tt / dur + phase + 0.5)
                c2 = np.cos(2 * np.pi * tt / dur + phase + 0.4)
                ax2 = 0.10 * s2
                az2 = 0.07 * c2
                rots[bld_i][f] = q_mul(q_from_axis_angle((1, 0, 0), ax2), q_from_axis_angle((0, 0, 1), az2))
        # root stays identity
        return times, rots

    clips = {"sway_loop": sway(len(names))}
    targets = {
        "rhizome_length_m": 0.080,
        "rhizome_diameter_m": 0.0084,
        "leaf_count": len(attach),
        "leaf_blade_length_m_range": [0.040, 0.052],
        "petiole_length_m_range": [0.027, 0.036],
        "height_target_m": [0.06, 0.10],
        "silhouette": "anubias-like: creeping rhizome, petioles, thick ovate leaves; not a carpet",
    }
    return {
        "id": "pl_v0_2_anubias_proxy",
        "rhizome": (rz_p, compute_normals(rz_p, rz_i), rz_u, rz_i, jr, wr, "mat_anubias_rhizome"),
        "foliage": (foliage[0], foliage[1], foliage[2], foliage[3], jf, wf, "mat_anubias_leaf"),
        "names": names, "parents": parents, "jpos": jpos, "clips": clips,
        "kind": "anubias", "targets": targets,
    }


def make_java_fern():
    """Epiphytic rhizome + long lanceolate leaves. Mid-height for a 60 cm tank."""
    rng = np.random.default_rng(77)
    length = 0.070
    n = 14
    t = np.linspace(-0.5, 0.5, n)
    # slightly raised, as if sitting on wood (still pivot-friendly at y~0)
    centers = np.stack([
        t * length,
        0.006 + 0.0015 * np.sin(t * 5),
        0.003 * np.cos(t * 3.5),
    ], axis=1)
    radii = 0.0038 + 0.0007 * np.sin(t * 8)
    rz_p, rz_n, rz_u, rz_i = sweep_tube(centers, radii, segs=10, closed_caps=True, uv_tiles=(1, 2))

    leaf_defs = [
        # t, yaw, lean, blade_len, blade_w, stalk
        (-0.36,  2.10, 0.32, 0.100, 0.016, 0.012),
        (-0.22, -2.35, 0.36, 0.115, 0.018, 0.010),
        (-0.10,  0.40, 0.20, 0.125, 0.019, 0.011),
        (0.02,   2.85, 0.34, 0.118, 0.017, 0.013),
        (0.14,  -0.55, 0.30, 0.108, 0.016, 0.010),
        (0.26,   3.45, 0.28, 0.120, 0.018, 0.012),
        (0.38,  -2.00, 0.33, 0.095, 0.015, 0.009),
    ]
    parts_leaf = []
    attach = []
    for li, (tt, yaw, lean, blen, bw, stalk) in enumerate(leaf_defs):
        attach_pos = np.array([tt * length, 0.010, 0.001 * np.sin(tt * 2)], dtype=np.float64)
        out = np.array([np.sin(yaw), 0.0, np.cos(yaw)])
        direc = np.array([0, 1, 0]) * np.cos(lean) + out * np.sin(lean)
        direc = direc / np.linalg.norm(direc)
        leaf_base = attach_pos + direc * stalk
        u = np.linspace(0, 1, 6)[:, None]
        pc = (1 - u) * attach_pos + u * leaf_base
        pr = np.linspace(0.0016, 0.0011, 6)
        pp, pn, pu, pi = sweep_tube(pc, pr, segs=6, closed_caps=True)
        bp, bn, bu, bi = leaf_profiled(blen, bw, segs_l=11, segs_w=5, profile=lanceolate_profile, cup=0.0025)
        yaxis = np.array([0.0, 1.0, 0.0])
        target = direc
        if np.linalg.norm(np.cross(yaxis, target)) < 1e-8:
            R = np.eye(3)
        else:
            v = np.cross(yaxis, target)
            c = np.dot(yaxis, target)
            vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
            R = np.eye(3) + vx + vx @ vx * (1 / (1 + c))
        bp, bn = transform_mesh(bp, bn, R=R, t=leaf_base)
        mid = leaf_base + direc * (blen * 0.40)
        tip = leaf_base + direc * (blen * 0.85)
        attach.append((attach_pos, leaf_base, mid, tip, direc, blen))
        parts_leaf.append(("pet", pp, pn, pu, pi))
        parts_leaf.append(("bld", bp, bn, bu, bi))

    pet_parts = [(p, n, u, i) for typ, p, n, u, i in parts_leaf if typ == "pet"]
    bld_parts = [(p, n, u, i) for typ, p, n, u, i in parts_leaf if typ == "bld"]
    foliage = merge_meshes(pet_parts + bld_parts)
    foliage = (foliage[0], compute_normals(foliage[0], foliage[3]), foliage[2], foliage[3])

    names = ["pl_jf_root"]
    parents = [-1]
    jpos = [np.array([0.0, 0.0, 0.0], dtype=np.float64)]
    for li, (att, base, mid, tip, direc, blen) in enumerate(attach, start=1):
        pi = len(names)
        names.append(f"pl_jf_leaf_{li:02d}_a")  # base / stalk
        parents.append(0)
        jpos.append(att.copy())
        names.append(f"pl_jf_leaf_{li:02d}_b")  # mid blade
        parents.append(pi)
        jpos.append(mid.copy())
    jpos = np.array(jpos, dtype=np.float64)

    primary_f = np.full(len(foliage[0]), -1, dtype=np.int32)
    for vi, p in enumerate(foliage[0]):
        bd, best = 1e9, 0
        for li, (att, base, mid, tip, direc, blen) in enumerate(attach):
            # parameter along leaf
            d_a = np.linalg.norm(p - att)
            d_m = np.linalg.norm(p - mid)
            d_t = np.linalg.norm(p - tip)
            if d_a < bd:
                bd, best = d_a, 1 + li * 2
            if min(d_m, d_t) < bd:
                bd, best = min(d_m, d_t), 2 + li * 2
        primary_f[vi] = best
    jf, wf = skin_by_nearest_and_chain(foliage[0], jpos, parents, primary_f)
    primary_r = np.zeros(len(rz_p), dtype=np.int32)
    jr, wr = skin_by_nearest_and_chain(rz_p, jpos, parents, primary_r)

    def sway(n_joints, dur=3.5, fps=30):
        F = int(dur * fps) + 1
        times = np.linspace(0, dur, F)
        rots = {i: np.tile(np.array([0, 0, 0, 1.0]), (F, 1)) for i in range(n_joints)}
        nleaf = len(attach)
        for li in range(nleaf):
            a_i = 1 + li * 2
            b_i = 2 + li * 2
            phase = li * 0.75
            for f, tt in enumerate(times):
                s = np.sin(2 * np.pi * tt / dur + phase)
                c = np.cos(2 * np.pi * tt / dur * 1.0 + phase * 0.6)
                # base modest
                rots[a_i][f] = q_mul(q_from_axis_angle((1, 0, 0), 0.04 * s),
                                     q_from_axis_angle((0, 0, 1), 0.03 * c))
                # mid/tip more (long leaves flex more distally)
                s2 = np.sin(2 * np.pi * tt / dur + phase + 0.55)
                c2 = np.cos(2 * np.pi * tt / dur + phase + 0.35)
                rots[b_i][f] = q_mul(q_from_axis_angle((1, 0, 0), 0.12 * s2),
                                     q_from_axis_angle((0, 0, 1), 0.08 * c2))
        return times, rots

    clips = {"sway_loop": sway(len(names))}
    targets = {
        "rhizome_length_m": 0.070,
        "rhizome_diameter_m": 0.0076,
        "leaf_count": len(attach),
        "leaf_blade_length_m_range": [0.095, 0.125],
        "height_target_m": [0.10, 0.14],
        "silhouette": "java-fern-like: epiphytic rhizome, long lanceolate leaves",
    }
    return {
        "id": "pl_v0_2_java_fern_proxy",
        "rhizome": (rz_p, compute_normals(rz_p, rz_i), rz_u, rz_i, jr, wr, "mat_java_fern_rhizome"),
        "foliage": (foliage[0], foliage[1], foliage[2], foliage[3], jf, wf, "mat_java_fern_leaf"),
        "names": names, "parents": parents, "jpos": jpos, "clips": clips,
        "kind": "java_fern", "targets": targets,
    }


def build_plant_glb(plant, path):
    extras = {**EXTRAS_BASE, "asset": plant["id"], "pivot": "rhizome_base_origin",
              "animation": ["sway_loop"], "root_motion": False,
              "sway_note": "Procedural sine flex on petiole/leaf bones only; rhizome/root unanimated. Not a water-physics simulation.",
              "morphology_targets": plant["targets"],
              "mapping_role": (
                  "candidate technical stand-in for plant_anubias_nana; not a species scan"
                  if plant["kind"] == "anubias"
                  else "candidate technical stand-in for plant_java_fern; not a species scan"
              ),
              }
    w = GLBWriter(extras=extras)
    add_plant_materials(w, plant["kind"])
    rz = plant["rhizome"]
    fo = plant["foliage"]
    mid_r = w.add_mesh(meshdata(plant["id"] + "_rhizome", rz[0], rz[1], rz[2], rz[3], rz[6], rz[4], rz[5]))
    mid_f = w.add_mesh(meshdata(plant["id"] + "_leaves", fo[0], fo[1], fo[2], fo[3], fo[6], fo[4], fo[5]))
    local_t, ibm = chain_global_bind(plant["jpos"], plant["parents"])
    j_idxs, j_roots = add_skeleton_nodes(w, plant["names"], plant["parents"], local_t)
    # one skin shared
    skin_idx = w.add_skin(plant["id"] + "_skin", j_idxs, ibm, skeleton=j_roots[0])
    node_r = w.add_node(NodeSpec(name=plant["id"] + "_rhizome", mesh=mid_r, skin=skin_idx))
    node_f = w.add_node(NodeSpec(name=plant["id"] + "_leaves", mesh=mid_f, skin=skin_idx))
    wrapper = w.add_node(NodeSpec(name=plant["id"], children=[j_roots[0], node_r, node_f],
                                  extras={"role": "asset_root", "pivot": "rhizome_base_origin"}))
    for clip_name, (times, rots) in plant["clips"].items():
        node_rots = {j_idxs[j]: rots[j] for j in rots}
        w.add_rotation_clip(clip_name, times, node_rots,
                            extras={"loop": True, "root_motion": False,
                                    "not_water_physics": True, "units": "seconds"})
    w.save(str(path), scene_name=plant["id"], roots=[wrapper])
    # bind-pose OBJ (merged, no skin)
    merged = merge_meshes([
        (rz[0], rz[1], rz[2], rz[3]),
        (fo[0], fo[1], fo[2], fo[3]),
    ])
    write_obj(OUT_OBJ / f"{plant['id']}.obj", merged[0], merged[1], merged[2], merged[3], plant["id"])
    return path


def aabb(pos):
    return pos.min(0), pos.max(0)


def main():
    OUT_GLB.mkdir(parents=True, exist_ok=True)
    OUT_OBJ.mkdir(parents=True, exist_ok=True)
    OUT_TEX.mkdir(parents=True, exist_ok=True)
    plants = [make_anubias(), make_java_fern()]
    catalog = []
    built = []
    for plant in plants:
        path = OUT_GLB / f"{plant['id']}.glb"
        build_plant_glb(plant, path)
        rz, fo = plant["rhizome"], plant["foliage"]
        allp = np.vstack([rz[0], fo[0]])
        mn, mx = aabb(allp)
        catalog.append({
            "id": plant["id"],
            "kind": plant["kind"],
            "role": "technical_proxy",
            "species_claim": "none_technical_proxy_only",
            "replaces_mapping_suggestion": (
                "plant_anubias_nana (do not use pl_fg_v0_carpet_proxy)"
                if plant["kind"] == "anubias" else
                "plant_java_fern (prefer over pl_md_v0_leaf_proxy for this silhouette)"
            ),
            "tris_rhizome": triangle_count(rz[3]),
            "tris_leaves": triangle_count(fo[3]),
            "tris_total": triangle_count(rz[3]) + triangle_count(fo[3]),
            "bones": plant["names"],
            "animations": ["sway_loop"],
            "root_motion": False,
            "aabb_min": mn.tolist(),
            "aabb_max": mx.tolist(),
            "measured_height_m": float(mx[1] - min(0.0, mn[1])),
            "measured_size_m": (mx - mn).tolist(),
            "targets": plant["targets"],
            "pivot": "rhizome_base_origin",
            "formats": ["glb", "obj(bind_pose)"],
            "unchanged_from_v0": False,
        })
        built.append(plant)
        print(f"wrote {path} {path.stat().st_size} height={mx[1]-min(0.0,mn[1]):.4f} size={(mx-mn)}")

    # side-by-side preview scene (two new plants only)
    w = GLBWriter(extras={**EXTRAS_BASE, "asset": "scn_v0_2_two_plants_proxy",
                          "contains": ["pl_v0_2_anubias_proxy", "pl_v0_2_java_fern_proxy"],
                          "note": "Layout for comparison only; not a tank template."})
    # need both material sets
    add_plant_materials(w, "anubias")
    add_plant_materials(w, "java_fern")
    roots = []
    placements = [(-0.09, 0, 0), (0.09, 0, 0)]
    for plant, tr in zip(built, placements):
        rz, fo = plant["rhizome"], plant["foliage"]
        mid_r = w.add_mesh(meshdata(plant["id"] + "_rhizome", rz[0], rz[1], rz[2], rz[3], rz[6], rz[4], rz[5]))
        mid_f = w.add_mesh(meshdata(plant["id"] + "_leaves", fo[0], fo[1], fo[2], fo[3], fo[6], fo[4], fo[5]))
        local_t, ibm = chain_global_bind(plant["jpos"], plant["parents"])
        j_idxs, j_roots = add_skeleton_nodes(w, plant["names"], plant["parents"], local_t)
        skin_idx = w.add_skin(plant["id"] + "_skin", j_idxs, ibm, skeleton=j_roots[0])
        node_r = w.add_node(NodeSpec(name=plant["id"] + "_rhizome", mesh=mid_r, skin=skin_idx))
        node_f = w.add_node(NodeSpec(name=plant["id"] + "_leaves", mesh=mid_f, skin=skin_idx))
        wrapper = w.add_node(NodeSpec(name=plant["id"], translation=tr, children=[j_roots[0], node_r, node_f]))
        for clip_name, (times, rots) in plant["clips"].items():
            # unique clip names per plant to avoid animator collision in a combined file
            node_rots = {j_idxs[j]: rots[j] for j in rots}
            w.add_rotation_clip(f"{plant['id']}__{clip_name}", times, node_rots,
                                extras={"loop": True, "root_motion": False, "not_water_physics": True})
        roots.append(wrapper)
    scn = OUT_GLB / "scn_v0_2_two_plants_proxy.glb"
    w.save(str(scn), scene_name="scn_v0_2_two_plants_proxy", roots=roots)
    catalog.append({
        "id": "scn_v0_2_two_plants_proxy", "kind": "comparison_scene",
        "role": "technical_proxy", "formats": ["glb"],
        "note": "Anubias proxy at x=-0.09, Java fern proxy at x=+0.09. Clips are prefixed per plant.",
        "unchanged_from_v0": False,
    })
    print("wrote", scn, scn.stat().st_size)
    (ROOT / "validation").mkdir(exist_ok=True)
    (ROOT / "validation" / "generation_catalog.json").write_text(json.dumps(catalog, indent=2, ensure_ascii=False))
    return catalog


if __name__ == "__main__":
    main()
