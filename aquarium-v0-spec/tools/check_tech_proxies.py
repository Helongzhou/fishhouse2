#!/usr/bin/env python3
"""Check the technical-proxy GLBs and the Unity 2022 wiring. Does not run Unity."""
import csv
import json
import struct
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "art/tech-proxies-v0"
GLB = ART / "export/glb"
MODELS = ROOT / "unity/Assets/_Game/TechProxies/Models"
WIRED = {
    "fh_v0_tetra_proxy_L0.glb": ("ember_tetra", "swim_tail_loop", False),
    "pl_fg_v0_carpet_proxy.glb": ("plant_anubias_nana", "sway_loop", True),
    "pl_md_v0_leaf_proxy.glb": ("plant_java_fern", "sway_loop", True),
}

def fail(message):
    raise SystemExit("FAIL " + message)

def read_glb(path):
    data = path.read_bytes()
    if len(data) < 20 or data[:4] != b"glTF":
        fail(path.name + " is not a GLB")
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if version != 2 or length != len(data):
        fail(path.name + " GLB header does not match the file")
    offset = 12
    chunks = []
    while offset + 8 <= length:
        chunk_length, chunk_type = struct.unpack_from("<I4s", data, offset)
        chunks.append((chunk_type, data[offset + 8:offset + 8 + chunk_length]))
        offset += 8 + chunk_length
    if not chunks or chunks[0][0] != b"JSON":
        fail(path.name + " has no JSON chunk")
    document = json.loads(chunks[0][1])
    binary = b"".join(body for kind, body in chunks if kind == b"BIN\x00")
    return document, binary

def component_size(component_type):
    return {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}[component_type]

