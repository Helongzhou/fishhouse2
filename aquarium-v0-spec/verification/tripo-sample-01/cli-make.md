# tripo make

The magic command: input in, finished local 3D artifacts out. Blocking; exits when files are on disk.

```
tripo make <input...> [options]
```

## Before anything is spent: the plan card

`make` first prints a plan card on stderr — input, scenario, model, then **one numbered line per API call** (endpoint, what it does, its args), deliverables and cost notes:

```
── Plan ──────────────────────────
input: a medieval knight
scenario: game-mobile (Mobile game / low-poly)
model: P2-20260801 — default → P2
steps:
  1. text-to-model  POST /v3/generation/text-to-model — generate a 3D model from text
     texture=true texture_quality="standard" face_limit=15000 pbr=false model="P2-20260801"
  2. convert  POST /v3/models/convert — convert format / texture size / orientation
     format="FBX" texture_size=2048
deliverables: FBX
cost impact:
  · estimated generation cost: 110 credits (official list price; chain steps not estimated)
  · non-default convert options bill as complex convert
  P2 editable low-poly topology
  2K textures for mobile budgets
  P2 costs more credits than H/P1; check credits before running
  append --then rig if you need a skeleton
──────────────────────────────────
```

In a TTY it then asks `Run this plan?`; answering no exits 0 with `cancelled; no credits spent`. `--yes` (auto-enabled when stdin/stdout are not TTYs, i.e. for agents and pipes) skips the question but still prints the card, so the log shows exactly what ran. `--quiet` hides it.

## Inputs (auto-detected)

| you pass | it runs |
| --- | --- |
| quoted text | text-to-model |
| one image (.png/.jpg/.jpeg/.webp/.bmp) or image URL | image-to-model |
| 2–4 images | multiview-to-model (filename hints front/back/left/right win; otherwise positional front,left,back,right; front required) |
| a model file or model URL (.glb/.gltf/.fbx/.obj/.stl) | import (add --then for processing) |
| task id / `@last` / `@name` | continue processing from that task (--then required) |

## Options

- `--for <scenario>` — `game-mobile` `game-pc` `film` `print` `ar-web` `anim` `toy`. Sets params+chain+format from the domain knowledge base; generation still defaults to P2 unless explicitly overridden.
- `--then <steps>` — processing chain, comma-separated. Steps: `texture convert rig-check rig retarget segment complete decimate uv`. Args: `step:key=value,...`; bare value maps to the step's primary arg (`convert:fbx` → format, `decimate:5000` → face_limit, `retarget:preset:walk` → animation). Overrides the scenario chain.
- `--model <m>` — force a model. The CLI normalizes these aliases (case-insensitive) to the wire versions the server actually accepts. Full table with status (GA / Legacy) and official links: `tripo docs --topic commands/generate` → "Models & Versions".
  - `tripo-v3.1` (`v3.1-20260211`) — explicit H-series choice for dense geometry / H-only features. Plan card estimate at official list price: 10–50 credits before add-ons.
  - `tripo-p1` (`P1-20260311`) — explicit lower-cost low-poly; face_limit 50–20000; quad/parts/geometry_quality unsupported and stripped. Not estimated.
  - `tripo-p2` (`P2-20260801`) — default 3D generation model; editable topology; quad; face_limit 48–50000 tri / 48–25000 quad. Plan card estimate: 100–130 credits.
  - Without an explicit model, every prompt and scenario uses P2. Face budgets and H-only options do not change the model: invalid P2 budgets fail and unsupported fields are removed with warnings. Select H3.1 explicitly to use its capabilities. Legacy models are not estimated.
- `-n, --candidates <n>` — up to 4 parallel candidates with different seeds (interactive pick when chaining).
- `--seed <n>` — fixed model_seed for reproducible geometry.
- `-p, --param key=value` — extra API parameter, repeatable. Common: `texture=false pbr=false` (bare geometry, cheaper), `texture_quality=detailed`, `face_limit=15000`, `quad=true` (forces FBX), `auto_size=true`, `negative_prompt=...`. For 3D generation, `-p model=<string>` is also an explicit model choice, normalized and validated like `--model`; `--model` takes precedence when both are supplied.
- `-o, --out <dir>` — artifact base dir (default `./tripo-out/<name>-<id8>/`).
- `--no-wait` — submit only, print `{"task_id"}` and exit (poll later with `tripo task watch`). Chain steps need the finished upstream task, so `--no-wait` with `--then` or a preset that has its own chain exits 2 before anything is submitted.
- `--no-download` — wait but skip downloads, including the final step of a chain.
- `--name <name>` — history name for `@name` references.
- `--notify` — desktop notification when done.
- `--timeout <seconds>` — watch timeout (default 1800).
- `--yes` (global) — skip the plan-card confirmation; implied when headless.
- `--dry-run` — plan + validate only; nothing is uploaded or submitted, no credits spent. See below.

## Dry run (agents: validate before spending)

`tripo make ... --dry-run --json` runs the whole planning stage with **zero network calls** and prints the plan as the result on stdout: the selected model, the completed request body for every call (scenario defaults merged, aliases normalized to wire values, illegal params stripped, step defaults filled in), and the outcome of every local validation. Works without an API key.

```bash
tripo make concept.png --for game-mobile --then rig:model=rig-v3.0,convert:fbx --dry-run --json
```

