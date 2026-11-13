#!/usr/bin/env python3
"""Procedural mesh helpers for technical-proxy aquarium assets. Units: meters."""
from __future__ import annotations

import numpy as np
from PIL import Image, ImageDraw

EPS = 1e-8


def compute_normals(positions: np.ndarray, indices: np.ndarray) -> np.ndarray:
    nrm = np.zeros_like(positions, dtype=np.float64)
    p = positions
    tri = indices.reshape(-1, 3)
    v0, v1, v2 = p[tri[:, 0]], p[tri[:, 1]], p[tri[:, 2]]
    fn = np.cross(v1 - v0, v2 - v0)
    for i in range(3):
        np.add.at(nrm, tri[:, i], fn)
    lens = np.linalg.norm(nrm, axis=1)
    lens[lens < EPS] = 1.0
    return (nrm / lens[:, None]).astype(np.float32)


def compute_tangents(positions, normals, uvs, indices) -> np.ndarray:
    """Lengyel-style tangents; w sign = handedness."""
    tan1 = np.zeros_like(positions, dtype=np.float64)
    tan2 = np.zeros_like(positions, dtype=np.float64)
    tri = indices.reshape(-1, 3)
    p, uv = positions, uvs
    for a, b, c in tri:
        e1 = p[b] - p[a]
        e2 = p[c] - p[a]
        d1 = uv[b] - uv[a]
        d2 = uv[c] - uv[a]
        denom = d1[0] * d2[1] - d1[1] * d2[0]
        f = 1.0 / denom if abs(denom) > EPS else 0.0
        sdir = f * (d2[1] * e1 - d1[1] * e2)
        tdir = f * (d1[0] * e2 - d2[0] * e1)
        tan1[a] += sdir; tan1[b] += sdir; tan1[c] += sdir
        tan2[a] += tdir; tan2[b] += tdir; tan2[c] += tdir
    tangents = np.zeros((len(positions), 4), dtype=np.float32)
    for i in range(len(positions)):
        n = normals[i].astype(np.float64)
        t = tan1[i]
        t = t - n * np.dot(n, t)
        ln = np.linalg.norm(t)
        t = t / ln if ln > EPS else np.array([1, 0, 0])
        w = 1.0 if np.dot(np.cross(n, t), tan2[i]) >= 0 else -1.0
        tangents[i] = (t[0], t[1], t[2], w)
    return tangents


def weld_none(positions, normals, uvs, indices):
    return positions.astype(np.float32), normals.astype(np.float32), uvs.astype(np.float32), indices.astype(np.uint32)


def box(xmin, xmax, ymin, ymax, zmin, zmax, invert=False):
    """6-sided box, outward normals unless invert=True (for inner glass)."""
    # 24 verts (unique normals per face)
    faces = [
        # +Z
        ([(xmin, ymin, zmax), (xmax, ymin, zmax), (xmax, ymax, zmax), (xmin, ymax, zmax)], (0, 0, 1), [(0, 0), (1, 0), (1, 1), (0, 1)]),
        # -Z
        ([(xmax, ymin, zmin), (xmin, ymin, zmin), (xmin, ymax, zmin), (xmax, ymax, zmin)], (0, 0, -1), [(0, 0), (1, 0), (1, 1), (0, 1)]),
        # +X
        ([(xmax, ymin, zmax), (xmax, ymin, zmin), (xmax, ymax, zmin), (xmax, ymax, zmax)], (1, 0, 0), [(0, 0), (1, 0), (1, 1), (0, 1)]),
        # -X
        ([(xmin, ymin, zmin), (xmin, ymin, zmax), (xmin, ymax, zmax), (xmin, ymax, zmin)], (-1, 0, 0), [(0, 0), (1, 0), (1, 1), (0, 1)]),
        # +Y
        ([(xmin, ymax, zmax), (xmax, ymax, zmax), (xmax, ymax, zmin), (xmin, ymax, zmin)], (0, 1, 0), [(0, 0), (1, 0), (1, 1), (0, 1)]),
        # -Y
        ([(xmin, ymin, zmin), (xmax, ymin, zmin), (xmax, ymin, zmax), (xmin, ymin, zmax)], (0, -1, 0), [(0, 0), (1, 0), (1, 1), (0, 1)]),
    ]
    pos, nrm, uv, idx = [], [], [], []
    for verts, n, uvs in faces:
        base = len(pos)
        for v in verts:
            pos.append(v)
            nrm.append((-n[0], -n[1], -n[2]) if invert else n)
        for u in uvs:
            uv.append(u)
        if invert:
            idx.extend([base, base + 3, base + 2, base, base + 2, base + 1])
        else:
            idx.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    return (np.array(pos, np.float32), np.array(nrm, np.float32),
            np.array(uv, np.float32), np.array(idx, np.uint32))