def element_count(accessor_type):
    return {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[accessor_type]

def accessor_bytes(document, binary, index):
    accessor = document["accessors"][index]
    view = document["bufferViews"][accessor["bufferView"]]
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    stride = view.get("byteStride")
    width = component_size(accessor["componentType"]) * element_count(accessor["type"])
    count = accessor["count"]
    if not stride or stride == width:
        return binary[start:start + width * count], accessor
    out = bytearray()
    for i in range(count):
        begin = start + i * stride
        out += binary[begin:begin + width]
    return bytes(out), accessor

def triangles(document):
    total = 0
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            if primitive.get("mode", 4) != 4:
                fail("non-triangle primitive")
            accessor = document["accessors"][primitive["indices"]]
            total += accessor["count"] // 3
    return total

def translation_targets(document):
    names = []
    nodes = document.get("nodes", [])
    for animation in document.get("animations", []):
        for channel in animation.get("channels", []):
            if channel["target"]["path"] == "translation":
                names.append(nodes[channel["target"]["node"]].get("name"))
    return names

def weight_sample(document, binary):
    samples = []
    for mesh in document.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            weights = primitive.get("attributes", {}).get("WEIGHTS_0")
            if weights is None:
                continue
            raw, accessor = accessor_bytes(document, binary, weights)
            if accessor["componentType"] != 5126 or accessor["type"] != "VEC4":
                fail("WEIGHTS_0 is not float VEC4")
            count = accessor["count"]
            picks = sorted({0, count // 2, count - 1})
            for pick in picks:
                values = struct.unpack_from("<4f", raw, pick * 16)
                samples.append(sum(values))
    return samples

def main():
    manifest_path = ART / "assets_manifest.csv"
    with manifest_path.open(encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if len(rows) != 10:
        fail("manifest row count " + str(len(rows)))
    checked = 0
    for row in rows:
        path = ART / row["file_glb"]
        if path.stat().st_size != int(row["bytes"]):
            fail(row["id"] + " byte count differs from the manifest")
        document, binary = read_glb(path)
        extras = document.get("asset", {}).get("extras", {})
        if extras.get("units") != "meter" or extras.get("not_final_art") is not True or extras.get("not_real_species") is not True:
            fail(row["id"] + " extras do not mark a meter technical proxy")
        if extras.get("root_motion") not in (False, None):
            fail(row["id"] + " declares root motion")
        if translation_targets(document):
            fail(row["id"] + " animates translation: " + ",".join(translation_targets(document)))
        if triangles(document) != int(row["tris"]):
            fail(row["id"] + " triangle count " + str(triangles(document)) + " != " + row["tris"])
        if row["skinned"] == "True":
            samples = weight_sample(document, binary)
            if not samples or any(abs(sample - 1) > 1e-3 for sample in samples):
                fail(row["id"] + " skin weights do not sum to 1")
        names = [animation.get("name") for animation in document.get("animations", [])]
        expected = [part for part in row["animations"].split(",") if part]
        if names != expected and not (row["animations"] == "" and names == []):
            # The assembled scene repeats sway_loop twice. Compare as a multiset of the manifest cell.
            manifest_names = [part for part in row["animations"].split(",") if part]
            if sorted(names) != sorted(manifest_names):
                fail(row["id"] + " animations " + str(names))
        checked += 1
    if checked != 10:
        fail("checked " + str(checked))

    for name, (binding, clip, plant) in WIRED.items():
        document, _binary = read_glb(GLB / name)
        animation_names = [animation.get("name") for animation in document.get("animations", [])]
        if clip not in animation_names:
            fail(name + " missing " + clip)
        mesh_material = None
        for mesh in document["meshes"]:
            for primitive in mesh["primitives"]:
                mesh_material = document["materials"][primitive["material"]]
        alpha = mesh_material.get("alphaMode", "OPAQUE")
        if plant and (alpha != "MASK" or mesh_material.get("doubleSided") is not True):
            fail(name + " leaf material is not double-sided MASK")
        if not plant and alpha != "OPAQUE":
            fail(name + " fish material is not opaque")
        art_hash = hashlib.sha256((GLB / name).read_bytes()).hexdigest()
        unity_hash = hashlib.sha256((MODELS / name).read_bytes()).hexdigest()
        if art_hash != unity_hash:
            fail(name + " Unity copy differs from the art pack")

    model_names = sorted(path.name for path in MODELS.iterdir() if path.suffix == ".glb")
    if model_names != sorted(WIRED):
        fail("Unity Models contains " + str(model_names))
    manifest = (ROOT / "unity/Packages/manifest.json").read_text(encoding="utf-8")
    if '"com.unity.cloud.gltfast": "6.10.1"' not in manifest:
        fail("Unity 2022 manifest is not pinned to glTFast 6.10.1")
    if "6.16.1" in manifest:
        fail("Unity 2022 manifest received the Unity 6 glTFast pin")
    build = (ROOT / "unity/ProjectSettings/EditorBuildSettings.asset").read_text(encoding="utf-8")
    if build.count("path:") != 1 or "Assets/_Game/Scenes/Aquarium.unity" not in build:
        fail("player build scene list changed")
    if "TechProxyValidation" in build:
        fail("validation scene was added to the player build")
    scene = (ROOT / "unity/Assets/_Game/Scenes/Aquarium.unity").read_text(encoding="utf-8")
    if "EnableTechnicalProxyVisuals" in scene:
        fail("player scene enables the technical proxy")
    source = (ROOT / "unity/Assets/_Game/UnityAdapter/TechProxyVisuals.cs").read_text(encoding="utf-8")
    for token in list(WIRED) + ["ember_tetra", "plant_anubias_nana", "plant_java_fern", "swim_tail_loop", "sway_loop"]:
        if token not in source:
            fail("adapter is missing " + token)
    entry = (ROOT / "unity/Assets/_Game/UnityAdapter/AquariumGameEntry.cs").read_text(encoding="utf-8")
    if "public bool EnableTechnicalProxyVisuals;" not in entry:
        fail("game entry has no default-off proxy switch")
    print("PASS technical proxy files: 10 GLB headers, triangles, skin samples and rotation-only clips")
    print("PASS Unity 2022 wiring: 3 L0 copies, glTFast 6.10.1 pin, player scene unchanged")
    print("NOT RUN Unity import, compile, Play, alpha clip, skinning or screenshots")

if __name__ == "__main__":
    main()
