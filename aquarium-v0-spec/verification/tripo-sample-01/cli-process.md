# tripo model / anim / mesh — processing steps

All subcommands take input as: explicit argument (task id / @last / @name / file / URL) > piped stdin JSON > `@last` (only in an interactive terminal with nothing piped).
Headless, a missing input never falls back to `@last`: empty stdin or no `task_id` exits 2, an upstream error JSON exits with the upstream's code, and nothing is submitted.
Shared options: `--json --yes -o --no-wait --no-download --timeout --name -p key=value`.
Any API parameter without a first-class flag can be passed with `-p key=value` (JSON values allowed: `-p 'part_names=["head","body"]'`).

## tripo model

```bash
tripo model texture [input] [--model v3.5-20260815|v3.0-20250812|v2.5-20250123] \
    --texture-quality fast|standard|detailed|extreme --texture-alignment geometry [-p pbr=true] [-p delight=false]
tripo model convert [input] --format GLTF|USDZ|FBX|OBJ|STL|3MF \
    [--texture-size 2048] [--texture-format JPEG|PNG|OPEN_EXR|...] [--face-limit N] \
    [--quad] [--flatten-bottom] [--fbx-preset blender|3dsmax|mixamo|bake_scale] \
    [--export-orientation +x|-x|+y|-y] [--scale-factor 1.5] \
    [--pivot-to-center-bottom] [--export-vertex-colors]
tripo model import <file-or-url>            # GLB/GLTF/FBX/OBJ/STL ≤150MB
```

| step | input accepted | params |
| --- | --- | --- |
| `texture` | task / file / URL (file/URL is imported implicitly) | `model` `v3.0-20250812` (default) / `v3.5-20260815` (latest; required by `fast` and `delight` — the CLI selects it when you use them without `--model`, and rejects `fast` with another version) / `v2.5-20250123` (short names `v3.5` / `v3.0` / `v2.5` are accepted); `texture_quality` fast/standard/detailed/extreme; `delight` (default true, v3.5 only: strip baked lighting from the reference); `pbr` (default false — the CLI sends `false` unless you pass `-p pbr=true`, which only an explicit PBR request warrants); `texture_seed`; `texture_alignment` original_image/geometry |
| `convert` | task / file | see table below; `format` required |
| `import` | file / URL | GLB / GLTF / FBX / OBJ / STL ≤150 MB. A self-contained `.gltf` (buffers/images embedded as `data:` URIs) uploads fine; one that references external `.bin`/textures is rejected locally — export `.glb` or pass a URL. Output adds `normalize_scale`: final size = original size × normalize_scale (`1` = untouched); divide by it to recover the original size. Texture / convert / rig etc. on a file / URL input return it too (they import first); task-id inputs do not |

Local files over 60 MB (and every `.gltf`) are uploaded through a presigned URL instead of multipart; the limits stay 20 MB per image and 150 MB per model.

### Texture preflight and guidance

`tripo model texture ... --dry-run --json` plans a standalone texture request offline (details below). For an existing generated task that needs PBR without guidance, `make` can plan the texture step too:

```bash
tripo make <task_id> --then texture:pbr=true --dry-run --json
```

When `texture_prompt` guidance is needed, use standalone `model texture`: `-p texture_prompt=...` accepts a JSON object, while `--then` cannot carry it. Supply guidance for imported meshes and reuse the original reference when available. Preflight the command with `--dry-run --json`, get confirmation, then run it without `--dry-run`, for example:

```bash
tripo model texture hero.glb -p pbr=false -p 'texture_prompt={"text":"worn leather"}' --json
tripo model texture <task_id> -p pbr=true -p 'texture_prompt={"image":"<file_token>"}' --json
```

Reference list prices checked 2026-11-02: fast / standard 10 credits, detailed 20, extreme 30 per texture task. Confirm the current price for the account's region ([international](https://developers.tripo3d.ai/en/pricing), [China](https://developers.tripo3d.com/zh/pricing)); the reference price is not a live quote.

Text guidance uses `{"text":"..."}` (optionally with `style_image`); image guidance uses `{"image":"<URL, file_token or local path>"}`. Existing positional image lists and named views such as `{"images":[{"front":"file_front"},{"left":"file_left"}]}` remain supported. Nested image references (`image`, `images`, `style_image`) follow the same rule as `tripo mcp --toolset full`: `./`, `../`, absolute and `~/` paths, and existing relative files, are all checked before anything is uploaded, then uploaded first. A missing explicit local path fails locally; other values (URLs, file tokens, remote object keys) are sent unchanged, so prefix a local file with `./`. Guidance is not newly required by CLI validation, and passthrough combinations are not guaranteed to be accepted upstream.