def merge_meshes(parts):
    pos, nrm, uv, idx = [], [], [], []
    for p, n, u, i in parts:
        base = len(pos) if not pos else len(pos)
        # pos is list of arrays being extended - fix
    pos_l, nrm_l, uv_l, idx_l = [], [], [], []
    base = 0
    for p, n, u, i in parts:
        pos_l.append(p)
        nrm_l.append(n)
        uv_l.append(u)
        idx_l.append(i + base)
        base += len(p)
    return (np.concatenate(pos_l), np.concatenate(nrm_l),
            np.concatenate(uv_l), np.concatenate(idx_l))


def hollow_box(outer, inner, skip_top=True):
    """Glass tank: outer (xmin,xmax,ymin,ymax,zmin,zmax), inner same. Open top optional."""
    ox0, ox1, oy0, oy1, oz0, oz1 = outer
    ix0, ix1, iy0, iy1, iz0, iz1 = inner
    parts = []
    # bottom slab
    parts.append(box(ox0, ox1, oy0, iy0, oz0, oz1))
    # +Z wall (front)
    parts.append(box(ox0, ox1, iy0, oy1, iz1, oz1))
    # -Z wall (back)
    parts.append(box(ox0, ox1, iy0, oy1, oz0, iz0))
    # +X
    parts.append(box(ix1, ox1, iy0, oy1, iz0, iz1))
    # -X
    parts.append(box(ox0, ix0, iy0, oy1, iz0, iz1))
    if not skip_top:
        parts.append(box(ox0, ox1, iy1, oy1, oz0, oz1))
    return merge_meshes(parts)


def value_noise(w, h, scale=8, seed=0):
    rng = np.random.default_rng(seed)
    gw, gh = scale + 1, scale + 1
    grid = rng.random((gh, gw))
    ys = np.linspace(0, scale, h, endpoint=False)
    xs = np.linspace(0, scale, w, endpoint=False)
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    fy = (ys - y0)[:, None]
    fx = (xs - x0)[None, :]
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)
    y1 = np.clip(y0 + 1, 0, gh - 1)
    x1 = np.clip(x0 + 1, 0, gw - 1)
    y0 = np.clip(y0, 0, gh - 1)
    x0 = np.clip(x0, 0, gw - 1)
    n00 = grid[y0[:, None], x0[None, :]]
    n10 = grid[y0[:, None], x1[None, :]]
    n01 = grid[y1[:, None], x0[None, :]]
    n11 = grid[y1[:, None], x1[None, :]]
    return (n00 * (1 - fx) + n10 * fx) * (1 - fy) + (n01 * (1 - fx) + n11 * fx) * fy


def fbm(w, h, seed=0, octaves=4, scale=4):
    acc = np.zeros((h, w))
    amp = 1.0
    total = 0.0
    s = scale
    for i in range(octaves):
        acc += amp * value_noise(w, h, scale=s, seed=seed + i * 17)
        total += amp
        amp *= 0.5
        s *= 2
    return acc / total


