# tripo generate — deterministic endpoint access

Use `tripo make` unless you need exact endpoint control. All subcommands accept the shared task options (`--json --yes -o --no-wait --no-download --timeout --name -p key=value`).
Everything below is the full parameter reference from the official API docs — you should not need to consult the website.

## 3D

```bash
tripo generate text-to-model "a ceramic teapot" [--model tripo-v3.1|tripo-p1|tripo-p2] [-p face_limit=15000] ...
tripo generate image-to-model photo.png [-p texture_alignment=original_image] [-p orientation=align_image]
tripo generate multiview-to-model front.png back.png [left.png right.png]
tripo generate multiview-to-model @last          # reuse the 4 views of an image-to-multiview / edit-multiview task
```

### P Series (`tripo-p1` / `tripo-p2`) — same endpoints, different model

The P series (editable low-poly topology) uses the **same three endpoints**; only the `model` value changes. The official docs document it as a separate "P Series" tab on each generation page — this is the direct entry point:

```bash
tripo generate text-to-model "a low poly knight for a game" -p face_limit=8000                 # → model: P2-20260801
tripo generate image-to-model photo.png --model tripo-p2 -p quad=true -p face_limit=12000      # → model: P2-20260801
tripo generate multiview-to-model front.png back.png --model tripo-p1                           # explicit lower-cost P-series choice
```

| endpoint | H series (standard) page | P series page |
| --- | --- | --- |
| `POST /v3/generation/text-to-model` | `…/docs/generation-text-to-model/standard` | <https://developers.tripo3d.ai/en/docs/generation-text-to-model/p> |
| `POST /v3/generation/image-to-model` | `…/docs/generation-image-to-model/standard` | <https://developers.tripo3d.ai/en/docs/generation-image-to-model/p> |
| `POST /v3/generation/multiview-to-model` | `…/docs/generation-multiview-to-model/standard` | <https://developers.tripo3d.ai/en/docs/generation-multiview-to-model/p> |

(cn: replace `developers.tripo3d.ai/en` with `developers.tripo3d.com/zh`.) P-series constraints — face_limit ranges, unsupported params, P2's `quad` — are in the table below; the CLI enforces them locally.

### 3D models (`--model`) — Models & Versions

Keywords: model versions · H series (v3.x) · P series (P1 / P2) · quad · alias → wire value.

| alias (what you type) | wire value (what the API receives) | status | positioning | notes |
| --- | --- | --- | --- | --- |
| `tripo-v3.1` | `v3.1-20260211` | GA | explicit H-series choice for dense geometry and H-only features | full feature set; 10–40 credits (text) or 20–50 (image / multiview) by texture tier, plus `geometry_quality=detailed` +20, `quad` +5, `smart_low_poly` +10, `generate_parts` +20 |
| `tripo-p1` | `P1-20260311` | GA | explicit lower-cost low-poly choice | face_limit 50–20000; **no** `quad` `smart_low_poly` `generate_parts` `geometry_quality`; no published price, so plans leave it unestimated |
| `tripo-p2` | `P2-20260801` | GA (2026-08) | default for every 3D generation unless a model is explicitly supplied | adds `quad` (forces FBX); face_limit 48–50000 tri / 48–25000 quad (validated locally); still **no** `smart_low_poly` `generate_parts` `geometry_quality` (stripped with a warning). 100–130 credits for every input by texture tier, no add-ons; confirm the plan card's estimate with the human before spending |
| `tripo-v3.0` / `tripo-v2.5` | `v3.0-20250812` / `v2.5-20250123` | Legacy | previous generations | only when the human asks |

**Alias vs wire value.** `tripo-*`, `p1`, `p2`, `v3.1` … are CLI-side aliases; the CLI always sends the dated wire value in the `model` field (`--model tripo-p2` → `"model": "P2-20260801"`). The v3 server whitelist accepts wire values only — sending an alias straight to the API returns 1004 `invalid model` with the allowed list. `task.json` records the wire value that was actually used. `tripo-turbo` / `tripo-v2.0` appear in some docs but are **not** on the v3 whitelist (400).

**Model selection (CLI and both regional MCP servers).**

