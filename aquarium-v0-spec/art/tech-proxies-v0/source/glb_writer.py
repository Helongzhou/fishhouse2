#!/usr/bin/env python3
"""Minimal glTF 2.0 / GLB writer. Units: meters. Convention: Y-up, +Z forward, right-handed."""
from __future__ import annotations

import json
import struct
from dataclasses import dataclass, field
from typing import Any

import numpy as np
from PIL import Image

ARRAY_BUFFER = 34962
ELEMENT_ARRAY_BUFFER = 34963
FLOAT = 5126
UNSIGNED_SHORT = 5123
UNSIGNED_INT = 5125
UNSIGNED_BYTE = 5121
VEC2, VEC3, VEC4, MAT4, SCALAR = "VEC2", "VEC3", "VEC4", "MAT4", "SCALAR"
COMPONENT_BYTES = {FLOAT: 4, UNSIGNED_SHORT: 2, UNSIGNED_INT: 4, UNSIGNED_BYTE: 1}
TYPE_COUNT = {SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4, MAT4: 16}


def _pad4(n: int) -> int:
    return (4 - (n % 4)) % 4


def q_normalize(q: np.ndarray) -> np.ndarray:
    q = np.asarray(q, dtype=np.float64)
    n = np.linalg.norm(q)
    return q / n if n > 1e-12 else np.array([0, 0, 0, 1], dtype=np.float64)


def q_from_axis_angle(axis, angle: float) -> np.ndarray:
    axis = np.asarray(axis, dtype=np.float64)
    n = np.linalg.norm(axis)
    if n < 1e-12 or abs(angle) < 1e-12:
        return np.array([0, 0, 0, 1], dtype=np.float64)
    axis = axis / n
    s = np.sin(angle * 0.5)
    return q_normalize(np.array([axis[0] * s, axis[1] * s, axis[2] * s, np.cos(angle * 0.5)]))


def q_mul(a, b) -> np.ndarray:
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return q_normalize(np.array([
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    ]))


def q_to_mat4(q, t=(0, 0, 0), s=(1, 1, 1)) -> np.ndarray:
    x, y, z, w = q_normalize(q)
    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z
    m = np.eye(4, dtype=np.float64)
    m[0, 0] = 1 - 2 * (yy + zz)
    m[0, 1] = 2 * (xy - wz)
    m[0, 2] = 2 * (xz + wy)
    m[1, 0] = 2 * (xy + wz)
    m[1, 1] = 1 - 2 * (xx + zz)
    m[1, 2] = 2 * (yz - wx)
    m[2, 0] = 2 * (xz - wy)
    m[2, 1] = 2 * (yz + wx)
    m[2, 2] = 1 - 2 * (xx + yy)
    m[0:3, 0] *= s[0]
    m[0:3, 1] *= s[1]
    m[0:3, 2] *= s[2]
    m[0, 3], m[1, 3], m[2, 3] = t
    return m


def mat4_trs(t, q=(0, 0, 0, 1), s=(1, 1, 1)) -> np.ndarray:
    return q_to_mat4(q, t, s)


@dataclass
class MeshData:
    name: str
    positions: np.ndarray
    normals: np.ndarray
    uvs: np.ndarray
    indices: np.ndarray
    material: str
    joints: np.ndarray | None = None
    weights: np.ndarray | None = None
    tangents: np.ndarray | None = None


@dataclass
class NodeSpec:
    name: str
    translation: tuple[float, float, float] = (0, 0, 0)
    rotation: tuple[float, float, float, float] = (0, 0, 0, 1)
    scale: tuple[float, float, float] = (1, 1, 1)
    children: list[int] = field(default_factory=list)
    mesh: int | None = None
    skin: int | None = None
    extras: dict | None = None


