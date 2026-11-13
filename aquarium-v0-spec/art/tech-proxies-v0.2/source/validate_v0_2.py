#!/usr/bin/env python3
"""Independent re-audit of v0 mapped assets + validation of v0.2 plant proxies.

Reads live GLB bytes. Does not treat the old self_check.json as evidence.
Does not write into /workspace/aquarium-art-v0/.
Does not sign Unity URP visual approval.
"""
from __future__ import annotations

import hashlib
import io
import json
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC = Path(__file__).resolve().parent
V0 = Path("/workspace/aquarium-art-v0")
sys.path.insert(0, str(SRC))

from preview_lib import (
    accessor_numpy, aabb_of_meshes, attach_uv_textures, find_anim, frame_from_meshes,
    gather_posed_meshes, load_primitive, pygltf_counts, raster_textured, read_glb,
    sample_animation, walk_globals,
)

PREV = ROOT / "preview"
VAL = ROOT / "validation"
COMPONENT = {5120: ("<i1", 1), 5121: ("<u1", 1), 5122: ("<i2", 2), 5123: ("<u2", 2),
             5125: ("<u4", 4), 5126: ("<f4", 4)}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def png_info(doc, blob, image_index):
    img = doc["images"][image_index]
    bv = doc["bufferViews"][img["bufferView"]]
    raw = blob[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
    im = Image.open(io.BytesIO(raw)).convert("RGBA")
    a = np.array(im)[:, :, 3]
    return {
        "name": img.get("name"),
        "is_png": raw[:8] == b"\x89PNG\r\n\x1a\n",
        "size": list(im.size),
        "alpha_min": int(a.min()),
        "alpha_max": int(a.max()),
        "frac_a0": float((a == 0).mean()),
        "frac_a255": float((a == 255).mean()),
        "frac_a_lt_102": float((a < 102).mean()),  # ~0.4 cutoff
        "unique_alpha": int(len(np.unique(a))),
    }


def inspect_glb(path: Path) -> dict:
    doc, blob, meta = read_glb(path)
    pyg = pygltf_counts(path)
    mats = []
    for mat in doc.get("materials") or []:
        pbr = mat.get("pbrMetallicRoughness") or {}
        rec = {
            "name": mat.get("name"),
            "alphaMode": mat.get("alphaMode", "OPAQUE"),
            "alphaCutoff": mat.get("alphaCutoff"),
            "doubleSided": bool(mat.get("doubleSided", False)),
            "has_baseColorTexture": "baseColorTexture" in pbr,
            "has_normalTexture": "normalTexture" in mat,
            "has_mrTexture": "metallicRoughnessTexture" in pbr,
            "baseColorFactor": pbr.get("baseColorFactor"),
        }
        if "baseColorTexture" in pbr:
            tex = doc["textures"][pbr["baseColorTexture"]["index"]]
            rec["baseColor_tex"] = png_info(doc, blob, tex["source"])
        mats.append(rec)
    mesh_stats = []
    for mesh in doc.get("meshes") or []:
        for prim in mesh.get("primitives") or []:
            p = load_primitive(doc, blob, prim)
            pos = p["pos"].astype(np.float64)
            mn, mx = pos.min(0), pos.max(0)
            werr = None
            if p["weights"] is not None:
                werr = float(np.max(np.abs(p["weights"].astype(np.float64).sum(1) - 1.0)))
            mesh_stats.append({
                "name": mesh.get("name"),
                "verts": int(len(pos)),
                "tris": int(len(p["idx"]) // 3),
                "aabb_min": mn.tolist(), "aabb_max": mx.tolist(),
                "size_m": (mx - mn).tolist(),
                "has_uv": p["uv"] is not None,
                "has_skin": p["joints"] is not None,
                "weight_err_max": werr,
                "material_index": p["material"],
            })
    skins = []
    for skin in doc.get("skins") or []:
        skins.append({
            "name": skin.get("name"),
            "joints": [doc["nodes"][j].get("name") for j in skin.get("joints", [])],
        })
    anims = []
    movement = {}
    for anim in doc.get("animations") or []:
        paths = set()
        times_all = []
        chans = []
        has_translation = False
        for ch in anim.get("channels", []):
            chan_path = ch["target"]["path"]
            paths.add(chan_path)
            if chan_path == "translation":
                has_translation = True
            samp = anim["samplers"][ch["sampler"]]
            times = accessor_numpy(doc, blob, samp["input"]).reshape(-1).astype(np.float64)
            times_all.append(times)
            node = doc["nodes"][ch["target"]["node"]].get("name")
            chans.append({"node": node, "path": chan_path, "t0": float(times[0]), "t1": float(times[-1]),
                          "interpolation": samp.get("interpolation")})
        t0 = float(min(t[0] for t in times_all)) if times_all else 0
        t1 = float(max(t[-1] for t in times_all)) if times_all else 0
        loop_ok = True
        for ch in anim.get("channels", []):
            if ch["target"]["path"] != "rotation":
                continue
            samp = anim["samplers"][ch["sampler"]]
            rots = accessor_numpy(doc, blob, samp["output"]).astype(np.float64).reshape(-1, 4)

            def nq(q):
                return q / max(np.linalg.norm(q), 1e-12)
            if min(np.linalg.norm(nq(rots[0]) - nq(rots[-1])), np.linalg.norm(nq(rots[0]) + nq(rots[-1]))) > 0.05:
                loop_ok = False
        # vertex motion + region motion (tips vs base)
        def posed(tt, skip_blend=True):
            ov = sample_animation(doc, blob, anim, tt)
            ms, world = gather_posed_meshes(doc, blob, ov, skip_transparent=skip_blend)
            if not ms:
                ms, world = gather_posed_meshes(doc, blob, ov, skip_transparent=False)
            return ms, world
        m0, w0 = posed(t0)
        mq, wq = posed(t0 + 0.25 * max(t1 - t0, 1e-6))
        mid, _ = posed((t0 + t1) * 0.5)
        max_disp = 0.0
        mean_disp = 0.0
        by0 = {m["name"]: m["pos"] for m in m0}
        for m in mq:
            if m["name"] in by0 and len(by0[m["name"]]) == len(m["pos"]):
                d = np.linalg.norm(m["pos"] - by0[m["name"]], axis=1)
                max_disp = max(max_disp, float(d.max()))
                mean_disp = max(mean_disp, float(d.mean()))
        # root / wrapper translation in world of first joint if present
        root_delta = None
        anims.append({
            "name": anim.get("name"),
            "duration_s": t1 - t0,
            "paths": sorted(paths),
            "has_translation_channels": has_translation,
            "loop_keys_close": loop_ok,
            "channels": chans,
            "extras": anim.get("extras"),
            "vertex_motion": {"max_m": max_disp, "mean_m": mean_disp, "moved": max_disp > 1e-5},
        })
        movement[anim.get("name")] = anims[-1]["vertex_motion"]
    # bind aabb world
    meshes_bind, world = gather_posed_meshes(doc, blob, None)
    world_aabb = None
    if meshes_bind:
        mn, mx = aabb_of_meshes(meshes_bind)
        world_aabb = {"min": mn.tolist(), "max": mx.tolist(), "size_m": (mx - mn).tolist(),
                      "height_m": float(mx[1] - min(mn[1], 0.0))}
    return {
        "path": str(path),
        "file_name": path.name,
        "bytes": path.stat().st_size,
        "sha256": sha256(path),
        "pygltflib": pyg,
        "asset_extras": (doc.get("asset") or {}).get("extras", {}),
        "meshes": mesh_stats,
        "total_tris": int(sum(m["tris"] for m in mesh_stats)),
        "materials": mats,
        "skins": skins,
        "animations": anims,
        "world_aabb_bind": world_aabb,
        "doc": doc, "blob": blob,  # stripped later
    }


def sway_region_test(path: Path, anim_name="sway_loop"):
    """Compare displacement of low (rhizome) vs high (leaf tip) vertices. Not water physics."""
    doc, blob, _ = read_glb(path)
    anim = find_anim(doc, anim_name)
    if not anim:
        # prefixed clip in combined scene
        for a in doc.get("animations") or []:
            if a.get("name", "").endswith(anim_name) or anim_name in (a.get("name") or ""):
                anim = a
                break
    if not anim:
        return None
    times = None
    for ch in anim["channels"]:
        times = accessor_numpy(doc, blob, anim["samplers"][ch["sampler"]]["input"]).reshape(-1)
        break
    t0, t1 = float(times[0]), float(times[-1])
    def verts(t):
        ov = sample_animation(doc, blob, anim, t)
        ms, _ = gather_posed_meshes(doc, blob, ov, skip_transparent=False)
        return np.vstack([m["pos"] for m in ms])
    p0, pq = verts(t0), verts(t0 + 0.3 * (t1 - t0))
    # use bind y to split
    y = p0[:, 1]
    ycut_lo = np.quantile(y, 0.15)
    ycut_hi = np.quantile(y, 0.85)
    d = np.linalg.norm(pq - p0, axis=1)
    lo = float(d[y <= ycut_lo].mean()) if np.any(y <= ycut_lo) else 0
    hi = float(d[y >= ycut_hi].mean()) if np.any(y >= ycut_hi) else 0
    return {
        "low_y_mean_disp_m": lo,
        "high_y_mean_disp_m": hi,
        "tip_moves_more_than_base": hi > lo * 1.15,
        "note": "Sine bone flex sample, not a water-flow simulation.",
    }


def verdicts_from_v0_fish(rep):
    items = []
    size = rep["world_aabb_bind"]["size_m"]
    length = size[2]
    items.append({
        "id": "fish_proportion",
        "asset": "fh_v0_tetra_proxy_L0",
        "mapping": "ember_tetra (technical slot only)",
        "file_status": "已过",
        "engine_status": "待引擎验证",
        "detail": f"再导入体长 {length*100:.2f} cm（含尾鳍；extras 曾写 3.8 cm 为生成目标，实测更长）。相对 60 cm 缸比例合理。不是 Ember Tetra 物种还原。",
    })
    swim = next(a for a in rep["animations"] if a["name"] == "swim_tail_loop")
    items.append({
        "id": "fish_tail_wag",
        "asset": "fh_v0_tetra_proxy_L0",
        "file_status": "已过" if swim["vertex_motion"]["moved"] and swim["loop_keys_close"] and (not swim["has_translation_channels"]) else "不合格",
        "engine_status": "待引擎验证",
        "detail": f"8 骨链，仅 rotation，循环闭合，无根位移通道。t=0.5s 相对 t=0 最大顶点位移 {swim['vertex_motion']['max_m']*1000:.2f} mm。文件层证明摆尾存在；游戏观感待 URP。",
    })
    turn = next(a for a in rep["animations"] if a["name"] == "turn_left")
    items.append({
        "id": "fish_turn_semantics",
        "asset": "fh_v0_tetra_proxy_L0 / ember_tetra",
        "file_status": "有条件通过（文件层有原地弯曲 clip）；若当作空间转弯则不合格",
        "engine_status": "待引擎验证",
        "detail": (
            "turn_left 只有骨骼旋转通道，无 translation；首尾关键帧回到绑定姿势（loop_keys_close=True）。"
            f"中段最大顶点位移约 {turn['vertex_motion']['max_m']*1000:.1f} mm。"
            "这是「身体弯曲后回到原朝向」的原地姿态，不是世界空间航向改变，也不产生位移。"
            "玩法转向必须由逻辑旋转/移动根节点；不要把该 clip 当成完成了一次空间转弯。"
        ),
    })
    items.append({
        "id": "fish_species_mapping",
        "asset": "fh_v0_tetra_proxy_L0 -> ember_tetra",
        "file_status": "仅技术占位可接受，物种还原不合格（未声称）",
        "engine_status": "待引擎验证近景品种可读性",
        "detail": "资产 extras 已标明 technical_proxy / not_real_species。可作为 ember_tetra 技术槽验证蒙皮与 clip 名称，不能当作灯鱼品种造型通过。",
    })
    return items


def verdicts_from_v0_plants(carpet, midleaf):
    items = []
    cs = carpet["world_aabb_bind"]["size_m"]
    items.append({
        "id": "mapping_carpet_as_anubias",
        "asset": "pl_fg_v0_carpet_proxy -> plant_anubias_nana",
        "file_status": "不合格",
        "engine_status": "不要在引擎里按水榕验收此网格",
        "detail": (
            f"该网格是前景地毯丛：约 {cs[0]*100:.1f}×{cs[1]*100:.1f}×{cs[2]*100:.1f} cm，36 叶状细条、径向铺开，"
            "骨骼为 1 根 + 3 簇茎，无可辨识匍匐根茎、叶柄或厚实卵形叶。"
            "把地毯代理映射为 plant_anubias_nana 会误导形态。应停用该映射；改用 v0.2 pl_v0_2_anubias_proxy 做技术验证（仍非物种还原）。"
            "地毯网格可继续当「前景地毯占位」，不要叫水榕。"
        ),
    })
    # alpha on carpet — fields are correct even if mapping is wrong
    leaf_mat = next((m for m in carpet["materials"] if m["alphaMode"] == "MASK"), None)
    ok_alpha = (leaf_mat and leaf_mat["doubleSided"] and leaf_mat.get("baseColor_tex")
                and leaf_mat["baseColor_tex"]["is_png"]
                and leaf_mat["baseColor_tex"]["alpha_min"] == 0
                and leaf_mat["baseColor_tex"]["alpha_max"] == 255)
    items.append({
        "id": "v0_carpet_alphaclip_fields",
        "asset": "pl_fg_v0_carpet_proxy",
        "file_status": "已过（glTF 字段与 alpha 贴图）" if ok_alpha else "不合格",
        "engine_status": "待引擎验证 URP Alpha Clipping / 双面",
        "detail": f"alphaMode={leaf_mat['alphaMode'] if leaf_mat else None}, doubleSided={leaf_mat['doubleSided'] if leaf_mat else None}, tex={leaf_mat.get('baseColor_tex') if leaf_mat else None}。文件层 MASK+doubleSided+有效透明通道成立；URP 转换未测。",
    })
    ms = midleaf["world_aabb_bind"]["size_m"]
    items.append({
        "id": "mapping_midleaf_as_java_fern",
        "asset": "pl_md_v0_leaf_proxy -> plant_java_fern",
        "file_status": "不合格（形态不足以代表铁皇冠）",
        "engine_status": "不要按铁皇冠近景验收此网格",
        "detail": (
            f"尺寸约 {ms[0]*100:.1f}×{ms[1]*100:.1f}×{ms[2]*100:.1f} cm，高仅约 7.7 cm；叶片为偏卵形/阔叶剖面（生成宽度约 2.2–3.0 cm、长 4.2–6.2 cm），"
            "不是长披针叶。虽有短管状根茎痕迹，但不足以读成附生铁皇冠。4 骨簇状 sway，不是沿叶长的柔摆。"
            "技术槽可保留默认关闭；形态验收请改用 v0.2 pl_v0_2_java_fern_proxy。"
        ),
    })
    leaf_mat2 = next((m for m in midleaf["materials"] if m["alphaMode"] == "MASK"), None)
    ok2 = (leaf_mat2 and leaf_mat2["doubleSided"] and leaf_mat2.get("baseColor_tex")
           and leaf_mat2["baseColor_tex"]["alpha_min"] == 0 and leaf_mat2["baseColor_tex"]["alpha_max"] == 255)
    items.append({
        "id": "v0_midleaf_alphaclip_fields",
        "asset": "pl_md_v0_leaf_proxy",
        "file_status": "已过（glTF 字段与 alpha 贴图）" if ok2 else "不合格",
        "engine_status": "待引擎验证 URP Alpha Clipping / 双面",
        "detail": f"alphaMode={leaf_mat2['alphaMode'] if leaf_mat2 else None}, doubleSided={leaf_mat2['doubleSided'] if leaf_mat2 else None}, tex={leaf_mat2.get('baseColor_tex') if leaf_mat2 else None}。",
    })
    items.append({
        "id": "v0_plant_sway_generic",
        "asset": "both v0 plant proxies",
        "file_status": "已过（有循环旋转 sway，无根位移通道）",
        "engine_status": "待引擎验证",
        "detail": "sway_loop 为程序正弦骨骼旋转，不是水流物理。v0 草是整簇 3 茎骨带动，容易显得偏刚体。v0.2 改为叶柄/叶片分级相位。",
    })
    items.append({
        "id": "closeup_readability_v0",
        "asset": "v0 mapped trio",
        "file_status": "文件层仅确认有网格/UV/贴图，不签字",
        "engine_status": "待引擎验证（近景阅读未做）",
        "detail": "无 Unity 画面。软件预览不能代替近景侧纹、叶脉、透明叠加验收。",
    })
    return items


def verdicts_from_v02(anub, java, anub_sway, java_sway):
    items = []
    ah = anub["world_aabb_bind"]["height_m"]
    asz = anub["world_aabb_bind"]["size_m"]
    # not a carpet: height similar to footprint, leaf-scale not 13cm lawn of 36 blades
    not_carpet = asz[0] < 0.12 and asz[2] < 0.12 and ah >= 0.06
    # materials
    leaf = next((m for m in anub["materials"] if "leaf" in (m["name"] or "")), None)
    rz = next((m for m in anub["materials"] if "rhizome" in (m["name"] or "")), None)
    alpha_ok = (leaf and leaf["alphaMode"] == "MASK" and leaf["doubleSided"]
                and leaf.get("baseColor_tex") and leaf["baseColor_tex"]["alpha_min"] == 0
                and leaf["baseColor_tex"]["alpha_max"] == 255)
    morph_ok = not_carpet and any("rhizome" in (m["name"] or "") for m in anub["meshes"])
    items.append({
        "id": "v02_anubias_morphology",
        "asset": "pl_v0_2_anubias_proxy",
        "file_status": "已过（技术代理形态）" if morph_ok else "不合格",
        "engine_status": "待引擎验证近景可读；不签物种还原",
        "detail": (
            f"实测包围盒 {asz[0]*100:.1f}×{ah*100:.1f}×{asz[2]*100:.1f} cm（宽×高×深），高目标 6–10 cm，实测 {ah*100:.1f} cm。"
            "独立根茎网格 + 6 枚带叶柄的卵形叶，不是地毯。"
            "仍是程序化技术代理，不承诺水榕精模或物种还原。"
        ),
    })
    sway_ok = anub_sway and anub_sway["tip_moves_more_than_base"] and any(
        a["name"] == "sway_loop" and a["loop_keys_close"] and not a["has_translation_channels"] and a["vertex_motion"]["moved"]
        for a in anub["animations"]
    )
    items.append({
        "id": "v02_anubias_sway",
        "asset": "pl_v0_2_anubias_proxy",
        "file_status": "已过（文件层分级柔摆）" if sway_ok else "不合格",
        "engine_status": "待引擎验证；非水流物理",
        "detail": f"根/低处平均位移 {anub_sway['low_y_mean_disp_m']*1000 if anub_sway else None:.2f} mm，叶尖平均 {anub_sway['high_y_mean_disp_m']*1000 if anub_sway else None:.2f} mm。叶柄/叶片分骨、相位错开，根骨不转。这是正弦采样对比，不是流体解算。",
    })
    items.append({
        "id": "v02_anubias_alphaclip",
        "asset": "pl_v0_2_anubias_proxy",
        "file_status": "已过（glTF MASK + doubleSided + 有效 alpha）" if alpha_ok else "不合格",
        "engine_status": "待引擎验证 URP 双面 Alpha Clip",
        "detail": f"leaf mat={leaf}",
    })

    jh = java["world_aabb_bind"]["height_m"]
    jsz = java["world_aabb_bind"]["size_m"]
    # lanceolate: height should dominate width of a single-plant footprint; 14cm tall, ~8cm wide is ok
    java_morph = jh >= 0.10 and any("rhizome" in (m["name"] or "") for m in java["meshes"])
    jleaf = next((m for m in java["materials"] if "leaf" in (m["name"] or "")), None)
    jalpha = (jleaf and jleaf["alphaMode"] == "MASK" and jleaf["doubleSided"]
              and jleaf.get("baseColor_tex") and jleaf["baseColor_tex"]["alpha_min"] == 0
              and jleaf["baseColor_tex"]["alpha_max"] == 255)
    height_note = "略高于目标上限 14 cm" if jh > 0.140 else "落在目标 10–14 cm 内"
    items.append({
        "id": "v02_java_fern_morphology",
        "asset": "pl_v0_2_java_fern_proxy",
        "file_status": "已过（技术代理形态）" if java_morph else "不合格",
        "engine_status": "待引擎验证近景可读；不签物种还原",
        "detail": (
            f"实测 {jsz[0]*100:.1f}×{jh*100:.1f}×{jsz[2]*100:.1f} cm，高目标 10–14 cm，实测 {jh*100:.1f} cm（{height_note}）。"
            "附生向根茎 + 7 枚长披针叶（叶长目标 9.5–12.5 cm）。仍是技术代理，不是铁皇冠精模。"
        ),
    })
    jsway_ok = java_sway and java_sway["tip_moves_more_than_base"] and any(
        a["name"] == "sway_loop" and a["loop_keys_close"] and not a["has_translation_channels"] and a["vertex_motion"]["moved"]
        for a in java["animations"]
    )
    items.append({
        "id": "v02_java_fern_sway",
        "asset": "pl_v0_2_java_fern_proxy",
        "file_status": "已过（文件层分级柔摆）" if jsway_ok else "不合格",
        "engine_status": "待引擎验证；非水流物理",
        "detail": f"低处平均 {java_sway['low_y_mean_disp_m']*1000 if java_sway else None:.2f} mm，叶尖平均 {java_sway['high_y_mean_disp_m']*1000 if java_sway else None:.2f} mm。沿叶长分 a/b 骨，根茎不摆。",
    })
    items.append({
        "id": "v02_java_alphaclip",
        "asset": "pl_v0_2_java_fern_proxy",
        "file_status": "已过（glTF MASK + doubleSided + 有效 alpha）" if jalpha else "不合格",
        "engine_status": "待引擎验证 URP 双面 Alpha Clip",
        "detail": f"leaf mat={jleaf}",
    })
    items.append({
        "id": "unity_urp_visual",
        "asset": "all",
        "file_status": "未做，不签字",
        "engine_status": "未做",
        "detail": "本次无 Unity URP 实机画面。玻璃/水体/近景/植被透明叠加以引擎验收为准。软件光栅预览明确非引擎。",
    })
    items.append({
        "id": "license_source",
        "asset": "v0.2 new plants + copied fish reference",
        "file_status": "已过（来源声明准确，无排他权承诺）",
        "engine_status": "N/A",
        "detail": "新网格与贴图为本次原创程序化。第三方运行库：numpy 2.5.4 (BSD)、Pillow 13.0.0 (HPND)、pygltflib 1.16.5 (MIT)。不声称独家版权或商业素材授权。复制的鱼 GLB 与 v0 哈希一致，未改。",
    })
    return items


def render_jobs(cache):
    jobs = []
    def add(glb, anim, t, view, fov, fname, title, caption, eye=None, target=None, dist=2.5):
        jobs.append((glb, anim, t, view, fov, fname, title, caption, eye, target, dist))
    add("pl_v0_2_anubias_proxy.glb", "sway_loop", 0.0, "oblique", 32, "01_anubias_oblique_t0.png",
        "pl_v0_2_anubias_proxy t=0",
        "CPU raster + texture sample. NOT Unity URP. Technical proxy, not a real Anubias.")
    add("pl_v0_2_anubias_proxy.glb", "sway_loop", 1.5, "oblique", 32, "02_anubias_oblique_t1.5.png",
        "pl_v0_2_anubias_proxy t=1.5s",
        "Motion sample only, not water physics. Compare leaf lean to t=0.")
    add("pl_v0_2_anubias_proxy.glb", "sway_loop", 0.0, "front", 30, "03_anubias_front_rhizome.png",
        "anubias proxy · front, show rhizome",
        "Look for creeping rhizome and petioles. Software raster only.", dist=2.2)
    add("pl_v0_2_anubias_proxy.glb", "sway_loop", 0.0, "side", 28, "04_anubias_near_leaves.png",
        "anubias proxy · closer side",
        "Near-view software raster; does not prove engine close-up LOD.", dist=1.8)
    add("pl_v0_2_java_fern_proxy.glb", "sway_loop", 0.0, "oblique", 32, "05_java_fern_oblique_t0.png",
        "pl_v0_2_java_fern_proxy t=0",
        "CPU raster. Technical proxy, not a real Java fern.")
    add("pl_v0_2_java_fern_proxy.glb", "sway_loop", 1.75, "oblique", 32, "06_java_fern_oblique_t1.75.png",
        "pl_v0_2_java_fern_proxy t=1.75s",
        "Sine flex sample, not water physics.")
    add("pl_v0_2_java_fern_proxy.glb", "sway_loop", 0.0, "front", 30, "07_java_fern_front_rhizome.png",
        "java fern proxy · front, rhizome",
        "Epiphytic rhizome + lanceolate leaves. Software raster.", dist=2.1)
    add("pl_v0_2_java_fern_proxy.glb", "sway_loop", 0.0, "side", 28, "08_java_fern_near.png",
        "java fern proxy · closer side",
        "Near-view software raster only.", dist=1.7)
    add("scn_v0_2_two_plants_proxy.glb", None, 0.0, "front", 32, "09_two_plants_front.png",
        "v0.2 two plants · front",
        "Left Anubias proxy, right Java fern proxy. Not a tank, not URP.", dist=2.4)
    add("scn_v0_2_two_plants_proxy.glb", None, 0.0, "oblique", 35, "10_two_plants_oblique.png",
        "v0.2 two plants · oblique",
        "CPU raster, not Unity URP.", dist=2.3)
    add("ref_unchanged_from_v0/fh_v0_tetra_proxy_L0.glb", "turn_left", 0.0, "side", 30, "11_fish_turn_t0.png",
        "unchanged v0 fish turn_left t=0",
        "Reference copy, sha256 matches v0. Side view.")
    add("ref_unchanged_from_v0/fh_v0_tetra_proxy_L0.glb", "turn_left", 0.75, "oblique", 32, "12_fish_turn_t0.75.png",
        "unchanged v0 fish turn_left t=0.75s",
        "In-place bone bend; heading returns to bind at clip end. Not a spatial turn.")
    add("ref_unchanged_from_v0/fh_v0_tetra_proxy_L0.glb", "turn_left", 1.5, "side", 30, "13_fish_turn_t1.5.png",
        "unchanged v0 fish turn_left t=1.5s (end=bind)",
        "Should match t=0 heading. Confirms loop-back, not a new world heading.")
    index = []
    for glb, anim_name, t, view, fov, fname, title, caption, eye, target, dist in jobs:
        if glb not in cache:
            print("skip missing", glb)
            continue
        pack = cache[glb]
        doc, blob = pack["doc"], pack["blob"]
        ov = None
        if anim_name:
            anim = find_anim(doc, anim_name)
            if anim:
                ov = sample_animation(doc, blob, anim, t)
        meshes, _ = gather_posed_meshes(doc, blob, ov, skip_transparent=False)
        meshes = attach_uv_textures(meshes, doc, blob)
        if view and meshes:
            eye, target = frame_from_meshes(meshes, view=view, dist_scale=dist)
        im = raster_textured(meshes, eye, target, (0, 1, 0), width=800, height=450, fov_deg=fov,
                             title=title, caption=caption)
        out = PREV / fname
        im.save(out, "PNG")
        index.append({"file": str(out), "source_glb": glb, "animation": anim_name, "t": t,
                      "bytes": out.stat().st_size, "renderer": "cpu_software_zbuffer_textured",
                      "not_unity_urp": True, "not_water_physics": True})
        print("  wrote", out.name, out.stat().st_size)
    # contact sheets
    def sheet(names, dest, kind):
        ims = [Image.open(PREV / n) for n in names if (PREV / n).exists()]
        if not ims:
            return
        w, h = ims[0].size
        canvas = Image.new("RGB", (w * len(ims), h), (10, 12, 14))
        x = 0
        for im in ims:
            canvas.paste(im, (x, 0)); x += w
        canvas.save(PREV / dest)
        index.append({"file": str(PREV / dest), "kind": kind, "bytes": (PREV / dest).stat().st_size,
                      "not_unity_urp": True})
    sheet(["01_anubias_oblique_t0.png", "02_anubias_oblique_t1.5.png"], "14_contact_anubias_sway.png", "motion_contact")
    sheet(["05_java_fern_oblique_t0.png", "06_java_fern_oblique_t1.75.png"], "15_contact_java_fern_sway.png", "motion_contact")
    sheet(["11_fish_turn_t0.png", "12_fish_turn_t0.75.png", "13_fish_turn_t1.5.png"], "16_contact_fish_turn_loopback.png", "turn_semantics")
    return index


def baseline_still_intact():
    base = {}
    for line in (ROOT / "docs" / "v0_baseline_sha256.txt").read_text().splitlines()[1:]:
        parts = line.split()
        if len(parts) >= 3:
            base[parts[2]] = (parts[0], int(parts[1]))
    mismatches = []
    missing = []
    for rel, (h, s) in base.items():
        p = V0 / rel
        if not p.exists():
            missing.append(rel)
            continue
        now = sha256(p)
        if now != h or p.stat().st_size != s:
            mismatches.append({"path": rel, "baseline": h, "now": now, "size0": s, "size1": p.stat().st_size})
    return {
        "checked": len(base),
        "mismatches": mismatches,
        "missing": missing,
        "unchanged": len(mismatches) == 0 and len(missing) == 0,
    }


def strip_doc(rep):
    d = {k: v for k, v in rep.items() if k not in ("doc", "blob")}
    return d


def main():
    PREV.mkdir(parents=True, exist_ok=True)
    VAL.mkdir(parents=True, exist_ok=True)

    print("Re-inspecting v0 mapped GLBs (read-only)...")
    fish = inspect_glb(V0 / "export/glb/fh_v0_tetra_proxy_L0.glb")
    carpet = inspect_glb(V0 / "export/glb/pl_fg_v0_carpet_proxy.glb")
    midleaf = inspect_glb(V0 / "export/glb/pl_md_v0_leaf_proxy.glb")
    print("Inspecting v0.2 plants...")
    anub = inspect_glb(ROOT / "export/glb/pl_v0_2_anubias_proxy.glb")
    java = inspect_glb(ROOT / "export/glb/pl_v0_2_java_fern_proxy.glb")
    scn = inspect_glb(ROOT / "export/glb/scn_v0_2_two_plants_proxy.glb")
    fish_copy = inspect_glb(ROOT / "export/glb/ref_unchanged_from_v0/fh_v0_tetra_proxy_L0.glb")
    print("  fish copy hash match:", fish_copy["sha256"] == fish["sha256"])

    anub_sway = sway_region_test(ROOT / "export/glb/pl_v0_2_anubias_proxy.glb")
    java_sway = sway_region_test(ROOT / "export/glb/pl_v0_2_java_fern_proxy.glb")
    print("anubias region sway", anub_sway)
    print("java region sway", java_sway)

    items = []
    items += verdicts_from_v0_fish(fish)
    items += verdicts_from_v0_plants(carpet, midleaf)
    items += verdicts_from_v02(anub, java, anub_sway, java_sway)

    print("Rendering previews...")
    cache = {
        "pl_v0_2_anubias_proxy.glb": anub,
        "pl_v0_2_java_fern_proxy.glb": java,
        "scn_v0_2_two_plants_proxy.glb": scn,
        "ref_unchanged_from_v0/fh_v0_tetra_proxy_L0.glb": fish_copy,
    }
    preview_index = render_jobs(cache)

    intact = baseline_still_intact()
    print("v0 baseline intact:", intact)

    summary = {
        "timezone": "Asia/Shanghai (UTC+8)",
        "loader": "independent GLB re-parse + pygltflib; not a copy of v0 self_check.json conclusions",
        "unity_urp": "NOT_RUN",
        "v0_first_batch_hashes": intact,
        "v0_reinspect": {
            "fh_v0_tetra_proxy_L0": strip_doc(fish),
            "pl_fg_v0_carpet_proxy": strip_doc(carpet),
            "pl_md_v0_leaf_proxy": strip_doc(midleaf),
        },
        "v02_inspect": {
            "pl_v0_2_anubias_proxy": strip_doc(anub),
            "pl_v0_2_java_fern_proxy": strip_doc(java),
            "scn_v0_2_two_plants_proxy": strip_doc(scn),
            "ref_unchanged_fh_v0_tetra_proxy_L0": {
                "sha256": fish_copy["sha256"],
                "matches_v0": fish_copy["sha256"] == fish["sha256"],
                "bytes": fish_copy["bytes"],
            },
        },
        "sway_region_samples": {"anubias": anub_sway, "java_fern": java_sway},
        "acceptance_items": items,
        "preview_index": preview_index,
        "explicitly_not_passed": [
            "Unity URP import and visuals were not run and are not signed off.",
            "Species reconstruction is not claimed and not passed.",
            "pl_fg_v0_carpet_proxy as plant_anubias_nana is unqualified.",
            "pl_md_v0_leaf_proxy as plant_java_fern silhouette is unqualified.",
            "turn_left is in-place bone bend returning to bind, not a spatial turn.",
            "Plant sway samples are not water-physics simulations.",
        ],
    }
    out = VAL / "self_check.json"
    out.write_text(json.dumps(summary, indent=2, ensure_ascii=False))
    print("wrote", out, "items", len(items))
    for it in items:
        print(f"  [{it['file_status']}] {it['id']}: {it['asset']}")
    return 0 if intact["unchanged"] else 2


if __name__ == "__main__":
    sys.exit(main())