- Explicit `--model` wins, followed by `-p model=<string>`; both are normalized and validated against the selected model's capabilities. MCP calls use their `model` argument.
- Omission always selects `P2-20260801`. Prompts, `--for` presets, face budgets and geometry options do not override it.
- Select H3.1 explicitly for H-only features (`smart_low_poly`, `generate_parts`, `geometry_quality`) or face budgets outside P2's range. Without that override, existing P2 validation removes unsupported parameters with warnings and rejects out-of-range face limits; it never silently changes models.
- Every upstream generation request includes the canonical `model`. Preserve that model explicitly between the confirmed plan and execution.
- Texture, rig, image generation and other processing capabilities retain their own defaults.

Official docs (ov / cn):
- Models & Versions: <https://developers.tripo3d.ai/en/docs/models-and-versions> · <https://developers.tripo3d.com/zh/docs/models-and-versions>
- P Series model detail: <https://developers.tripo3d.ai/en/models/p1> · <https://developers.tripo3d.com/zh/models/p1>
- P Series endpoints: `…/docs/generation-text-to-model/p`, `…/docs/generation-image-to-model/p`, `…/docs/generation-multiview-to-model/p` (the `/standard` tab on each page is the H-series version)

### Parameters (text / image / multiview-to-model)

| param | default | applies to | notes |
| --- | --- | --- | --- |
| `prompt` | — | text | ≤1024 chars |
| `negative_prompt` | — | text | ≤255 chars |
| `input` | — | image | `file_` token, `http(s)://` URL or `task_` id (e.g. a text-to-image task) — the CLI uploads local files for you |
| `inputs` | — | multiview | see "multiview inputs" below |
| `model_seed` / `image_seed` / `texture_seed` | random | all | geometry / text-to-image stage / texture seeds — the key to reproducibility (`task.json` records them) |
| `face_limit` | adaptive | all | max polycount; P1 50–20000, P2 48–50000 (quad 48–25000) |
| `texture` | `true` | all | set `false` for bare geometry (print / re-texture later) — saves credits |
| `pbr` | `false` | all | PBR maps; **forces `texture=true`**. The API defaults to `true`, so the CLI sends `false` when omitted; pass `-p pbr=true` only when the user explicitly asks for PBR |
| `texture_quality` | `standard` | all | `fast` (texture v3.5 only, same price as standard) / `standard` / `detailed` / `extreme` (8K, extra credits) |
| `texture_version` | derived from `model` | all | texture model, decoupled from geometry: `v3.5-20260815` (latest) / `v3.0-20250812` (v3.x and P default) / `v2.5-20250123` (short names `v3.5` / `v3.0` / `v2.5` are accepted). Required by `fast` and `delight` — the CLI fills in v3.5 when you omit it; `fast` with another version is rejected locally |
| `delight` | `true` | all | remove lighting baked into the reference before texturing; `false` keeps the original shading. Texture v3.5 only (older versions ignore it) |
| `geometry_quality` | `standard` | v3.x | `detailed` = Ultra mode (+20 credits) |
| `auto_size` | `false` | all | scale to real-world metres |
| `quad` | `false` | v3.x, P2 | quad mesh; **forces FBX output** (quads cannot live in GLB) |
| `smart_low_poly` | `false` | v3.x | hand-crafted-style low-poly; face_limit 500–20000 tri / 500–10000 quad |
| `generate_parts` | `false` | v3.x | editable segmented parts — see rules below |
| `compress` | — | v3.x | `geometry` = meshopt compression |
| `export_orientation` | `+x` | all | forward axis `+x` / `-x` / `+y` / `-y` — see warning below |
| `export_uv` | `true` | all | `false` = faster, smaller file (UVs are unwrapped at the texture stage instead) |
| `enable_image_autofix` | `false` | image | auto-repair low-quality inputs |
| `texture_alignment` | `original_image` | image, multiview | `original_image` (match the picture) / `geometry` (match the mesh) |
| `orientation` | `default` | image, multiview | `align_image` = align the model to the input viewpoint |

Only valid when `model >= tripo-v3.0`: `texture_quality` `geometry_quality` `auto_size` `quad` `smart_low_poly` `generate_parts` `compress`. On v2.5 the CLI removes `geometry_quality` `quad` `smart_low_poly` `generate_parts` `auto_size` `compress` with a warning.

**`generate_parts` rules (server-enforced):**
- `texture=true` or `pbr=true` (including simply omitting `texture`, which defaults to true) → request rejected with `1004`. The CLI flips both to `false` for you and warns; re-texture afterwards with `tripo model texture`.
- `quad=true` → quad is ignored; parts come back as triangle meshes.
- `smart_low_poly=true` → smart_low_poly wins and **no parts are produced**, so the CLI rejects the combination.