Standalone texture preflight (`tripo model texture ... --dry-run --json`) works without an API key, performs existing parameter and local-file checks, and returns `dry_run`, `valid`, `input`, `steps`, `pending_uploads`, `warnings`, `errors`, `generation_credits_estimate` and `cost_notes`. `steps[].payload` is the normalized texture request, including the texture model and explicit `pbr` (false by default). A local model or reference image uses `<upload:path>` and is listed in `pending_uploads`; the real standalone command uploads them and sends the texture request, with import handled internally by the server. No upload, balance lookup, task lookup, submission, download, telemetry, update check, history or context write occurs during that preflight. This CLI option does not add a `dry_run` parameter to MCP `tripo_texture`; other processing commands and `generate` do not accept it.

For either supported dry-run path, local validation failures return `valid:false` and exit 2; piped upstream failures preserve their exit code. Remote input availability, task state and server acceptance are not checked. `generation_credits_estimate:null` means texture execution cost is not estimated, never that execution is free. Fix errors and resolve parameter adjustments that conflict with the user's requirements. Disclose the remaining offline and cost limitations, get confirmation, then remove `--dry-run` from the same command; those limitations do not disappear on another preflight. Re-texturing keeps geometry but regenerates textures, so re-supply the original reference when available.

### convert parameters (full)

| group | param | default | notes |
| --- | --- | --- | --- |
| basic | `format` | — | `GLTF` `USDZ` `FBX` `OBJ` `STL` `3MF` (3MF = single colour) |
| mesh | `quad` | false | quad remesh; **forces FBX output** (GLTF is rejected locally, other formats are overridden — pass `--format FBX`) |
| mesh | `force_symmetry` | false | only with `quad=true` |
| mesh | `face_limit` | keep original | target polycount |
| mesh | `flatten_bottom` / `flatten_bottom_threshold` | false / 0.01 | flat base for printing |
| texture | `texture_size` | 4096 | diffuse size in px |
| texture | `texture_format` | JPEG | `BMP` `DPX` `HDR` `JPEG` `OPEN_EXR` `PNG` `TARGA` `TIFF` `WEBP` |
| texture | `bake` | true | bake advanced materials into base textures |
| texture | `pack_uv` | false | pack all UVs into one layout |
| texture | `export_vertex_colors` | false | **OBJ / GLTF only** |
| export | `pivot_to_center_bottom` | false | pivot at bottom centre |
| export | `scale_factor` | 1 | uniform scale |
| export | `with_animation` | true | keep skeleton + animation data |
| export | `animate_in_place` | false | strip root motion |
| export | `part_names` | — | export only these parts (names from a segment task) |
| export | `export_orientation` | `+x` | forward axis `+x` / `-x` / `+y` / `-y` — set orientation **here**, as the last step (generation-time `export_orientation` is not inherited by other steps) |
| export | `fbx_preset` | blender | `blender` / `3dsmax` / `mixamo` / `bake_scale` (bake the scale transform into the geometry) — FBX only |

Billing: a non-default `quad`, `flatten_bottom`, `flatten_bottom_threshold`, `texture_size`, `texture_format`, `pivot_to_center_bottom` or `scale_factor`, or any `face_limit`, bills as "complex convert" (pricier than a plain format swap).

## tripo anim

```bash
tripo anim check [input]                    # rig-check: {"riggable":true,"rig_type":"biped"}; GLB/FBX/OBJ/STL input
tripo anim rig [input] [--model v3.0-20260909|v2.5-20260210|v1.0-20240301] \
    [--rig-type biped|quadruped|hexapod|octopod|avian|serpentine|aquatic|others] \
    [--spec tripo|mixamo|actorcore|unity|unreal|vrm] [--out-format glb|fbx]   # default v2.5 for quadruped, v3.0 otherwise
tripo anim retarget [input] --animation preset:walk [preset:run ...] \
    [--out-format glb|fbx] [--animate-in-place]
```