class GLBWriter:
    def __init__(self, extras: dict | None = None):
        self.bin = bytearray()
        self.buffer_views: list[dict] = []
        self.accessors: list[dict] = []
        self.images: list[dict] = []
        self.samplers = [{
            "magFilter": 9729,
            "minFilter": 9987,
            "wrapS": 10497,
            "wrapT": 10497,
        }]
        self.textures: list[dict] = []
        self.materials: list[dict] = []
        self.meshes: list[dict] = []
        self.nodes: list[dict] = []
        self.skins: list[dict] = []
        self.animations: list[dict] = []
        self.scenes: list[dict] = []
        self.extras = extras or {}
        self.mat_index: dict[str, int] = {}
        self.tex_index: dict[str, int] = {}

    def _add_bytes(self, data: bytes, target: int | None = None, stride: int | None = None) -> int:
        pad = _pad4(len(self.bin))
        if pad:
            self.bin.extend(b"\x00" * pad)
        offset = len(self.bin)
        self.bin.extend(data)
        bv: dict[str, Any] = {"buffer": 0, "byteOffset": offset, "byteLength": len(data)}
        if target is not None:
            bv["target"] = target
        if stride is not None:
            bv["byteStride"] = stride
        self.buffer_views.append(bv)
        return len(self.buffer_views) - 1

    def add_accessor(self, array: np.ndarray, typ: str, component: int, target: int | None, normalized=False) -> int:
        array = np.ascontiguousarray(array)
        if component == FLOAT:
            blob = array.astype("<f4").tobytes()
        elif component == UNSIGNED_SHORT:
            blob = array.astype("<u2").tobytes()
        elif component == UNSIGNED_INT:
            blob = array.astype("<u4").tobytes()
        elif component == UNSIGNED_BYTE:
            blob = array.astype("<u1").tobytes()
        else:
            raise ValueError(component)
        view = self._add_bytes(blob, target)
        count = int(array.shape[0])
        acc: dict[str, Any] = {
            "bufferView": view,
            "componentType": component,
            "count": count,
            "type": typ,
        }
        if normalized:
            acc["normalized"] = True
        flat = array.astype(np.float64).reshape(count, -1)
        # min/max required for POSITION; useful for all
        acc["min"] = [float(x) for x in np.min(flat, axis=0)]
        acc["max"] = [float(x) for x in np.max(flat, axis=0)]
        self.accessors.append(acc)
        return len(self.accessors) - 1

    def add_png_texture(self, name: str, image: Image.Image, srgb: bool = True) -> int:
        if name in self.tex_index:
            return self.tex_index[name]
        import io
        buf = io.BytesIO()
        img = image.convert("RGBA") if image.mode != "RGBA" else image
        img.save(buf, format="PNG", optimize=True)
        view = self._add_bytes(buf.getvalue(), target=None)
        self.images.append({"name": name, "mimeType": "image/png", "bufferView": view})
        img_i = len(self.images) - 1
        self.textures.append({"name": name, "sampler": 0, "source": img_i})
        idx = len(self.textures) - 1
        self.tex_index[name] = idx
        return idx

    def add_material(self, name: str, *, base_color=(1, 1, 1, 1), metallic=0.0, roughness=0.7,
                     base_color_tex: str | None = None, normal_tex: str | None = None,
                     orm_tex: str | None = None, alpha_mode="OPAQUE", alpha_cutoff=0.4,
                     double_sided=False, emissive=(0, 0, 0), transmission: float | None = None) -> int:
        if name in self.mat_index:
            return self.mat_index[name]
        pbr: dict[str, Any] = {
            "baseColorFactor": [float(c) for c in base_color],
            "metallicFactor": float(metallic),
            "roughnessFactor": float(roughness),
        }
        if base_color_tex is not None:
            pbr["baseColorTexture"] = {"index": self.tex_index[base_color_tex]}
        if orm_tex is not None:
            pbr["metallicRoughnessTexture"] = {"index": self.tex_index[orm_tex]}
        mat: dict[str, Any] = {"name": name, "pbrMetallicRoughness": pbr, "emissiveFactor": list(emissive)}
        if normal_tex is not None:
            mat["normalTexture"] = {"index": self.tex_index[normal_tex], "scale": 1.0}
        if alpha_mode != "OPAQUE":
            mat["alphaMode"] = alpha_mode
            if alpha_mode == "MASK":
                mat["alphaCutoff"] = float(alpha_cutoff)
        if double_sided:
            mat["doubleSided"] = True
        if transmission is not None:
            mat["extensions"] = {"KHR_materials_transmission": {"transmissionFactor": float(transmission)}}
        self.materials.append(mat)
        self.mat_index[name] = len(self.materials) - 1
        return self.mat_index[name]

    def add_mesh(self, mesh: MeshData) -> int:
        attrs: dict[str, int] = {
            "POSITION": self.add_accessor(mesh.positions, VEC3, FLOAT, ARRAY_BUFFER),
            "NORMAL": self.add_accessor(mesh.normals, VEC3, FLOAT, ARRAY_BUFFER),
            "TEXCOORD_0": self.add_accessor(mesh.uvs, VEC2, FLOAT, ARRAY_BUFFER),
        }
        if mesh.tangents is not None:
            attrs["TANGENT"] = self.add_accessor(mesh.tangents, VEC4, FLOAT, ARRAY_BUFFER)
        if mesh.joints is not None and mesh.weights is not None:
            attrs["JOINTS_0"] = self.add_accessor(mesh.joints, VEC4, UNSIGNED_SHORT, ARRAY_BUFFER)
            attrs["WEIGHTS_0"] = self.add_accessor(mesh.weights, VEC4, FLOAT, ARRAY_BUFFER)
        if mesh.indices.max() < 65535:
            idx_acc = self.add_accessor(mesh.indices.astype(np.uint16), SCALAR, UNSIGNED_SHORT, ELEMENT_ARRAY_BUFFER)
        else:
            idx_acc = self.add_accessor(mesh.indices.astype(np.uint32), SCALAR, UNSIGNED_INT, ELEMENT_ARRAY_BUFFER)
        prim: dict[str, Any] = {
            "attributes": attrs,
            "indices": idx_acc,
            "material": self.mat_index[mesh.material],
            "mode": 4,
        }
        self.meshes.append({"name": mesh.name, "primitives": [prim]})
        return len(self.meshes) - 1

    def add_node(self, spec: NodeSpec) -> int:
        node: dict[str, Any] = {"name": spec.name}
        if spec.translation != (0, 0, 0):
            node["translation"] = [float(x) for x in spec.translation]
        q = q_normalize(np.array(spec.rotation))
        if np.linalg.norm(q - np.array([0, 0, 0, 1])) > 1e-8:
            node["rotation"] = [float(x) for x in q]
        if spec.scale != (1, 1, 1):
            node["scale"] = [float(x) for x in spec.scale]
        if spec.children:
            node["children"] = list(spec.children)
        if spec.mesh is not None:
            node["mesh"] = spec.mesh
        if spec.skin is not None:
            node["skin"] = spec.skin
        if spec.extras:
            node["extras"] = spec.extras
        self.nodes.append(node)
        return len(self.nodes) - 1

    def set_node_children(self, idx: int, children: list[int]):
        if children:
            self.nodes[idx]["children"] = list(children)
        elif "children" in self.nodes[idx]:
            del self.nodes[idx]["children"]

    def add_skin(self, name: str, joint_node_indices: list[int], ibm: np.ndarray, skeleton: int | None = None) -> int:
        # ibm: (J, 4, 4) row-major math, stored column-major in glTF
        packed = np.transpose(ibm, (0, 2, 1)).astype("<f4").reshape(len(joint_node_indices), 16)
        acc = self.add_accessor(packed, MAT4, FLOAT, None)
        skin: dict[str, Any] = {
            "name": name,
            "joints": list(joint_node_indices),
            "inverseBindMatrices": acc,
        }
        if skeleton is not None:
            skin["skeleton"] = skeleton
        self.skins.append(skin)
        return len(self.skins) - 1

    def add_animation(self, name: str, channels: list[dict], samplers: list[dict], extras=None) -> int:
        anim: dict[str, Any] = {"name": name, "samplers": samplers, "channels": channels}
        if extras:
            anim["extras"] = extras
        self.animations.append(anim)
        return len(self.animations) - 1

    def add_rotation_clip(self, name: str, times: np.ndarray, node_rotations: dict[int, np.ndarray],
                          extras=None, loop=True) -> int:
        """node_rotations: node_index -> (F, 4) quaternions xyzw."""
        times = np.ascontiguousarray(times, dtype=np.float32)
        t_acc = self.add_accessor(times, SCALAR, FLOAT, None)
        samplers = []
        channels = []
        for node, rots in node_rotations.items():
            rots = np.ascontiguousarray(rots, dtype=np.float32)
            # ensure unit quats, same hemisphere as previous for lerp
            for i in range(len(rots)):
                rots[i] = q_normalize(rots[i])
                if i > 0 and np.dot(rots[i], rots[i - 1]) < 0:
                    rots[i] = -rots[i]
            r_acc = self.add_accessor(rots, VEC4, FLOAT, None)
            sidx = len(samplers)
            samplers.append({"input": t_acc, "output": r_acc, "interpolation": "LINEAR"})
            channels.append({"sampler": sidx, "target": {"node": int(node), "path": "rotation"}})
        return self.add_animation(name, channels, samplers, extras=extras)

    def finish_scene(self, name: str, root_nodes: list[int]):
        self.scenes.append({"name": name, "nodes": list(root_nodes)})

    def save(self, path: str, scene_name: str = "Scene", roots: list[int] | None = None):
        if not self.scenes:
            if roots is None:
                roots = list(range(len(self.nodes)))
            self.finish_scene(scene_name, roots)
        pad = _pad4(len(self.bin))
        if pad:
            self.bin.extend(b"\x00" * pad)
        gltf: dict[str, Any] = {
            "asset": {
                "version": "2.0",
                "generator": "aquarium-art-v0-procedural (technical proxy, not final art)",
                "extras": self.extras,
            },
            "scene": 0,
            "scenes": self.scenes,
            "nodes": self.nodes,
            "meshes": self.meshes,
            "materials": self.materials,
            "accessors": self.accessors,
            "bufferViews": self.buffer_views,
            "buffers": [{"byteLength": len(self.bin)}],
        }
        if self.skins:
            gltf["skins"] = self.skins
        if self.animations:
            gltf["animations"] = self.animations
        if self.images:
            gltf["images"] = self.images
        if self.textures:
            gltf["textures"] = self.textures
        if self.samplers:
            gltf["samplers"] = self.samplers
        used_ext = set()
        for mat in self.materials:
            used_ext.update((mat.get("extensions") or {}).keys())
        if used_ext:
            gltf["extensionsUsed"] = sorted(used_ext)
            # transmission is optional; do not put in extensionsRequired
        json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
        json_bytes += b" " * _pad4(len(json_bytes))
        chunk_json = struct.pack("<I", len(json_bytes)) + b"JSON" + json_bytes
        chunk_bin = struct.pack("<I", len(self.bin)) + b"BIN\x00" + bytes(self.bin)
        total = 12 + len(chunk_json) + len(chunk_bin)
        header = b"glTF" + struct.pack("<II", 2, total)
        Path = __import__("pathlib").Path
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        Path(path).write_bytes(header + chunk_json + chunk_bin)
        return path, total