def height_to_normal_map(height: np.ndarray, strength=4.0) -> Image.Image:
    """OpenGL +Y normal map from height (0..1)."""
    dy, dx = np.gradient(height.astype(np.float64))
    nx = -dx * strength
    ny = -dy * strength  # image y downward; flip so green = up
    nz = np.ones_like(nx)
    n = np.stack([nx, -ny, nz], axis=-1)
    lens = np.linalg.norm(n, axis=2, keepdims=True)
    n = n / np.maximum(lens, EPS)
    rgb = (n * 0.5 + 0.5) * 255.0
    rgba = np.dstack([rgb, np.full(height.shape, 255)])
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def orm_map(w, h, occlusion, roughness, metallic, seed=0) -> Image.Image:
    # R=AO, G=roughness, B=metallic (glTF packs metallicRoughness as G=rough, B=metal)
    if np.isscalar(occlusion):
        o = np.full((h, w), occlusion)
    else:
        o = occlusion
    if np.isscalar(roughness):
        r = np.full((h, w), roughness)
    else:
        r = roughness
    if np.isscalar(metallic):
        m = np.full((h, w), metallic)
    else:
        m = metallic
    jitter = (fbm(w, h, seed=seed, scale=8) - 0.5) * 0.06
    r = np.clip(r + jitter, 0.04, 1.0)
    o = np.clip(o, 0, 1)
    m = np.clip(m, 0, 1)
    img = np.dstack([o, r, m, np.ones((h, w))]) * 255.0
    return Image.fromarray(img.astype(np.uint8), "RGBA")


def tex_fish(w=256, h=128, seed=3) -> Image.Image:
    # Technical proxy: olive body + one lighter mid-lateral band. Not a real species.
    n = fbm(w, h, seed=seed, scale=6)
    yy = np.linspace(-1, 1, h)[:, None]
    xx = np.linspace(0, 1, w)[None, :]
    body = np.array([0.22, 0.32, 0.20])[None, None, :]
    belly = np.array([0.55, 0.58, 0.42])[None, None, :]
    stripe = np.array([0.75, 0.82, 0.55])[None, None, :]
    t = np.clip(0.5 - yy * 0.55 + (n - 0.5) * 0.15, 0, 1)
    col = body * (1 - t[..., None]) + belly * t[..., None]
    band = np.exp(-0.5 * ((yy - 0.05) / 0.12) ** 2) * (1.0 - 0.3 * xx)
    col = col * (1 - band[..., None] * 0.85) + stripe * band[..., None]
    # dark edge / tail hint
    col *= (0.85 + 0.15 * n[..., None])
    col = np.clip(col, 0, 1)
    rgba = np.dstack([col, np.ones((h, w))]) * 255
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def tex_leaf(w=256, h=256, seed=5, tint=(0.28, 0.48, 0.22)) -> Image.Image:
    n = fbm(w, h, seed=seed, scale=5)
    xs = np.linspace(-1, 1, w)[None, :]
    ys = np.linspace(0, 1, h)[:, None]
    midrib = np.exp(-((xs) / 0.06) ** 2) * 0.25
    vein = 0.08 * np.sin(ys * 18 + xs * 6)
    g = np.clip(np.array(tint)[None, None, :] * (0.75 + 0.4 * n[..., None]) + midrib[..., None] * 0.15, 0, 1)
    g[..., 1] = np.clip(g[..., 1] + vein * 0.05, 0, 1)
    # alpha: leaf-shaped ellipse + slightly ragged edge
    rx, ry = 0.92, 0.98
    shape = ((xs / rx) ** 2 + ((ys - 0.5) * 2 / ry) ** 2)
    alpha = np.clip(1.2 - shape, 0, 1)
    alpha = np.where(alpha > 0.08, np.clip(alpha + (n - 0.5) * 0.08, 0, 1), 0)
    rgba = np.dstack([g, alpha]) * 255
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def tex_wood(w=512, h=256, seed=7) -> Image.Image:
    n = fbm(w, h, seed=seed, octaves=5, scale=3)
    xs = np.linspace(0, 8, w)[None, :]
    grain = np.sin(xs * 6.5 + n * 8.0) * 0.5 + 0.5
    dark = np.array([0.28, 0.18, 0.10])
    lite = np.array([0.45, 0.32, 0.18])
    wet = np.array([0.18, 0.12, 0.07])
    t = 0.4 * grain + 0.6 * n
    col = wet[None, None, :] * (1 - t[..., None]) + lite[None, None, :] * t[..., None]
    col = col * (0.85 + 0.2 * grain[..., None])
    col = np.clip(col, 0, 1)
    rgba = np.dstack([col, np.ones((h, w))]) * 255
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def tex_stone(w=256, h=256, seed=11) -> Image.Image:
    n = fbm(w, h, seed=seed, octaves=5, scale=4)
    n2 = fbm(w, h, seed=seed + 3, octaves=3, scale=10)
    base = np.array([0.42, 0.40, 0.36])
    dark = np.array([0.28, 0.27, 0.25])
    warm = np.array([0.48, 0.43, 0.36])
    t = n
    col = dark[None, None, :] * (1 - t[..., None]) + base[None, None, :] * t[..., None]
    col = col * (0.9 + 0.2 * n2[..., None]) + 0.05 * warm
    col = np.clip(col, 0, 1)
    rgba = np.dstack([col, np.ones((h, w))]) * 255
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def tex_sand(w=512, h=512, seed=13) -> Image.Image:
    n = fbm(w, h, seed=seed, octaves=5, scale=12)
    speck = value_noise(w, h, scale=80, seed=seed + 9)
    base = np.array([0.78, 0.70, 0.52])
    dark = np.array([0.55, 0.48, 0.34])
    col = dark[None, None, :] * (1 - n[..., None]) + base[None, None, :] * n[..., None]
    col = col * (0.92 + 0.12 * speck[..., None])
    col = np.clip(col, 0, 1)
    rgba = np.dstack([col, np.ones((h, w))]) * 255
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def tex_glass(w=64, h=64) -> Image.Image:
    img = np.zeros((h, w, 4), dtype=np.uint8)
    img[..., 0] = 230
    img[..., 1] = 240
    img[..., 2] = 245
    img[..., 3] = 40
    return Image.fromarray(img, "RGBA")


