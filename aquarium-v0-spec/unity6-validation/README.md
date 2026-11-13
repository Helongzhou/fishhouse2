# Unity 6 migration candidate

This isolated project targets **6000.6.8f1** (`237b6593792b`) with candidate **URP17.6.0**. Its exact resolved package/editor combination must pass real editor acceptance before becoming the running baseline. `../unity` remains the Unity2022.3.62f3/URP14.0.11 source baseline.

Only Assets/_Game and the necessary Packages/ProjectSettings were copied. No Library, cache, imported assets, Temp or build was copied, and no Unity process was started by the preparation tool. The original source hashes and intentional changes are in `migration-source-manifest.json`.

Preparation: `python3 tools/prepare_unity6_validation.py` from the repository root creates this directory only when absent; use `--destination` for another clean candidate. It refuses to overwrite an existing project and verifies the source baseline remains unchanged.

The sole initial product API change is `GraphicsSettings.defaultRenderPipeline` in the Editor Prepare helper. Shader/material/renderer assets are generated afresh by **Aquarium → Prepare Project**. Unity6's default RenderGraph remains enabled; this project has no custom RenderPass/Blit implementation to migrate. The legacy input/IMGUI flow, real 3D source, English fallback and full-session schema remain the same.

The copied Editor validation tools belong to their original owner. The launcher accepts `--project <this-directory>` and must be scheduled by the root agent before use. No import, compile, Play, rendering, JsonUtility or player-build success has yet been established. See `../docs/Unity6迁移联调说明.md`.

After real batch-01 exposed inline-null serialization, the isolated Gameplay snapshot is schema2 with explicit HasSubstrate; old gameplay schema1 prototype saves are rejected. The exact source patch is retained under `../tools/unity6_patches` and applied by the fresh-candidate preparation script. This does not alter World2 or outer Session1. Batch-01 passed compilation/URP/scene/config checks, then failed save restoration; the fix still awaits real batch-02.
