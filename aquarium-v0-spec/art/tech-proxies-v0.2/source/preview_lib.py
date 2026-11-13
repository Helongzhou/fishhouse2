#!/usr/bin/env python3
"""Independent re-import of generated GLB files (not the generator's in-memory meshes).

Two loaders:
  1) Native GLB parser (json + struct + numpy) — used for numeric checks and skin sampling
  2) pygltflib — cross-check node/mesh/anim/material counts

Also writes software-raster preview stills. These stills are NOT Unity URP shots
and do not prove glass/water refraction or camera near-view LOD.
"""
from __future__ import annotations

import json
import math
import struct
import sys
from collections import defaultdict
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SRC = Path(__file__).resolve().parent
sys.path.insert(0, str(SRC))

from glb_writer import q_mul, q_normalize, q_to_mat4, mat4_trs  # math only

GLB_DIR = ROOT / "export" / "glb"
PREV = ROOT / "preview"
VAL = ROOT / "validation"

COMPONENT = {5120: ("<i1", 1), 5121: ("<u1", 1), 5122: ("<i2", 2), 5123: ("<u2", 2),
             5125: ("<u4", 4), 5126: ("<f4", 4)}
TYPE_N = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT2": 4, "MAT3": 9, "MAT4": 16}


def read_glb(path: Path):
    data = path.read_bytes()
    if len(data) < 12 or data[:4] != b"glTF":
        raise ValueError(f"{path} is not a GLB")
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if version != 2:
        raise ValueError(f"{path} glTF version {version}")
    if length != len(data):
        # still parse; note mismatch
        pass
    off = 12
    doc = None
    blob = b""
    chunks = []
    while off + 8 <= len(data):
        clen, ctype = struct.unpack_from("<I4s", data, off)
        payload = data[off + 8: off + 8 + clen]
        off += 8 + clen
        chunks.append(ctype)
        if ctype == b"JSON":
            doc = json.loads(payload)
        elif ctype.startswith(b"BIN"):
            blob = payload
    if doc is None:
        raise ValueError(f"{path} has no JSON chunk")
    return doc, blob, {"version": version, "length": length, "file_size": len(data), "chunks": [c.decode("latin1") for c in chunks]}


def accessor_numpy(doc, blob, acc_i):
    acc = doc["accessors"][acc_i]
    typ, n = acc["type"], TYPE_N[acc["type"]]
    fmt, csz = COMPONENT[acc["componentType"]]
    count = acc["count"]
    bv = doc["bufferViews"][acc["bufferView"]] if "bufferView" in acc else None
    if bv is None:
        arr = np.zeros((count, n) if n > 1 else (count,), dtype=np.dtype(fmt))
        return arr
    offset = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = bv.get("byteStride") or (csz * n)
    if stride == csz * n:
        raw = blob[offset: offset + count * stride]
        arr = np.frombuffer(raw, dtype=np.dtype(fmt), count=count * n)
        arr = arr.reshape(count, n) if n > 1 else arr
    else:
        rows = []
        for i in range(count):
            o = offset + i * stride
            rows.append(np.frombuffer(blob[o:o + csz * n], dtype=np.dtype(fmt), count=n))
        arr = np.stack(rows, axis=0)
    if acc.get("normalized"):
        if acc["componentType"] == 5121:
            arr = arr.astype(np.float32) / 255.0
        elif acc["componentType"] == 5123:
            arr = arr.astype(np.float32) / 65535.0
    return np.array(arr, copy=True)


def node_local_matrix(node, rot_override=None):
    if "matrix" in node and rot_override is None:
        m = np.array(node["matrix"], dtype=np.float64).reshape(4, 4).T
        return m
    t = node.get("translation", [0, 0, 0])
    r = rot_override if rot_override is not None else node.get("rotation", [0, 0, 0, 1])
    s = node.get("scale", [1, 1, 1])
    return mat4_trs(t, r, s)


def walk_globals(doc, rot_overrides=None):
    """rot_overrides: node_index -> quat xyzw"""
    nodes = doc.get("nodes", [])
    children_of = defaultdict(list)
    mentioned = set()
    for i, n in enumerate(nodes):
        for c in n.get("children", []):
            children_of[i].append(c)
            mentioned.add(c)
    roots = [i for i in range(len(nodes)) if i not in mentioned]
    # also scene roots if present
    if doc.get("scenes"):
        s = doc["scenes"][doc.get("scene", 0)]
        roots = list(s.get("nodes", roots))
    world = [np.eye(4) for _ in nodes]
    visited = set()

    def rec(i, parent):
        if i in visited:
            return
        visited.add(i)
        ro = None if rot_overrides is None else rot_overrides.get(i)
        local = node_local_matrix(nodes[i], ro)
        world[i] = parent @ local
        for c in nodes[i].get("children", []):
            rec(c, world[i])

    for r in roots:
        rec(r, np.eye(4))
    # disconnected
    for i in range(len(nodes)):
        if i not in visited:
            rec(i, np.eye(4))
    return world


def sample_animation(doc, blob, anim, t, default_rots=None):
    """Return node -> rotation quat at time t (LINEAR)."""
    overrides = {} if default_rots is None else dict(default_rots)
    for ch in anim.get("channels", []):
        path = ch["target"]["path"]
        if path != "rotation":
            continue
        node = ch["target"]["node"]
        samp = anim["samplers"][ch["sampler"]]
        times = accessor_numpy(doc, blob, samp["input"]).reshape(-1).astype(np.float64)
        rots = accessor_numpy(doc, blob, samp["output"]).astype(np.float64)
        if len(rots.shape) == 1:
            rots = rots.reshape(-1, 4)
        if t <= times[0]:
            overrides[node] = q_normalize(rots[0])
            continue
        if t >= times[-1]:
            overrides[node] = q_normalize(rots[-1])
            continue
        i = int(np.searchsorted(times, t, side="right") - 1)
        i = max(0, min(i, len(times) - 2))
        u = (t - times[i]) / max(times[i + 1] - times[i], 1e-12)
        a, b = rots[i], rots[i + 1]
        if np.dot(a, b) < 0:
            b = -b
        overrides[node] = q_normalize((1 - u) * a + u * b)
    return overrides