def tex_water(w=64, h=64) -> Image.Image:
    img = np.zeros((h, w, 4), dtype=np.uint8)
    img[..., 0] = 90
    img[..., 1] = 150
    img[..., 2] = 140
    img[..., 3] = 38
    return Image.fromarray(img, "RGBA")


def sweep_tube(centers: np.ndarray, radii: np.ndarray, segs=10, closed_caps=True, uv_tiles=(1, 1)):
    """centers (N,3), radii (N,). Returns pos, nrm, uv, idx. Frames via parallel transport."""
    n = len(centers)
    # tangents
    tan = np.zeros_like(centers, dtype=np.float64)
    tan[1:-1] = centers[2:] - centers[:-2]
    tan[0] = centers[1] - centers[0]
    tan[-1] = centers[-1] - centers[-2]
    tan /= np.maximum(np.linalg.norm(tan, axis=1, keepdims=True), EPS)
    # initial normal
    ref = np.array([0.0, 1.0, 0.0])
    if abs(np.dot(tan[0], ref)) > 0.9:
        ref = np.array([1.0, 0.0, 0.0])
    nor = np.cross(tan[0], ref)
    nor /= np.linalg.norm(nor)
    binorm = np.cross(tan[0], nor)
    frames_n = [nor]
    frames_b = [binorm]
    for i in range(1, n):
        v = np.cross(tan[i - 1], tan[i])
        c = np.dot(tan[i - 1], tan[i])
        if np.linalg.norm(v) < 1e-8:
            nor_i = frames_n[-1]
        else:
            # rotate previous normal around v
            axis = v / np.linalg.norm(v)
            ang = np.arctan2(np.linalg.norm(v), c)
            k = axis
            nor_p = frames_n[-1]
            nor_i = nor_p * np.cos(ang) + np.cross(k, nor_p) * np.sin(ang) + k * np.dot(k, nor_p) * (1 - np.cos(ang))
        nor_i /= np.linalg.norm(nor_i)
        b = np.cross(tan[i], nor_i)
        b /= np.linalg.norm(b)
        nor_i = np.cross(b, tan[i])
        frames_n.append(nor_i)
        frames_b.append(b)
    frames_n = np.array(frames_n)
    frames_b = np.array(frames_b)
    pos, nrm, uv = [], [], []
    for i in range(n):
        v = i / (n - 1)
        for j in range(segs):
            a = 2 * np.pi * j / segs
            offset = np.cos(a) * frames_n[i] + np.sin(a) * frames_b[i]
            pos.append(centers[i] + radii[i] * offset)
            nrm.append(offset)
            uv.append((j / segs * uv_tiles[0], v * uv_tiles[1]))
    idx = []
    for i in range(n - 1):
        for j in range(segs):
            a = i * segs + j
            b = i * segs + (j + 1) % segs
            c = (i + 1) * segs + j
            d = (i + 1) * segs + (j + 1) % segs
            idx.extend([a, c, d, a, d, b])
    pos = np.array(pos, np.float64)
    nrm = np.array(nrm, np.float64)
    if closed_caps:
        for end, sign, ii in ((0, -1, 0), (n - 1, 1, n - 1)):
            center = centers[end]
            normal = tan[end] * sign
            base = len(pos)
            pos = np.vstack([pos, center])
            nrm = np.vstack([nrm, normal])
            uv = uv + [(0.5, 0.0 if end == 0 else 1.0)]
            ring0 = end * segs
            for j in range(segs):
                # cap verts already exist on ring; just fan
                j2 = (j + 1) % segs
                if sign > 0:
                    idx.extend([base, ring0 + j, ring0 + j2])
                else:
                    idx.extend([base, ring0 + j2, ring0 + j])
    nrm = nrm / np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), EPS)
    return pos.astype(np.float32), nrm.astype(np.float32), np.array(uv, np.float32), np.array(idx, np.uint32)