**`export_orientation` warning:** it applies to this generation only. Downstream tasks (texture, rig, retarget, convert, …) do **not** inherit it — they read the model in default orientation and produce a wrongly-oriented result while still reporting `success`. If you will post-process, leave it unset and set `export_orientation` on the final `convert` step instead.

### Multiview inputs

The API accepts three `inputs` shapes (never mix them):
- **view-key (recommended)**: `[{"front":…},{"back":…}]` with keys `front/left/back/right`; `front` required, ≥2 views, order irrelevant.
- **legacy positional**: exactly 4 strings `[front, left, back, right]`, `""` skips a slot.
- **task_id**: `[{"task_id":"<image-to-multiview or edit-multiview task>"}]` to reuse a 4-view sheet (source task must be `success`).

The CLI builds view-key payloads from your files: filename hints (`front|back|left|right`, or 正面/背面/左/右) win; otherwise positional order front, left, back, right. A front view is mandatory. A single task id / `@ref` argument is sent in the task_id shape instead (a `@ref` of another task type is rejected locally).

## Images (2D pipeline before 3D)

```bash
tripo generate text-to-image "front view of a knight, T-pose" [-p template=t_pose] [--model seedream_v4]
tripo generate image-to-image sketch.png --prompt "make it look like clay" [--model seedream_v5]
tripo generate image-to-image hero.png outfit.png --prompt "the character from image[1] wearing the outfit from image[2]"
tripo generate image-to-multiview photo.png     # 1 image → 4-view sheet
tripo generate edit-multiview <multiview-task-id> -p 'prompts=[{"view":"front","prompt":"add a sword"}]'
tripo generate image-to-splat photo.png         # gaussian splat
```

### Image models (`--model`)

| model | provider | status | notes |
| --- | --- | --- | --- |
| `seedream_v4` | ByteDance | GA | text-to-image default (balanced); **not accepted by image-to-image** |
| `seedream_v5` | ByteDance | GA | image-to-image default; higher quality |
| `banana` / `banana_pro` / `banana2` | Google | GA | fast / higher quality / latest fast |
| `chat_image_1` | OpenAI | retired 2026-10-23 | rejected locally; explicitly choose a replacement, never auto-resubmit |
| `chat_image_1.5` | OpenAI | active; retiring 2026-12-01 | the CLI warns until retirement and rejects it from 2026-12-01 00:00 UTC |
| `chat_image_2` | OpenAI | GA | GPT, best quality |
| `chat_image_2.5_flare` / `chat_image_2.5_sunburst` | OpenAI | GA | 2.5 series: flare = speed tier (chat_image_2 quality, much faster); sunburst = refined tier (keeps unedited areas closest to the original) |

Image model ids are endpoint-whitelisted; the Gemini names `gemini-2.5-flash` / `gemini-3-pro` / `gemini-3.1-flash` are sent as `banana` / `banana_pro` / `banana2`. Unknown IDs fail locally. Since 0.8.0 the CLI rejects retired `chat_image_1` without automatic replacement; `chat_image_1.5` remains valid until 2026-12-01. Full list with retirement dates: <https://developers.tripo3d.ai/en/docs/models-and-versions>.

### Image parameters

- Since 0.8.0 the CLI validates `model`, `quality`, `background`, `output_format` and known `size` rules on both image endpoints.
- Image editing preflights these and reference counts (Seedream ≤4, Banana ≤10, Chat Image ≤16) before uploading any local file.
- Standalone image/multiview-to-model likewise validates known model/face/quad constraints before uploads; text-to-model validates the effective prompt's 1024-character limit.
- Non-2.5 `background` is removed with an ignored warning (matching the documented server behavior); 2.5 transparency rejects JPEG. `quality=auto` is rejected.
- Unspecified values remain unspecified; the CLI does not invent provider fields or silently change models.

