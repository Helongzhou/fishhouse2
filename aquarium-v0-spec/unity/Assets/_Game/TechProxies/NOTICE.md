# Technical proxies for the Unity 2022 project

These three GLB files are development stand-ins copied from `art/tech-proxies-v0/`. They are not finished species models.

| Game id, unchanged | File | What it is |
| --- | --- | --- |
| `ember_tetra` | `Models/fh_v0_tetra_proxy_L0.glb` | Generic tetra proxy, about 4.8 cm, not Hyphessobrycon amandae |
| `plant_anubias_nana` | `Models/pl_fg_v0_carpet_proxy.glb` | Foreground plant stand-in |
| `plant_java_fern` | `Models/pl_md_v0_leaf_proxy.glb` | Midground leaf stand-in |

Neon tetra, shrimp, moss, hardscape, glass and water keep the existing primitives. L1 and L2 fish files stay in the art pack and are not switched by distance.

## How to turn it on, and how to turn it back

The player scene is unchanged and does not list `EnableTechnicalProxyVisuals`, so Play keeps the original placeholders. To inspect the proxies, select the `Aquarium Journal` object, enable `Enable Technical Proxy Visuals`, then enter Play. Clear that checkbox before the next Play to return to the placeholders. No save field or species row changes.

`TechProxyValidation.unity` is a separate viewing scene and is not in `EditorBuildSettings`. The built player still starts `Aquarium.unity`.

The GLB files live under `Assets` so the editor can read them. They are not in `Resources` or `StreamingAssets`. A player build that does not reference them leaves them out; if the checkbox is enabled in a build, the files are absent and the placeholders stay.

## Importer

`Packages/manifest.json` pins `com.unity.cloud.gltfast` **6.10.1** (Apache-2.0). Its package manifest says Unity `2020.3` / release `48f1`. This project is Unity **2022.3.62f3** and URP **14.0.11**. Newer glTFast 6.15+ documents a minimum of 2022.3.67f2, so that line was not used. Built-in animation and texture modules are listed so the importer's animation and PNG defines can turn on.

This environment did not open Unity, did not resolve Package Manager, and did not compile these scripts. Import, skinning, alpha clip, double-sided leaves and clip playback are unverified until that editor is run. The package's URP generator is what sets mask and `_Cull` off; the adapter only repeats those properties when the shader exposes them.

Scale stays 1 in meters. Gameplay still writes position and rotation on the existing object. The proxy is a child whose local scale cancels the placeholder's scale, and any collider on the proxy is removed. Root motion is forced off if an Animator appears. The fish clip played in game is `swim_tail_loop` only.

License and the file self-check live in `art/tech-proxies-v0/`. Preview stills there are software rasters, not Unity images.

## Checks recorded with this change

From `aquarium-v0-spec`, Python 3.12, no Unity editor and no .NET SDK:

- `python3 tools/check_tech_proxies.py` exited 0. It re-read all 10 GLBs: meter extras, manifest triangle counts, sampled skin weights summing to 1, and animation channels that rotate bones only.
- `python3 tools/validate_config.py` exited 0. The strict release command exited 1 with the existing 14 warnings.
- `python3 tools/check_localization.py` exited 0 at 269 keys. `--self-test` exited 0 with 12 isolated cases.

None of these commands compiles or renders the Unity project.