def bezier_centers(p0, p1, p2, p3, n):
    t = np.linspace(0, 1, n)[:, None]
    return ((1 - t) ** 3 * p0 + 3 * (1 - t) ** 2 * t * p1 + 3 * (1 - t) * t ** 2 * p2 + t ** 3 * p3)


def leaf_blade(length=0.06, width=0.025, drop=0.01, segs_l=6, segs_w=4, bend=0.15):
    """Leaf in XY, midrib +Y, sits at origin, bends toward +Z slightly."""
    pos, uv = [], []
    for i in range(segs_l + 1):
        v = i / segs_l
        # width profile: pointed both ends-ish, wider mid-lower
        wp = np.sin(np.pi * v) ** 0.85
        y = v * length
        z = -drop * (v ** 1.4) + bend * np.sin(np.pi * v) * 0.3
        for j in range(segs_w + 1):
            u = j / segs_w
            x = (u - 0.5) * width * wp
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


def transform_mesh(pos, nrm, R=None, t=None):
    pos = pos.copy()
    nrm = nrm.copy()
    if R is not None:
        pos = pos @ R.T
        nrm = nrm @ R.T
    if t is not None:
        pos = pos + t
    nrm = nrm / np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), EPS)
    return pos.astype(np.float32), nrm.astype(np.float32)


def rot_y(a):
    c, s = np.cos(a), np.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]], dtype=np.float64)


def rot_x(a):
    c, s = np.cos(a), np.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]], dtype=np.float64)


def rot_z(a):
    c, s = np.cos(a), np.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]], dtype=np.float64)