| step | input accepted | params |
| --- | --- | --- |
| `check` (rig-check) | task / file / URL — GLB / FBX / OBJ / STL, ≤150 MB | none. Output: `riggable` (bool) + recommended `rig_type` (one of the 7 types below, never `others`). Run it first: not riggable → stop before spending rig credits |
| `rig` | task / file / URL (GLB / FBX / OBJ / STL ≤150 MB) | `model`: `rig-v2.5` (alias `rig-v2.0`, wire `v2.5-20260210`, default for `quadruped`) / `rig-v3.0` (wire `v3.0-20260909`, default for other body types) / `rig-v1.0` (wire `v1.0-20240301`, server default, biped only). An explicit model overrides the body-type default. `rig_type` biped/quadruped/hexapod/octopod/avian/serpentine/aquatic/others (default biped — use rig-check's value; `others` = none of the above, v3.0 only); `spec` (biped only) tripo/mixamo/actorcore/unity/unreal/vrm (actorcore/unity/unreal/vrm need v3.0); `out_format` glb/fbx |
| `retarget` | **rig task id only** | `animation` (one) or `animations` (list, ≤5, billed per animation); `out_format` glb/fbx; `export_with_geometry` (default true); `animate_in_place` (default false); `bake_animation` is deprecated, removed locally with a warning, and never sent upstream |

- Short names `v3.0` / `v2.5` / `v1.0` and marketing names `rig-v1.0` / `rig-v2.0` / `rig-v2.5` / `rig-v3.0` are normalized to the wire versions automatically (sending the marketing names raw is a 400).
- When body type is unknown, run `tripo anim check <original_model>` and inspect `tripo task get <check_task_id> --json` first; rig the saved original model task/file with the recommended `--rig-type`, not the check task or the updated `@last`.
- `--for anim` and `--then rig-check,rig` use the check's body type to select the default. Offline plans omit the rig `model` until that check completes and disclose quadruped → v2.5, other types → v3.0. A specified `rig_type` or `model` resolves the choice locally; standalone rig never infers body type from a task id.
- Mixamo → `--spec mixamo` (+ `--fbx-preset mixamo` at convert time); Unity Humanoid → `--spec unity`; Unreal Mannequin → `--spec unreal`; ActorCore / VRM 1.0 → `--spec actorcore` / `--spec vrm`.
- Game code drives movement → `--animate-in-place`.
- The preset set depends on **which rig model and rig type the upstream rig task used**.
- In `make` / `--then` chains, a non-biped rig gets its own preset instead of the biped ones (quadruped / hexapod / octopod → `:walk`, serpentine / aquatic → `:march`); avian / others have no presets, so the chain stops after rig and keeps the rigged model.

### Presets for rig v2.5 / v3.0

- biped: `preset:idle` `preset:walk` `preset:run` `preset:dive` `preset:climb` `preset:jump` `preset:slash` `preset:shoot` `preset:hurt` `preset:fall` `preset:turn`
- quadruped: `preset:quadruped:walk`; hexapod: `preset:hexapod:walk`; octopod: `preset:octopod:walk`; serpentine: `preset:serpentine:march`; aquatic: `preset:aquatic:march`
- avian / others: none

### Presets for rig v1.0 (biped only, 90+)

All prefixed `preset:biped:`
- basic: `idle` `walk` `run` `run_upstairs` `turn` `jump` `jump_down` `jump_rope_01` `jump_rope_02` `fall` `climb` `dive` `swim` `surf` `flip` `sit` `standing_relax` `look_around` `wait` `swagger`
- dance: `dance_01` … `dance_06` `freaky`
- performance: `sing_01` … `sing_04` `cheer`
- emotion: `clap` `bow` `greet_01` … `greet_04` `wave_goodbye_01` `wave_goodbye_02` `agree` `heart_pose` `hug` `fold_arms` `laugh_01` `laugh_02` `cry` `sob` `afraid` `frightened` `scared_01` `scared_02` `angry_01` … `angry_03` `depressed` `frustrated_01` `frustrated_02` `complain_01` `complain_02` `victory_celebration` `defeat_02` `defeat_03`
- daily: `scratch` `make_a_call_01` `make_a_call_02` `play_mobile_game` `play_video_game` `dig` `chop` `shovel` `lift_heavy`
- sports: `warm_up` `press-up` `cross_body_crunch` `basketball_shot` `crossover_dribble` `dribble` `pitch_baseball` `golf` `volleyball` `football_catch` `football_save` `football_pass`
- combat: `box_01` … `box_03` `front_kick_01` `front_kick_02` `slash` `shoot` `fire` `cast_a_spell` `hurt` `hit_to_body_01` `hit_to_body_02` `hit_to_head` `hit_to_side` `hit_to_stomach` `flee_01` `flee_02`

## tripo mesh

```bash
tripo mesh segment [input] [--model v1.0-20250506|v2.0-20260430]
tripo mesh complete [input] [--completion-mode ai_completion|quick_cap]   # needs a segment task
tripo mesh decimate [input] --face-limit 5000 [--model v3.0|v2.0|v1.0] [--quad] [--smart-uv] [--no-bake]   # 500–20000 tri / 500–10000 quad
tripo mesh uv [input] [--no-bake] [--export-orientation +x|-x|+y|-y]     # input ≤ 80,000 equivalent triangles
tripo mesh smartsegment <file-or-url> [--seg-type image|model] [--granularity coarse|medium|fine] [--hint "..."]
```

| step | input accepted | params |
| --- | --- | --- |
| `segment` | task / file / URL | `model` `v1.0-20250506` (default, geometry-based) / `v2.0-20260430` (Beta, semantic labels + geometry). **v2 only**: `segmentation_granularity` simple/balanced/detailed (default balanced), `split_by_connectivity` (default true), `ref_image` file_token/URL — when `ref_image` is set the other two are ignored. With no `model`, any v2-only parameter selects v2.0; an explicit v1 is rejected locally (the server answers `1004`) |
| `complete` | **segment task only** | `completion_mode` `ai_completion` (AI diffusion, default) / `quick_cap` (fast hole-fill, no AI); `part_names` (default: all parts); `model` `v1.0-20250506` (only version) |
| `decimate` | task / file / URL | `model` `v2.0` (default, smart retopology, 30 credits) / `v3.0` (newest retopology, 35 credits tri / 40 quad) / `v1.0` (basic decimation, 10 credits); `face_limit` (v2.0/v3.0: optional, 500–20000 tri / 500–10000 quad, adaptive if omitted); `quad`; `bake` (default true — bake textures onto the low-poly, right for LODs); `part_names`; `smart_uv` (**v3.0 only**, default false — unwrap UVs in the same task, no extra cost, no UV atlas image; needs an explicit `--model v3.0`: without it, or with v1.0/v2.0, the CLI rejects the request, because the server answers `1004` on v1.0/v2.0 and v3.0 bills above the default). `--model v1.0`: up to 2,000,000 tri / 150,000 quad but `face_limit` becomes required and `bake`/`part_names` are unsupported |
| `uv` | task / file / URL | re-unwraps UVs and remaps the existing textures, rig and animation onto the new layout; **20 credits**. `bake` (default true; `--no-bake` returns geometry + UVs only); `export_orientation` `+x`/`-x`/`+y`/`-y`; `compress` `geometry` (meshopt, `-p compress=geometry`); `model` `default` (only value). **Input ≤ 80,000 equivalent triangles** (a quad counts as two): larger inputs are accepted, then the task fails and refunds — decimate first |
| `smartsegment` | **file / URL only (no task ids)** | `seg_type` `image` (photo → auto-model → segment, **85 credits**) / `model` (existing **GLB only** → segment, **55 credits**); CLI default: `image` for an image file/URL, else `model`; `granularity` coarse/medium/fine (default medium); `hint` free text describing the parts; `transform` 4×4 column-major matrix required for `model` — the CLI auto-fills identity |

- `segment` vs `smartsegment`: segment only splits an existing model; smartsegment is the end-to-end pipeline (asset → auto modelling → segmentation).
- smartsegment output: `prompt` (comma-separated part labels), `mask_url` (PNG semantic mask), `seg_model_url` (segmented GLB), `seg_task_id` (inner segmentation task), `model_task_id` (inner image_to_model / import task). Task types: `smartsegment_image` / `smartsegment_model`.
- `mesh complete` only accepts segmentation tasks; segmented / completed parts can then be exported selectively via `convert -p 'part_names=[...]'`.
- UVs in one task or two: `decimate --model v3.0 --smart-uv` unwraps during retopology — one task, no atlas image. `uv` is its own task that also returns the UV atlas; run it on models that are already under the limit, e.g. `--then decimate:20000,uv`.
- uv output: the model plus `uv_render.webp` (the UV atlas). It is a flat layout, not a render of the model, so the CLI does not copy it to `preview.png`.