```json
{"dry_run":true,"valid":true,
 "input":"concept.png","scenario":"game-mobile",
 "model":"P2-20260801","model_reason":"default → P2",
 "steps":[
   {"name":"image-to-model","endpoint":"/v3/generation/image-to-model","task_type":"image_to_model",
    "payload":{"input":"<upload:concept.png>","texture":true,"texture_quality":"standard","face_limit":15000,"pbr":false,"model":"P2-20260801"}},
   {"name":"rig","endpoint":"/v3/animations/rig","task_type":"animate_rig","payload":{"input":"<upstream>","model":"v3.0-20260909"}},
   {"name":"convert","endpoint":"/v3/models/convert","task_type":"convert_model","payload":{"input":"<upstream>","format":"FBX"}}],
 "pending_uploads":["concept.png"],
 "deliverables":["FBX"],
 "candidates":1,
 "warnings":["--then overrides the default game-mobile processing chain"],
 "errors":[],
 "generation_credits_estimate":110,
 "cost_notes":["estimated generation cost: 110 credits (official list price; chain steps not estimated)"]}
```

- `valid` / `errors` — `errors` are blocking (illegal chain order, out-of-range `face_limit`, quad + GLTF, missing `animation` for retarget, ...). The real run would exit 2 on them before spending anything; the dry run exits 2 too, so branch on the exit code or on `valid`.
- `warnings` — adjustments the CLI applies automatically (e.g. `P1 does not support quad (removed)`). A removed requirement is not fulfilled: resolve it with the user before paid execution, even when the normalized request is `valid=true`.
- `generation_credits_estimate` — official list price of the generation step × candidates; `null` for P1, legacy models, imports and task references (not estimated, never zero). Chain steps are not included, so a chained run costs more.
- `steps[].payload` — the exact body that would be POSTed. Local files show as `<upload:path>` (also listed in `pending_uploads`); chain steps use `<upstream>` for the task id that does not exist yet.
- A bare task id with no local history entry cannot have its type checked offline: chain legality is reported as unverified in `warnings` and checked by the real run.
- Without `--json` the same plan card as the real run is printed on stdout, followed by the warnings/errors and a verdict line.

Typical agent loop: dry-run → fix `errors` / adjust params → dry-run again until `valid` → drop `--dry-run` and run for real.

For an existing generated task needing PBR without guidance, plan `tripo make <task_id> --then texture:pbr=true --dry-run --json`. For imported-mesh texturing or reusing an original reference image, plan `tripo model texture ... -p 'texture_prompt={...}' --dry-run --json`; `--then` cannot carry the guidance object. See `commands/process` for request details and unestimated texture costs.

## Output (stdout, --json)

```json
{"task_id":"...","type":"convert_model","status":"success",
 "credits_consumed":145,
 "credits_breakdown":[
   {"task_id":"<generation>","type":"text_to_model","credits_consumed":110},
   {"task_id":"<texture>","type":"texture_model","credits_consumed":30},
   {"task_id":"<convert>","type":"convert_model","credits_consumed":5}],
 "output_dir":"tripo-out/knight-1a2b3c4d","files":["model.fbx","preview.png","task.json"],
 "model_file":".../model.fbx","preview":".../preview.png",
 "source_task_id":"<generation>",
 "chain":[{"task_id":"<texture>","type":"texture_model","credits_consumed":30},{"task_id":"<convert>","type":"convert_model","credits_consumed":5}]}
```

- Single task (no `--then`): `credits_consumed` is that task's spend; no `credits_breakdown`.
- Model file / URL input: the result adds `normalize_scale`, the factor the import rescaled the model by (final size = original size × normalize_scale; `1` = untouched). The platform never scales it back; divide by it to recover the original size. With `--then`, the steps after the import do not return it, so the result carries the import's value.
- Chained run: `credits_consumed` is the **total** across generation + every step, and `credits_breakdown` lists each task in run order (generation first). Human mode prints the same as `credits consumed (total): 145` followed by one indented line per task.
- `--for ar-web` with its own chain (no `--then`): the generated GLB is downloaded too, so the result adds `source_output_dir` / `source_model_file` (GLB) beside `output_dir` / `model_file` (USDZ). A chain that stops early (not riggable) reports the source model it downloaded instead in the same two fields.

Exit codes: 0 ok · 1 unexpected error · 2 params · 3 auth · 4 credits · 5 content policy · 6 task failed · 7 network · 8 not found · 9 rate limit.

## Cost behavior

- Since 0.8.0, `make` preflights every deterministic chain request before resolving local uploads, in both normal and dry-run modes.
- Missing convert formats, incompatible rig/texture options, known rig/preset-family conflicts and disabled steps cannot produce a successful validated request.
- Headless `-n 2` with an explicit or scenario-default chain is rejected before upload, including dry-run.
- `valid=true` means the normalized local plan passed known rules, not that unknown passthrough fields or runtime source-dependent checks have been verified.
- Removed P-series fields remain warnings and are absent from the payload.
- Plan notes, normalized step parameters and final deliverables reflect the actual request rather than overridden scenario defaults.
- Only the generation step is estimated, at official list price; P1, legacy models and chain steps are not.

- The built-in anim chain leaves spec and rig model unspecified so rig-check can recommend a body type. An omitted rig model selects v2.5 for quadruped and v3.0 otherwise; an explicit rig model wins. A dry-run with an unresolved check omits the rig payload's `model` and explains both choices in its warnings instead of claiming a fixed model.
- Explicit non-biped spec is rejected by this CLI to avoid claiming an ineffective requirement was met; the upstream documentation describes spec as biped-only, not as a universally forbidden field.
- A known v1.0 rig uses `preset:biped:*`; v2.5/v3.0 use generic/body-type presets. Unknown standalone sources are not assigned an invented rig version.
- Prompt wording never changes the generation model. Use `--model` or `-p model=<string>` to override the P2 default explicitly.

The plan card shows every call before anything is submitted; balance is pre-checked before submitting; failed tasks auto-refund frozen credits. After the run, `credits_consumed` / `credits_breakdown` report what was actually billed per task. `texture=false pbr=false` skips texture credits entirely (right for 3D printing).

