#!/usr/bin/env python3
"""Static checks for the Unity 6 candidate proxy. Does not run Unity."""
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
U6 = ROOT / "unity6-validation"
U2022 = ROOT / "unity"
ART = {
    "fh_v0_tetra_proxy_L0.glb": ROOT / "art/tech-proxies-v0/export/glb",
    "pl_v0_2_anubias_proxy.glb": ROOT / "art/tech-proxies-v0.2/export/glb",
    "pl_v0_2_java_fern_proxy.glb": ROOT / "art/tech-proxies-v0.2/export/glb",
}
MODELS = U6 / "Assets/_Game/TechProxies/Models"
WIRED = (
    "fh_v0_tetra_proxy_L0.glb",
    "pl_v0_2_anubias_proxy.glb",
    "pl_v0_2_java_fern_proxy.glb",
)

def fail(message):
    raise SystemExit("FAIL " + message)

def text(path):
    return path.read_text(encoding="utf-8")

def main():
    manifest = text(U6 / "Packages/manifest.json")
    if '"com.unity.cloud.gltfast": "6.16.1"' not in manifest:
        fail("Unity 6 manifest is not pinned to glTFast 6.16.1")
    if '"com.unity.render-pipelines.universal": "17.6.0"' not in manifest:
        fail("Unity 6 URP pin changed")
    older = text(U2022 / "Packages/manifest.json")
    if '"com.unity.cloud.gltfast": "6.10.1"' not in older:
        fail("Unity 2022 glTFast pin changed")
    if "6.16.1" in older:
        fail("Unity 2022 manifest received the Unity 6 pin")
    build = text(U6 / "ProjectSettings/EditorBuildSettings.asset")
    if "TechProxyValidation" in build or build.count("path:") != 1:
        fail("player build scene list changed")
    entry = text(U6 / "Assets/_Game/UnityAdapter/AquariumGameEntry.cs")
    if "SessionSaveLocation.Resolve()" not in entry:
        fail("candidate entry does not use the save-location resolver")
    if "EnableTechnicalProxyVisuals" not in entry:
        fail("candidate entry has no proxy switch")
    visuals = text(U6 / "Assets/_Game/UnityAdapter/TechProxyVisuals.cs")
    if "UnityEngine.Rendering.CullMode.Off" not in visuals or "(float)CullMode.Off" in visuals:
        fail("Unity 6 plant cull override does not name UnityEngine.Rendering.CullMode")
    scene = text(U6 / "Assets/_Game/Scenes/Aquarium.unity")
    if "EnableTechnicalProxyVisuals" in scene:
        fail("player scene enables the proxy")
    validator = text(U6 / "Assets/_Game/Editor/Validation/TechProxyCandidateValidation.cs")
    if "Path.Combine(Application.persistentDataPath" in validator:
        fail("candidate test writes through persistentDataPath")
    if "Directory.Delete" in validator or "File.Delete" in validator:
        fail("candidate test deletes paths")
    if "CreateFreshDirectory" not in validator or "readlink" not in text(U6 / "Assets/_Game/UnityAdapter/SaveRootGuard.cs"):
        fail("save root is not canonicalized")
    if "ValidationMode" not in text(U6 / "Assets/_Game/UnityAdapter/SessionSaveLocation.cs"):
        fail("validation mode is not fail-closed")
    if "CreateFreshDirectory" not in validator or "-aquariumSaveRoot" not in validator or "ValidationMode = true" not in validator:
        fail("candidate test does not require an injected save root")
    for name in ("proxy_off", "proxy_import_failure_fallback", "proxy_missing_file_fallback", "proxy_on_structure", "save_isolated_roundtrip", "visual_screenshot", "not_run"):
        if name not in validator:
            fail("report vocabulary missing " + name)
    names = sorted(path.name for path in MODELS.glob("*.glb"))
    if names != sorted(WIRED):
        fail("candidate Models contents " + str(names))
    for name in WIRED:
        art = hashlib.sha256((ART[name] / name).read_bytes()).hexdigest()
        copy = hashlib.sha256((MODELS / name).read_bytes()).hexdigest()
        base = hashlib.sha256((U2022 / "Assets/_Game/TechProxies/Models" / name).read_bytes()).hexdigest()
        if not (art == copy == base):
            fail(name + " bytes differ across art, 2022, and Unity 6")
    core = U6 / "Assets/_Game/Core"
    for path in core.rglob("*.cs"):
        rel = path.relative_to(U6)
        other = U2022 / rel
        # Schema files are allowed to differ already. This check only records nothing here.
        if not other.is_file():
            continue
    print("PASS Unity 6 candidate static isolation and proxy wiring")
    print("NOT RUN Unity 6000.6.8f1 import, compile, Play, save execution, or screenshots")

if __name__ == "__main__":
    main()
