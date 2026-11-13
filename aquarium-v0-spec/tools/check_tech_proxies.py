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
    "pl_v0_2_anubias_proxy.glb": ("plant_anubias_nana", "sway_loop", True),
    "pl_v0_2_java_fern_proxy.glb": ("plant_java_fern", "sway_loop", True),
}
PLANT_SHA = {
    "pl_v0_2_anubias_proxy.glb": "ec3d6cb8729f6a7ddb17ff4323d4d960a29eff45495e0ca8cdfde93bf043fdee",
    "pl_v0_2_java_fern_proxy.glb": "b7026e69ba2eea71f6f2fa5b608b4769c7ba1dfecf4f14c7d92fc87affeafd0c",
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

    sources = {
        "fh_v0_tetra_proxy_L0.glb": GLB,
        "pl_v0_2_anubias_proxy.glb": ROOT / "art/tech-proxies-v0.2/export/glb",
        "pl_v0_2_java_fern_proxy.glb": ROOT / "art/tech-proxies-v0.2/export/glb",
    }
    for name, (binding, clip, plant) in WIRED.items():
        source = sources[name] / name
        document, _binary = read_glb(source)
        animation_names = [animation.get("name") for animation in document.get("animations", [])]
        if clip not in animation_names:
            fail(name + " missing " + clip)
        if "turn_left" in animation_names and name.startswith("pl_"):
            fail(name + " contains turn_left")
        mesh_material = None
        for mesh in document["meshes"]:
            for primitive in mesh["primitives"]:
                mesh_material = document["materials"][primitive["material"]]
        alpha = mesh_material.get("alphaMode", "OPAQUE")
        if plant and (alpha != "MASK" or mesh_material.get("doubleSided") is not True):
            fail(name + " leaf material is not double-sided MASK")
        if not plant and alpha != "OPAQUE":
            fail(name + " fish material is not opaque")
        if name in PLANT_SHA and hashlib.sha256(source.read_bytes()).hexdigest() != PLANT_SHA[name]:
            fail(name + " hash differs from the v0.2 manifest")
        art_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        for copy_root in (MODELS, ROOT / "unity6-validation/Assets/_Game/TechProxies/Models"):
            if hashlib.sha256((copy_root / name).read_bytes()).hexdigest() != art_hash:
                fail(name + " Unity copy differs from the art pack")
    fern, _binary = read_glb(sources["pl_v0_2_java_fern_proxy.glb"] / "pl_v0_2_java_fern_proxy.glb")
    top = 0.0
    for mesh in fern["meshes"]:
        for primitive in mesh["primitives"]:
            accessor = fern["accessors"][primitive["attributes"]["POSITION"]]
            top = max(top, accessor["max"][1])
    over_cm = (top - 0.14) * 100
    if not (0.2 < over_cm < 0.5):
        fail("java fern height exception is not the measured 0.3 cm band: " + str(over_cm))

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
    for adapter in (
        ROOT / "unity/Assets/_Game/UnityAdapter/TechProxyVisuals.cs",
        ROOT / "unity6-validation/Assets/_Game/UnityAdapter/TechProxyVisuals.cs",
    ):
        source = adapter.read_text(encoding="utf-8")
        for token in list(WIRED) + ["ember_tetra", "plant_anubias_nana", "plant_java_fern", "swim_tail_loop", "sway_loop"]:
            if token not in source:
                fail(adapter.name + " is missing " + token)
        if "turn_left" in source or "pl_fg_v0_carpet_proxy" in source or "pl_md_v0_leaf_proxy" in source:
            fail(adapter.name + " still maps the old plant files or turn_left")
        if "0.14f" in source or "14.3" in source:
            fail(adapter.name + " scales a proxy toward the size cap")
    entry = (ROOT / "unity/Assets/_Game/UnityAdapter/AquariumGameEntry.cs").read_text(encoding="utf-8")
    if "public bool EnableTechnicalProxyVisuals;" not in entry:
        fail("game entry has no default-off proxy switch")
    print("PASS technical proxy files: v0 pack plus v0.2 plant hashes, triangles, skin samples and rotation-only clips")
    print("ACCEPTED FILE EXCEPTION java fern top " + format(top, ".6f") + " m, " + format(over_cm, ".3f") + " cm over 0.14 m; README 0.4 cm is not this measurement")
    print("PASS Unity 2022 and Unity 6 wiring use the same three file names; glTFast 6.10.1 pin unchanged; player scene unchanged")
    print("NOT RUN Unity import, compile, Play, alpha clip, double-sided mask, rhizome pivot, animation loop or screenshots")

if __name__ == "__main__":
    main()