def load_primitive(doc, blob, prim):
    attr = prim["attributes"]
    pos = accessor_numpy(doc, blob, attr["POSITION"]).astype(np.float64)
    nrm = accessor_numpy(doc, blob, attr["NORMAL"]).astype(np.float64) if "NORMAL" in attr else None
    uv = accessor_numpy(doc, blob, attr["TEXCOORD_0"]).astype(np.float64) if "TEXCOORD_0" in attr else None
    joints = accessor_numpy(doc, blob, attr["JOINTS_0"]).astype(np.int32) if "JOINTS_0" in attr else None
    weights = accessor_numpy(doc, blob, attr["WEIGHTS_0"]).astype(np.float64) if "WEIGHTS_0" in attr else None
    tan = accessor_numpy(doc, blob, attr["TANGENT"]).astype(np.float64) if "TANGENT" in attr else None
    if "indices" in prim:
        idx = accessor_numpy(doc, blob, prim["indices"]).astype(np.int64).reshape(-1)
    else:
        idx = np.arange(len(pos), dtype=np.int64)
    mat = prim.get("material")
    return {"pos": pos, "nrm": nrm, "uv": uv, "joints": joints, "weights": weights,
            "tan": tan, "idx": idx, "material": mat, "mode": prim.get("mode", 4)}


def skin_mesh(prim, doc, blob, world, skin_i):
    skin = doc["skins"][skin_i]
    joint_nodes = skin["joints"]
    ibm = accessor_numpy(doc, blob, skin["inverseBindMatrices"]).astype(np.float64)
    if ibm.ndim == 2:
        ibm = ibm.reshape(-1, 4, 4)
    # stored column-major; our mat4 multiplies as row-vector? We use column-vector (M @ v)
    # glTF matrix accessors are column-major 16 floats = our .T of row-major stored earlier.
    # Writer did: packed = np.transpose(ibm, (0, 2, 1)) so file is column-major of math IBM.
    # After reshape (J,4,4) as C-order from column-major 16-float... 
    # We wrote packed as (J, 16) of columns of math IBM, i.e. math IBM.T flattened row-wise?
    # np.transpose(ibm, (0,2,1)) then reshape (J,16): each 16 is row-major of IBM.T = column-major of IBM.
    # np.frombuffer reshape (J,4,4) in C-order interprets 16 floats as rows of IBM.T, so mat = IBM.T
    # Therefore math IBM = mat.transpose(0,2,1)
    ibm_math = np.transpose(ibm, (0, 2, 1))
    Jmats = []
    for ji, node_i in enumerate(joint_nodes):
        Jmats.append(world[node_i] @ ibm_math[ji])
    Jmats = np.stack(Jmats, axis=0)
    pos = prim["pos"]
    out = np.zeros_like(pos)
    js, ws = prim["joints"], prim["weights"]
    for v in range(len(pos)):
        acc = np.zeros(3)
        wsum = 0.0
        for k in range(js.shape[1]):
            w = float(ws[v, k])
            if w <= 0:
                continue
            j = int(js[v, k])
            m = Jmats[j]
            p = np.array([pos[v, 0], pos[v, 1], pos[v, 2], 1.0])
            acc += w * (m @ p)[:3]
            wsum += w
        out[v] = acc / wsum if wsum > 1e-8 else pos[v]
    return out


def gather_posed_meshes(doc, blob, rot_overrides=None, skip_transparent=False):
    world = walk_globals(doc, rot_overrides)
    meshes_out = []
    nodes = doc.get("nodes", [])
    mats = doc.get("materials", [])
    for i, node in enumerate(nodes):
        if "mesh" not in node:
            continue
        mesh = doc["meshes"][node["mesh"]]
        for prim in mesh["primitives"]:
            p = load_primitive(doc, blob, prim)
            mat = mats[p["material"]] if p["material"] is not None and mats else {}
            alpha_mode = mat.get("alphaMode", "OPAQUE")
            base = mat.get("pbrMetallicRoughness", {}).get("baseColorFactor", [1, 1, 1, 1])
            if skip_transparent and alpha_mode == "BLEND":
                continue
            if "skin" in node:
                pos = skin_mesh(p, doc, blob, world, node["skin"])
            else:
                M = world[i]
                h = np.c_[p["pos"], np.ones((len(p["pos"]), 1))]
                pos = (h @ M.T)[:, :3]
            # normals (approx rotate)
            nrm = p["nrm"]
            if nrm is not None:
                R = world[i][:3, :3]
                # for skinned, skip accurate nrm; use bind nrm rotated by wrapper only
                nrm = nrm @ np.linalg.inv(R).T if abs(np.linalg.det(R)) > 1e-8 else nrm
                ln = np.linalg.norm(nrm, axis=1, keepdims=True)
                nrm = nrm / np.maximum(ln, 1e-8)
            meshes_out.append({
                "name": mesh.get("name", f"mesh{node['mesh']}"),
                "node": node.get("name", str(i)),
                "pos": pos, "nrm": nrm, "idx": p["idx"], "uv": p["uv"],
                "alpha_mode": alpha_mode, "base": base, "material": mat,
                "mat_index": p["material"],
            })
    return meshes_out, world


def aabb_of_meshes(meshes):
    pts = np.vstack([m["pos"] for m in meshes])
    return pts.min(0), pts.max(0)