def icosphere(radius=0.02, subdiv=2, flatten_y=0.75, seed=0, noise=0.15):
    t = (1 + 5 ** 0.5) / 2
    verts = []
    for a, b in ((-1, t), (1, t), (-1, -t), (1, -t)):
        verts += [(0, a, b), (b, 0, a), (a, b, 0)]
    verts = np.array(verts, dtype=np.float64)
    faces = np.array([
        [0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11],
        [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
        [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9],
        [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1],
    ], dtype=np.int32)
    verts = verts / np.linalg.norm(verts, axis=1, keepdims=True)
    mid_cache = {}

    def mid(a, b):
        key = tuple(sorted((int(a), int(b))))
        if key in mid_cache:
            return mid_cache[key]
        p = (verts_list[a] + verts_list[b]) / 2
        ln = np.linalg.norm(p)
        if ln < 1e-12:
            p = np.array([0.0, 1.0, 0.0])
        else:
            p = p / ln
        mid_cache[key] = len(verts_list)
        verts_list.append(p)
        return mid_cache[key]

    verts_list = [v.copy() for v in verts]
    faces_cur = faces
    for _ in range(subdiv):
        mid_cache = {}
        new_faces = []
        for a, b, c in faces_cur:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            new_faces += [[a, ab, ca], [b, bc, ab], [c, ca, bc], [ab, bc, ca]]
        faces_cur = np.array(new_faces, dtype=np.int32)
    verts = np.array(verts_list, dtype=np.float64)
    rng = np.random.default_rng(seed)
    disp = 1.0 + noise * (rng.random(len(verts)) - 0.5) * 2
    verts = verts * disp[:, None]
    verts[:, 1] *= flatten_y
    # flatten bottom a bit (sit on ground)
    ymin = verts[:, 1].min()
    verts[:, 1] = ymin + (verts[:, 1] - ymin) * 0.9
    verts = verts * radius
    # shift so bottom is at y=0
    verts[:, 1] -= verts[:, 1].min()
    # spherical uv
    x, y, z = verts[:, 0], verts[:, 1], verts[:, 2]
    u = 0.5 + np.arctan2(z, x) / (2 * np.pi)
    r = np.linalg.norm(verts, axis=1)
    v = 0.5 + np.arcsin(np.clip((y - y.mean()) / np.maximum(r, EPS), -1, 1)) / np.pi
    uv = np.stack([u, v], axis=1)
    idx = faces_cur.reshape(-1).astype(np.uint32)
    nrm = compute_normals(verts.astype(np.float32), idx)
    return verts.astype(np.float32), nrm, uv.astype(np.float32), idx


def plane_displaced(sx, sz, nx, nz, height_fn, y0=0.0):
    xs = np.linspace(-sx / 2, sx / 2, nx)
    zs = np.linspace(-sz / 2, sz / 2, nz)
    pos, uv = [], []
    for j, z in enumerate(zs):
        for i, x in enumerate(xs):
            y = y0 + height_fn(x, z)
            pos.append((x, y, z))
            uv.append((i / (nx - 1) * 3, j / (nz - 1) * 1.5))
    idx = []
    for j in range(nz - 1):
        for i in range(nx - 1):
            a = j * nx + i
            b = a + 1
            c = a + nx
            d = c + 1
            idx.extend([a, c, d, a, d, b])
    pos = np.array(pos, np.float32)
    nrm = compute_normals(pos, np.array(idx, np.uint32))
    return pos, nrm, np.array(uv, np.float32), np.array(idx, np.uint32)


def skin_chain_weights(positions: np.ndarray, joint_pos: np.ndarray, axis=2, k=4):
    """Weight vertices to a chain of joints by projected distance along the bind chain."""
    # joint_pos (J,3) in mesh space
    J = len(joint_pos)
    # parameterize joints along cumulative length
    diffs = joint_pos[1:] - joint_pos[:-1]
    seglen = np.linalg.norm(diffs, axis=1)
    cum = np.concatenate([[0], np.cumsum(seglen)])
    total = max(cum[-1], EPS)
    # project each vertex onto polyline
    w = np.zeros((len(positions), J), dtype=np.float64)
    for vi, p in enumerate(positions):
        best_d = 1e9
        best_t = 0
        for i in range(J - 1):
            a, b = joint_pos[i], joint_pos[i + 1]
            ab = b - a
            l2 = np.dot(ab, ab) or EPS
            t = np.clip(np.dot(p - a, ab) / l2, 0, 1)
            proj = a + t * ab
            d = np.linalg.norm(p - proj)
            if d < best_d:
                best_d = d
                best_t = cum[i] + t * seglen[i]
        # also consider ends
        sigma = total / max(J * 0.55, 1)
        dist = np.abs(cum - best_t)
        ww = np.exp(-0.5 * (dist / sigma) ** 2)
        w[vi] = ww
    w_sum = w.sum(axis=1, keepdims=True)
    w_sum[w_sum < EPS] = 1
    w = w / w_sum
    # top-k
    joints = np.zeros((len(positions), 4), dtype=np.uint16)
    weights = np.zeros((len(positions), 4), dtype=np.float32)
    for vi in range(len(positions)):
        order = np.argsort(-w[vi])[:k]
        ww = w[vi, order]
        ww = ww / max(ww.sum(), EPS)
        joints[vi, :len(order)] = order.astype(np.uint16)
        weights[vi, :len(order)] = ww
    return joints, weights


def triangle_count(indices) -> int:
    return int(len(indices) // 3)