Size rules (official tabs, checked 2026-10-27):
- `banana`: fixed `1024x1024`, `832x1248`, `1248x832`, `864x1184`, `1184x864`, `896x1152`, `1152x896`, `768x1344`, `1344x768`, `1536x672`. Omitted size is left to the service; the documented default differs from its explicit fixed-size list.
- `banana_pro`: `1K` / `2K` / `4K` or positive integer `WxH`; no undocumented pixel limits are invented.
- `banana2`: `0.5K` / `1K` / `2K` / `4K` only.
- `seedream_v4`: `2K` / `4K`; `seedream_v5`: `2K` / `3K`; explicit `WxH` requires aspect ratio 1:16–16:1. The official MP ranges are rounded (v4 approximately 3.69–16.78 MP; v5 3.69–10.40 MP); exact pixel-count cutoffs are not asserted by the CLI and still require server validation.
- `chat_image_1.5`: `1024x1024` / `1024x1536` / `1536x1024` / `auto`.
- `chat_image_2` / 2.5: `auto` or `WxH`, each edge ≤3840 and divisible by 16, long/short ratio ≤3, total pixels 655360–8294400. `1536x864` and `1088x1920` work; `1080x1920` fails locally.

3D display aliases `H3.1` / `P1.0` / `P2.0` (plus `P2.0 Preview`, the pre-GA name kept for compatibility) and legacy `H3.0` / `H2.5` map to the same dated IDs and validation as `tripo-v3.1` / `tripo-p1` / `tripo-p2` / `tripo-v3.0` / `tripo-v2.5`; every model alias is case-insensitive. All `face_limit` values must be finite integers. Unknown model names are rejected, not forwarded.

| param | default | endpoint | notes |
| --- | --- | --- | --- |
| `prompt` | — | t2i (required), i2i | ≤1800 chars after trimming surrounding whitespace; i2i: required unless `template` is set (3D text-to-model keeps its separate 1024-char limit) |
| `image_seed` | random | t2i, i2i | finite integer; preserved by CLI and MCP for reproducibility |
| `negative_prompt` | — | t2i, i2i | string; the 3D negative-prompt 255-char limit does not apply to image endpoints |
| `input` | — | i2i, i2mv, edit-mv, splat | `file_` token / URL (i2i also accepts a `task_` id); i2i: JPEG/PNG ≤20 MB, ≤36 MP |
| `inputs` | — | i2i | multiple reference images (the CLI sends this when you pass several images): seedream ≤4, banana ≤10, chat_image ≤16; cite them in the prompt as `image[1]`, `image[2]`… |
| `template` | — | t2i, i2i | t2i: `asset_extraction` `character_completion` `t_pose` `variants` `figure`; i2i: `t_pose` `character_completion` `3d_enhance` `variants` `figure`; other values are rejected locally |
| `size` | `2048x2048` | t2i, i2i | keyword (`2K` / `4K`) or exact pixels; accepted values differ per model |
| `quality` | `low` | t2i, i2i | `chat_image_2`: `low` `medium` `high`; `chat_image_2.5_*`: also `xhigh` `max` (no `auto`). On other models the CLI removes it with a warning. `high` and above cost more credits |
| `background` | model decides | t2i, i2i | `chat_image_2.5_*` only (ignored elsewhere): `auto` / `opaque` / `transparent`; `transparent` requires `output_format=png` |
| `aspect_ratio` | `1:1` | t2i, i2i | banana family only: `1:1 2:3 3:2 3:4 4:3 4:5 5:4 9:16 16:9 21:9` (banana2 adds `1:8 1:4 4:1 8:1`); seedream / chat_image use `size` (the CLI removes it there with a warning) |
| `output_format` | `png` | t2i, i2i | `png` (lossless, alpha) / `jpeg` (smaller, no alpha) |
| `watermark` | `false` | t2i | AI-content watermark, seedream only (banana always embeds an invisible one; chat_image has no control); removed with a warning on other models |
| `model_seed` | random | splat | same seed + same image = repeatable `.splat` |

- `template=t_pose` is the golden pre-step for the animation pipeline; `3d_enhance` (i2i) cleans a photo before image-to-model.
- Sizes are billed in 1K / 2K / 4K tiers.
- `image-to-multiview` output: `front_view_url` `left_view_url` `back_view_url` `right_view_url` — feed them (or the task id, see above) to `multiview-to-model`.
- `edit-multiview` takes a successful `image-to-multiview` task; `prompts` is 1–4 `{view, prompt}` objects.
- `image-to-splat` returns a `.splat` file in `output.model_url` (not a mesh — no texture / rig / convert downstream). Fixed **30 credits**, ~4 minutes. Input PNG / JPEG / WebP ≤20 MB, ≥256×256 px, subject clear and centred. Use image-to-model when you need an editable mesh.