def raster(meshes, eye, target, up, width=800, height=450, fov_deg=40, bg=(18, 20, 24),
           light_dir=(0.2, 0.95, 0.25), title=None, caption=None):
    eye = np.array(eye, dtype=np.float64)
    target = np.array(target, dtype=np.float64)
    up = np.array(up, dtype=np.float64)
    f = target - eye
    f = f / np.linalg.norm(f)
    s = np.cross(f, up)
    sl = np.linalg.norm(s)
    if sl < 1e-8:
        s = np.array([1.0, 0, 0])
    else:
        s = s / sl
    u = np.cross(s, f)
    # view: world to camera (x right, y up, z backward? we want camera looks +f, OpenGL style -Z)
    # Use camera space: x=s, y=u, z=-f (OpenGL)
    V = np.eye(4)
    V[0, :3] = s
    V[1, :3] = u
    V[2, :3] = -f
    V[:3, 3] = -V[:3, :3] @ eye
    aspect = width / height
    fov = math.radians(fov_deg)
    near, far = 0.01, 8.0
    tan_h = math.tan(fov / 2)
    P = np.zeros((4, 4))
    P[0, 0] = 1 / (aspect * tan_h)
    P[1, 1] = 1 / tan_h
    P[2, 2] = (far + near) / (near - far)
    P[2, 3] = (2 * far * near) / (near - far)
    P[3, 2] = -1
    zbuf = np.full((height, width), 1.0, dtype=np.float64)
    img = np.zeros((height, width, 3), dtype=np.float64)
    img[:, :] = np.array(bg) / 255.0
    L = np.array(light_dir, dtype=np.float64)
    L = L / np.linalg.norm(L)
    # split opaque / blend
    opaques = [m for m in meshes if m["alpha_mode"] != "BLEND"]
    blends = [m for m in meshes if m["alpha_mode"] == "BLEND"]

    def draw_list(mlist, do_blend):
        for m in mlist:
            pos, nrm, idx = m["pos"], m["nrm"], m["idx"]
            base = np.array(m["base"][:3], dtype=np.float64)
            alpha = float(m["base"][3] if len(m["base"]) > 3 else 1.0)
            if m["alpha_mode"] == "MASK":
                alpha = 1.0
            name = (m["name"] + " " + m["node"] + " " + str((m.get("material") or {}).get("name") or "")).lower()
            # Preview only: software raster does not sample textures; use readable proxy tints.
            if any(s in name for s in ("leaf", "carpet", "pl_fg", "pl_md", "mat_leaf")):
                base = np.array([0.32, 0.55, 0.24])
            elif any(s in name for s in ("wood", "wd_", "mat_wood")):
                base = np.array([0.46, 0.30, 0.16])
            elif any(s in name for s in ("stone", "rk_", "mat_stone")):
                base = np.array([0.50, 0.47, 0.42])
            elif any(s in name for s in ("sand", "sb_", "mat_sand")):
                base = np.array([0.80, 0.72, 0.54])
            elif any(s in name for s in ("fish", "fh_", "mat_fish")):
                base = np.array([0.55, 0.62, 0.38])
            elif any(s in name for s in ("water", "mat_water")):
                base = np.array([0.30, 0.50, 0.48]); alpha = min(alpha, 0.16)
            elif any(s in name for s in ("glass", "tk_", "mat_glass")):
                base = np.array([0.72, 0.82, 0.86]); alpha = min(alpha, 0.14)
            if nrm is None:
                nrm = np.zeros_like(pos)
                nrm[:, 1] = 1
            # project
            h = np.c_[pos, np.ones((len(pos), 1))]
            clip = (P @ (V @ h.T)).T
            w = clip[:, 3:4]
            w[np.abs(w) < 1e-9] = 1e-9
            ndc = clip[:, :3] / w
            sx = (ndc[:, 0] * 0.5 + 0.5) * (width - 1)
            sy = (1.0 - (ndc[:, 1] * 0.5 + 0.5)) * (height - 1)
            sz = ndc[:, 2] * 0.5 + 0.5
            tris = idx.reshape(-1, 3)
            for a, b, c in tris:
                x0, y0, z0 = sx[a], sy[a], sz[a]
                x1, y1, z1 = sx[b], sy[b], sz[b]
                x2, y2, z2 = sx[c], sy[c], sz[c]
                # near/far clip
                if min(z0, z1, z2) < 0 or max(z0, z1, z2) > 1:
                    continue
                minx, maxx = int(max(0, math.floor(min(x0, x1, x2)))), int(min(width - 1, math.ceil(max(x0, x1, x2))))
                miny, maxy = int(max(0, math.floor(min(y0, y1, y2)))), int(min(height - 1, math.ceil(max(y0, y1, y2))))
                if minx >= maxx or miny >= maxy:
                    continue
                area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
                if abs(area) < 1e-8:
                    continue
                n = (nrm[a] + nrm[b] + nrm[c])
                ln = np.linalg.norm(n)
                n = n / ln if ln > 1e-8 else np.array([0, 1, 0])
                ndot = float(np.clip(np.dot(n, L), 0, 1))
                shade = 0.18 + 0.82 * ndot
                # subtle top light rim
                col = np.clip(base * shade * 1.05, 0, 1)
                invA = 1.0 / area
                ys = np.arange(miny, maxy + 1)
                xs = np.arange(minx, maxx + 1)
                if ys.size == 0 or xs.size == 0:
                    continue
                XX, YY = np.meshgrid(xs, ys)
                w0 = ((x1 - XX) * (y2 - YY) - (x2 - XX) * (y1 - YY)) * invA
                w1 = ((x2 - XX) * (y0 - YY) - (x0 - XX) * (y2 - YY)) * invA
                w2 = 1.0 - w0 - w1
                mask = (w0 >= -0.001) & (w1 >= -0.001) & (w2 >= -0.001)
                z = w0 * z0 + w1 * z1 + w2 * z2
                mask &= (z >= 0) & (z <= 1)
                if not np.any(mask):
                    continue
                yy = YY[mask].astype(np.int32)
                xx = XX[mask].astype(np.int32)
                zz = z[mask]
                if do_blend:
                    src_a = min(alpha, 0.35)
                    closer = zz <= (zbuf[yy, xx] + 0.001)
                    yy, xx = yy[closer], xx[closer]
                    if yy.size:
                        img[yy, xx] = img[yy, xx] * (1 - src_a) + col * src_a
                else:
                    closer = zz < zbuf[yy, xx]
                    yy, xx, zz = yy[closer], xx[closer], zz[closer]
                    if yy.size:
                        zbuf[yy, xx] = zz
                        img[yy, xx] = col

    draw_list(opaques, False)
    draw_list(blends, True)
    pix = (np.clip(img, 0, 1) * 255).astype(np.uint8)
    im = Image.fromarray(pix, "RGB")
    draw = ImageDraw.Draw(im)
    bar = 28
    draw.rectangle([0, 0, width, bar], fill=(10, 12, 14))
    label = title or ""
    draw.text((8, 6), label, fill=(220, 225, 220))
    if caption:
        draw.rectangle([0, height - 36, width, height], fill=(10, 12, 14))
        # wrap caption
        draw.text((8, height - 32), caption[:180], fill=(180, 186, 180))
    return im


def pygltf_counts(path):
    try:
        from pygltflib import GLTF2
        g = GLTF2().load(str(path))
        return {
            "ok": True,
            "nodes": len(g.nodes or []),
            "meshes": len(g.meshes or []),
            "materials": len(g.materials or []),
            "skins": len(g.skins or []),
            "animations": len(g.animations or []),
            "accessors": len(g.accessors or []),
            "images": len(g.images or []),
            "textures": len(g.textures or []),
        }
    except Exception as e:
        return {"ok": False, "error": str(e)}


def check_file(path: Path) -> dict:
    doc, blob, meta = read_glb(path)
    report = {"path": str(path), "file_name": path.name, "file_bytes": path.stat().st_size, "glb_meta": meta}
    issues = []
    notes = []
    pyg = pygltf_counts(path)
    report["pygltflib"] = pyg
    if not pyg.get("ok"):
        issues.append(f"pygltflib failed: {pyg.get('error')}")
    else:
        # count match
        def n(key, attr):
            a, b = len(doc.get(attr, []) or []), pyg[key]
            if a != b:
                issues.append(f"count mismatch {attr}: native={a} pygltflib={b}")
        n("nodes", "nodes"); n("meshes", "meshes"); n("materials", "materials")
        n("skins", "skins"); n("animations", "animations")
    # asset extras
    report["asset_extras"] = (doc.get("asset") or {}).get("extras", {})
    report["extensionsUsed"] = doc.get("extensionsUsed", [])
    # meshes
    mesh_stats = []
    nan_found = False
    for mi, mesh in enumerate(doc.get("meshes", [])):
        for pi, prim in enumerate(mesh.get("primitives", [])):
            p = load_primitive(doc, blob, prim)
            pos = p["pos"]
            if not np.isfinite(pos).all():
                nan_found = True
                issues.append(f"{mesh.get('name')} has non-finite positions")
            mn, mx = pos.min(0), pos.max(0)
            tris = len(p["idx"]) // 3
            wsum = None
            if p["weights"] is not None:
                wsum = p["weights"].sum(axis=1)
                if np.any(np.abs(wsum - 1.0) > 0.02):
                    issues.append(f"{mesh.get('name')} skin weights not normalized (max abs err {float(np.max(np.abs(wsum-1))):.4f})")
            mesh_stats.append({
                "name": mesh.get("name"), "tris": tris, "verts": int(len(pos)),
                "aabb_min": mn.tolist(), "aabb_max": mx.tolist(),
                "size_m": (mx - mn).tolist(),
                "has_normals": p["nrm"] is not None,
                "has_uv": p["uv"] is not None,
                "has_tangents": p["tan"] is not None,
                "has_skin": p["joints"] is not None,
                "material_index": p["material"],
            })
    report["meshes"] = mesh_stats
    report["total_tris"] = int(sum(m["tris"] for m in mesh_stats))
    if nan_found:
        issues.append("NaN/Inf in positions")
    # posed aabb bind
    meshes_bind, world = gather_posed_meshes(doc, blob, None)
    if meshes_bind:
        mn, mx = aabb_of_meshes(meshes_bind)
        report["world_aabb_bind"] = {"min": mn.tolist(), "max": mx.tolist(), "size_m": (mx - mn).tolist()}
    # nodes / bones
    report["nodes"] = [{"i": i, "name": n.get("name"), "mesh": n.get("mesh"), "skin": n.get("skin"),
                        "children": n.get("children", []), "translation": n.get("translation"),
                        "rotation": n.get("rotation")} for i, n in enumerate(doc.get("nodes", []))]
    report["skins"] = []
    for skin in doc.get("skins", []):
        jnames = [doc["nodes"][j].get("name") for j in skin.get("joints", [])]
        report["skins"].append({"name": skin.get("name"), "joints": jnames, "joint_count": len(jnames)})
    # materials
    mat_info = []
    for mat in doc.get("materials", []):
        pbr = mat.get("pbrMetallicRoughness", {})
        mat_info.append({
            "name": mat.get("name"),
            "baseColorFactor": pbr.get("baseColorFactor"),
            "metallicFactor": pbr.get("metallicFactor"),
            "roughnessFactor": pbr.get("roughnessFactor"),
            "has_baseColorTexture": "baseColorTexture" in pbr,
            "has_metallicRoughnessTexture": "metallicRoughnessTexture" in pbr,
            "has_normalTexture": "normalTexture" in mat,
            "alphaMode": mat.get("alphaMode", "OPAQUE"),
            "doubleSided": mat.get("doubleSided", False),
            "extensions": list((mat.get("extensions") or {}).keys()),
        })
    report["materials"] = mat_info
    # images exist
    report["image_count"] = len(doc.get("images", []))
    for im in doc.get("images", []):
        if "bufferView" in im:
            bv = doc["bufferViews"][im["bufferView"]]
            raw = blob[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
            if raw[:8] != b"\x89PNG\r\n\x1a\n":
                issues.append(f"image {im.get('name')} is not a PNG")
    # animations
    anim_reports = []
    movement = {}
    for anim in doc.get("animations", []):
        name = anim.get("name")
        times_all = []
        channels = []
        for ch in anim.get("channels", []):
            samp = anim["samplers"][ch["sampler"]]
            times = accessor_numpy(doc, blob, samp["input"]).reshape(-1).astype(np.float64)
            times_all.append(times)
            channels.append({"path": ch["target"]["path"], "node": doc["nodes"][ch["target"]["node"]].get("name"),
                             "frames": int(len(times)), "t0": float(times[0]), "t1": float(times[-1]),
                             "interpolation": samp.get("interpolation")})
        t0 = float(min(t[0] for t in times_all)) if times_all else 0
        t1 = float(max(t[-1] for t in times_all)) if times_all else 0
        # loop: first/last rotation close
        loop_ok = True
        for ch in anim.get("channels", []):
            if ch["target"]["path"] != "rotation":
                continue
            samp = anim["samplers"][ch["sampler"]]
            rots = accessor_numpy(doc, blob, samp["output"]).astype(np.float64).reshape(-1, 4)
            a, b = q_normalize(rots[0]), q_normalize(rots[-1])
            if min(np.linalg.norm(a - b), np.linalg.norm(a + b)) > 0.05:
                loop_ok = False
        # sample bind vs mid vs later — movement of skinned/posed verts
        samples = {}
        for label, tt in (("t0", t0), ("t_mid", (t0 + t1) * 0.5), ("t_q", t0 + 0.25 * (t1 - t0)), ("t1", t1)):
            ov = sample_animation(doc, blob, anim, tt)
            ms, _ = gather_posed_meshes(doc, blob, ov, skip_transparent=True)
            if not ms:
                ms, _ = gather_posed_meshes(doc, blob, ov, skip_transparent=False)
            # focus on skinned names if possible
            pts = np.vstack([m["pos"] for m in ms])
            samples[label] = {"aabb_min": pts.min(0).tolist(), "aabb_max": pts.max(0).tolist(),
                              "centroid": pts.mean(0).tolist()}
        # displacement between t0 and t_q on opaque meshes
        ov0 = sample_animation(doc, blob, anim, t0)
        ovq = sample_animation(doc, blob, anim, t0 + 0.25 * max(t1 - t0, 1e-6))
        m0, _ = gather_posed_meshes(doc, blob, ov0, skip_transparent=True)
        mq, _ = gather_posed_meshes(doc, blob, ovq, skip_transparent=True)
        disp = 0.0
        max_vert = 0.0
        if m0 and mq:
            # compare by name
            by0 = {m["name"]: m["pos"] for m in m0}
            for m in mq:
                if m["name"] in by0 and len(by0[m["name"]]) == len(m["pos"]):
                    d = np.linalg.norm(m["pos"] - by0[m["name"]], axis=1)
                    max_vert = max(max_vert, float(d.max()))
                    disp = max(disp, float(d.mean()))
        moved = max_vert > 1e-5
        movement[name] = {"max_vertex_disp_m": max_vert, "mean_disp_m": disp, "moved": moved}
        if not moved:
            issues.append(f"animation '{name}' produced no measurable vertex motion (max disp {max_vert})")
        anim_reports.append({
            "name": name, "duration_s": t1 - t0, "t0": t0, "t1": t1,
            "channels": channels, "loop_keys_close": loop_ok,
            "root_motion_declared_false": True,
            "samples": samples, "vertex_motion": movement[name],
            "extras": anim.get("extras"),
        })
    report["animations"] = anim_reports
    report["movement"] = movement

    # asset-specific expectations
    name = path.name
    if name.startswith("tk_v0_tank60") and "scn_" not in name:
        if meshes_bind:
            mn, mx = aabb_of_meshes(meshes_bind)
            # outer width/depth/height close to 0.60 / 0.30 / 0.36
            size = mx - mn
            report["tank_size_measured_m"] = {"x_width": float(size[0]), "y_height": float(size[1]), "z_depth": float(size[2])}
            if abs(size[0] - 0.60) > 0.008:
                issues.append(f"tank width {size[0]:.4f} m != 0.60")
            if abs(size[2] - 0.30) > 0.008:
                issues.append(f"tank depth {size[2]:.4f} m != 0.30")
            if abs(size[1] - 0.36) > 0.012:
                issues.append(f"tank height {size[1]:.4f} m != 0.36 (incl. bottom glass)")
            notes.append("Glass/water materials use BLEND + optional KHR_materials_transmission. File self-check cannot pass Unity URP refraction.")
    if name.startswith("fh_v0_tetra_proxy"):
        if meshes_bind:
            mn, mx = aabb_of_meshes(meshes_bind)
            length = float(mx[2] - mn[2])  # forward +Z
            report["fish_length_m"] = length
            report["fish_length_cm"] = length * 100
            if not (0.028 <= length <= 0.055):
                issues.append(f"fish length {length:.4f} m outside 2.8–5.5 cm proxy band")
            if "swim_tail_loop" in movement and not movement["swim_tail_loop"]["moved"]:
                issues.append("swim_tail_loop did not move")
            if "turn_left" in movement and not movement["turn_left"]["moved"]:
                issues.append("turn_left did not move")
            # turn should show more X spread or centroid shift in X vs swim
    if "carpet" in name or "leaf_proxy" in name:
        if "sway_loop" in movement and not movement["sway_loop"]["moved"]:
            issues.append("sway_loop did not move")
        if meshes_bind:
            mn, mx = aabb_of_meshes(meshes_bind)
            h = float(mx[1] - mn[1])
            report["plant_height_m"] = h
    if name.startswith("wd_"):
        if meshes_bind:
            span = float(np.linalg.norm(aabb_of_meshes(meshes_bind)[1] - aabb_of_meshes(meshes_bind)[0]))
            report["wood_diag_span_m"] = span
            if span < 0.25 or span > 0.55:
                notes.append(f"wood diagonal span {span:.3f} m (guidance was ~1/2–2/3 of 0.60 m)")
    # unit sanity: nothing should be in centimeters accidentally (e.g. 60 meters)
    if meshes_bind:
        mn, mx = aabb_of_meshes(meshes_bind)
        extent = float(np.max(mx - mn))
        if extent > 5:
            issues.append(f"world extent {extent:.3f} m looks like wrong units")
        if extent < 0.005:
            issues.append(f"world extent {extent:.6f} m is implausibly small")

    report["issues"] = issues
    report["notes"] = notes
    report["passed_file_self_check"] = len(issues) == 0
    report["engine_tests"] = {
        "unity_urp_import": "NOT_RUN",
        "unity_urp_glass_refraction": "NOT_RUN",
        "unity_urp_water": "NOT_RUN",
        "unity_camera_near_lod": "NOT_RUN",
        "blender_viewport": "NOT_RUN_blender_not_installed",
        "fbx_roundtrip": "NOT_EXPORTED",
    }
    return report, doc, blob


# ---------- expected checks / previews ----------

PREVIEW_JOBS = [
    # (glb, time_anim, t, eye, target, fov, filename, title, caption)
]


def frame_from_meshes(meshes, view="front", dist_scale=2.4):
    mn, mx = aabb_of_meshes(meshes)
    c = (mn + mx) * 0.5
    size = mx - mn
    r = max(float(np.linalg.norm(size) * 0.5), 0.01)
    if view == "front":
        eye = c + np.array([0.0, size[1] * 0.2, r * dist_scale])
    elif view == "side":
        eye = c + np.array([r * dist_scale, size[1] * 0.2, 0.0])
    elif view == "oblique":
        eye = c + np.array([r * 0.95, r * 0.75, r * 1.15])
    else:
        eye = c + np.array([0.0, r * 0.3, r * dist_scale])
    return eye, c


def build_jobs():
    jobs = []
    # view=auto uses AABB framing; eye/target placeholders ignored
    def J(glb, anim, t, view, fov, fname, title, caption, eye=None, target=None, dist=2.4):
        jobs.append((glb, anim, t, eye, target, fov, fname, title, caption, view, dist))
    J("tk_v0_tank60.glb", None, 0, "front", 35, "01_tank_front_bind.png", "tk_v0_tank60 bind · front",
      "Software raster only. Glass/water = alpha blend, NOT refraction. Not a Unity URP shot.", dist=2.1)
    J("sb_v0_sand.glb", None, 0, "oblique", 40, "02_sand_oblique.png", "sb_v0_sand",
      "Technical proxy substrate. Software raster.", dist=2.0)
    J("wd_v0_driftwood.glb", None, 0, "oblique", 35, "03_driftwood.png", "wd_v0_driftwood",
      "Procedural hardscape proxy, not a scan. Local pivot at bottom center.", dist=2.3)
    J("rk_v0_stone_set.glb", None, 0, "oblique", 35, "04_stones.png", "rk_v0_stone_set",
      "Procedural stones. Local pivot at set bottom center.", dist=2.3)
    J("fh_v0_tetra_proxy_L0.glb", "swim_tail_loop", 0.0, "side", 30, "05_fish_L0_swim_t0.png",
      "fh_v0_tetra_proxy_L0 swim_tail_loop t=0",
      "Side view so tail is readable. Technical proxy, not a real species.", dist=3.2)
    J("fh_v0_tetra_proxy_L0.glb", "swim_tail_loop", 0.5, "side", 30, "06_fish_L0_swim_t0.5.png",
      "fh_v0_tetra_proxy_L0 swim_tail_loop t=0.5s",
      "Compare tail pose to t=0 still. File self-check, not engine.", dist=3.2)
    J("fh_v0_tetra_proxy_L0.glb", "turn_left", 0.75, "oblique", 32, "07_fish_L0_turn_left_t0.75.png",
      "fh_v0_tetra_proxy_L0 turn_left t=0.75s",
      "In-place turn, no root motion. Software raster.", dist=3.4)
    J("pl_fg_v0_carpet_proxy.glb", "sway_loop", 0.0, "oblique", 32, "08_carpet_sway_t0.png",
      "pl_fg_v0_carpet_proxy sway t=0", "Foreground carpet proxy. Camera auto-framed to AABB.", dist=2.6)
    J("pl_fg_v0_carpet_proxy.glb", "sway_loop", 1.5, "oblique", 32, "09_carpet_sway_t1.5.png",
      "pl_fg_v0_carpet_proxy sway t=1.5s", "Compare blade lean to t=0.", dist=2.6)
    J("pl_md_v0_leaf_proxy.glb", "sway_loop", 0.0, "oblique", 32, "10_midleaf_sway_t0.png",
      "pl_md_v0_leaf_proxy sway t=0", "Midground leaf proxy, not a species scan.", dist=2.6)
    J("pl_md_v0_leaf_proxy.glb", "sway_loop", 1.5, "oblique", 32, "11_midleaf_sway_t1.5.png",
      "pl_md_v0_leaf_proxy sway t=1.5s", "Compare leaf pose to t=0.", dist=2.6)
    J("scn_v0_tank60_triangle_proxy.glb", "swim_tail_loop", 0.4, None, 32, "12_scene_front_swim.png",
      "assembled scene · front (software raster)",
      "60x30x36 cm proxy layout. Transparency is alpha only. Unity URP not tested.",
      eye=(0.0, 0.18, 0.78), target=(0.0, 0.14, 0.0))
    J("scn_v0_tank60_triangle_proxy.glb", "swim_tail_loop", 0.4, None, 35, "13_scene_oblique.png",
      "assembled scene · oblique",
      "Near-view composition check in software raster only.",
      eye=(0.42, 0.28, 0.42), target=(0.0, 0.08, 0.0))
    J("scn_v0_tank60_triangle_proxy.glb", "swim_tail_loop", 0.4, None, 40, "14_scene_near_fish.png",
      "near view toward fish (software raster)",
      "Does not prove Unity near-camera LOD. No magnifier UI.",
      eye=(0.10, 0.10, 0.22), target=(0.08, 0.10, 0.02))
    J("fh_v0_tetra_proxy_L1.glb", "swim_tail_loop", 0.5, "side", 30, "15_fish_L1_swim.png",
      "fh_v0_tetra_proxy_L1", "LOD1 technical proxy. Side view.", dist=3.2)
    J("fh_v0_tetra_proxy_L2.glb", "swim_tail_loop", 0.5, "side", 30, "16_fish_L2_swim.png",
      "fh_v0_tetra_proxy_L2", "LOD2 technical proxy. Side view.", dist=3.2)
    return jobs


def find_anim(doc, name):
    for a in doc.get("animations", []):
        if a.get("name") == name:
            return a
    return (doc.get("animations") or [None])[0]


# main() stripped; this module is a raster/GLB helper for v0.2 validation.


def load_material_textures(doc, blob):
    """Return list aligned with materials: dict with rgba numpy or None."""
    import io
    from PIL import Image
    out = []
    images = doc.get("images") or []
    textures = doc.get("textures") or []
    mats = doc.get("materials") or []
    def img_rgba(img):
        if "bufferView" not in img:
            return None
        bv = doc["bufferViews"][img["bufferView"]]
        raw = blob[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
        im = Image.open(io.BytesIO(raw)).convert("RGBA")
        return np.array(im)
    for mat in mats:
        pbr = mat.get("pbrMetallicRoughness") or {}
        rec = {"rgba": None, "alphaMode": mat.get("alphaMode", "OPAQUE"),
               "alphaCutoff": float(mat.get("alphaCutoff", 0.5)),
               "base": pbr.get("baseColorFactor", [1, 1, 1, 1]),
               "name": mat.get("name")}
        if "baseColorTexture" in pbr:
            tex = textures[pbr["baseColorTexture"]["index"]]
            rec["rgba"] = img_rgba(images[tex["source"]])
        out.append(rec)
    return out


def attach_uv_textures(meshes, doc, blob):
    texs = load_material_textures(doc, blob)
    for m in meshes:
        mi = m.get("mat_index")
        m["tex"] = texs[mi] if mi is not None and mi < len(texs) else None
        # also keep original uv
    return meshes


def raster_textured(meshes, eye, target, up, width=800, height=450, fov_deg=40, bg=(18, 20, 24),
                    light_dir=(0.2, 0.95, 0.25), title=None, caption=None):
    """Software z-buffer with optional baseColor UV sampling. NOT Unity URP."""
    from PIL import Image, ImageDraw
    eye = np.array(eye, dtype=np.float64)
    target = np.array(target, dtype=np.float64)
    up = np.array(up, dtype=np.float64)
    f = target - eye
    f = f / np.linalg.norm(f)
    s = np.cross(f, up)
    sl = np.linalg.norm(s)
    s = s / sl if sl > 1e-8 else np.array([1.0, 0, 0])
    u = np.cross(s, f)
    V = np.eye(4)
    V[0, :3] = s
    V[1, :3] = u
    V[2, :3] = -f
    V[:3, 3] = -V[:3, :3] @ eye
    aspect = width / height
    fov = math.radians(fov_deg)
    near, far = 0.01, 8.0
    tan_h = math.tan(fov / 2)
    P = np.zeros((4, 4))
    P[0, 0] = 1 / (aspect * tan_h)
    P[1, 1] = 1 / tan_h
    P[2, 2] = (far + near) / (near - far)
    P[2, 3] = (2 * far * near) / (near - far)
    P[3, 2] = -1
    zbuf = np.full((height, width), 1.0, dtype=np.float64)
    img = np.zeros((height, width, 3), dtype=np.float64)
    img[:, :] = np.array(bg) / 255.0
    L = np.array(light_dir, dtype=np.float64)
    L = L / np.linalg.norm(L)
    opaques = [m for m in meshes if m["alpha_mode"] != "BLEND"]
    blends = [m for m in meshes if m["alpha_mode"] == "BLEND"]

    def draw_list(mlist, do_blend):
        for m in mlist:
            pos, nrm, idx = m["pos"], m["nrm"], m["idx"]
            uv = m.get("uv")
            tex = m.get("tex") or {}
            rgba = tex.get("rgba") if tex else None
            cutoff = float(tex.get("alphaCutoff", 0.4)) if tex else 0.4
            amode = (tex.get("alphaMode") if tex else None) or m["alpha_mode"]
            base = np.array((tex.get("base") if tex else m["base"])[:3], dtype=np.float64)
            alpha = float((tex.get("base") if tex else m["base"])[3] if len((tex.get("base") if tex else m["base"])) > 3 else 1.0)
            name = (m["name"] + " " + m["node"] + " " + str((m.get("material") or {}).get("name") or "")).lower()
            if rgba is None:
                if any(s in name for s in ("leaf", "anubias", "java", "fern", "carpet", "pl_")):
                    base = np.array([0.28, 0.50, 0.22])
                elif any(s in name for s in ("rhizome", "wood", "wd_")):
                    base = np.array([0.40, 0.28, 0.16])
                elif any(s in name for s in ("fish", "fh_", "tetra")):
                    base = np.array([0.55, 0.62, 0.38])
            if nrm is None:
                nrm = np.zeros_like(pos); nrm[:, 1] = 1
            h = np.c_[pos, np.ones((len(pos), 1))]
            clip = (P @ (V @ h.T)).T
            w = clip[:, 3:4]; w[np.abs(w) < 1e-9] = 1e-9
            ndc = clip[:, :3] / w
            sx = (ndc[:, 0] * 0.5 + 0.5) * (width - 1)
            sy = (1.0 - (ndc[:, 1] * 0.5 + 0.5)) * (height - 1)
            sz = ndc[:, 2] * 0.5 + 0.5
            tris = idx.reshape(-1, 3)
            th, tw = (rgba.shape[0], rgba.shape[1]) if rgba is not None else (0, 0)
            for a, b, c in tris:
                x0, y0, z0 = sx[a], sy[a], sz[a]
                x1, y1, z1 = sx[b], sy[b], sz[b]
                x2, y2, z2 = sx[c], sy[c], sz[c]
                if min(z0, z1, z2) < 0 or max(z0, z1, z2) > 1:
                    continue
                minx, maxx = int(max(0, math.floor(min(x0, x1, x2)))), int(min(width - 1, math.ceil(max(x0, x1, x2))))
                miny, maxy = int(max(0, math.floor(min(y0, y1, y2)))), int(min(height - 1, math.ceil(max(y0, y1, y2))))
                if minx >= maxx or miny >= maxy:
                    continue
                area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
                if abs(area) < 1e-8:
                    continue
                n = (nrm[a] + nrm[b] + nrm[c])
                ln = np.linalg.norm(n)
                n = n / ln if ln > 1e-8 else np.array([0, 1, 0])
                ndot = float(np.clip(np.dot(n, L), 0, 1))
                shade = 0.22 + 0.78 * ndot
                invA = 1.0 / area
                ys = np.arange(miny, maxy + 1)
                xs = np.arange(minx, maxx + 1)
                if ys.size == 0 or xs.size == 0:
                    continue
                XX, YY = np.meshgrid(xs, ys)
                w0 = ((x1 - XX) * (y2 - YY) - (x2 - XX) * (y1 - YY)) * invA
                w1 = ((x2 - XX) * (y0 - YY) - (x0 - XX) * (y2 - YY)) * invA
                w2 = 1.0 - w0 - w1
                mask = (w0 >= -0.001) & (w1 >= -0.001) & (w2 >= -0.001)
                z = w0 * z0 + w1 * z1 + w2 * z2
                mask &= (z >= 0) & (z <= 1)
                col = np.clip(base * shade * 1.05, 0, 1)
                src_a = alpha
                if rgba is not None and uv is not None and np.any(mask):
                    uu = w0 * uv[a, 0] + w1 * uv[b, 0] + w2 * uv[c, 0]
                    vv = w0 * uv[a, 1] + w1 * uv[b, 1] + w2 * uv[c, 1]
                    # wrap
                    uu = np.mod(uu, 1.0); vv = np.mod(vv, 1.0)
                    px = np.clip((uu * (tw - 1)).astype(np.int32), 0, tw - 1)
                    py = np.clip(((1 - vv) * (th - 1)).astype(np.int32), 0, th - 1)
                    samp = rgba[py, px] / 255.0  # (H,W,4) via advanced index -> (n,4)? wait meshgrid
                    # samp shape follows XX
                    if amode == "MASK":
                        mask = mask & (samp[:, :, 3] >= cutoff)
                    src_a = np.where(mask, samp[:, :, 3] * alpha, 0)
                    col_map = np.clip(samp[:, :, :3] * shade * 1.05, 0, 1)
                else:
                    col_map = None
                    if amode == "MASK" and rgba is None:
                        src_a = 1.0
                if not np.any(mask):
                    continue
                yy = YY[mask].astype(np.int32)
                xx = XX[mask].astype(np.int32)
                zz = z[mask]
                if col_map is not None:
                    cols = col_map[mask]
                else:
                    cols = np.repeat(col[None, :], yy.size, axis=0)
                if do_blend:
                    closer = zz <= (zbuf[yy, xx] + 0.001)
                    yy, xx = yy[closer], xx[closer]
                    cols = cols[closer]
                    if yy.size:
                        sa = 0.35
                        img[yy, xx] = img[yy, xx] * (1 - sa) + cols * sa
                else:
                    closer = zz < zbuf[yy, xx]
                    yy, xx, zz, cols = yy[closer], xx[closer], zz[closer], cols[closer]
                    if yy.size:
                        zbuf[yy, xx] = zz
                        img[yy, xx] = cols

    draw_list(opaques, False)
    draw_list(blends, True)
    pix = (np.clip(img, 0, 1) * 255).astype(np.uint8)
    im = Image.fromarray(pix, "RGB")
    draw = ImageDraw.Draw(im)
    draw.rectangle([0, 0, width, 28], fill=(10, 12, 14))
    draw.text((8, 6), title or "", fill=(220, 225, 220))
    if caption:
        draw.rectangle([0, height - 36, width, height], fill=(10, 12, 14))
        draw.text((8, height - 32), caption[:180], fill=(180, 186, 180))
    return im
