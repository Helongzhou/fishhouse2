# Offline API-generation price comparison

The exact same approved Ember Tetra prompt and face_limit=5000 were preflighted on 2026-11-09 with Tripo CLI 0.11.2. No preset, processing chain, uploads or paid calls were used. Original manifest, 11 plans and runner were not changed.

| Model | Parameters | Credits / generation | 11 identical-price jobs |
|---|---|---:|---:|
| P2-20260801 | texture=true, pbr=false, texture_quality=standard | 110 | 1210 |
| v3.1-20260211 (H3.1) | texture=true, pbr=false, texture_quality=standard | 20 | 220 |
| v3.1-20260211 (H3.1) | texture=false, pbr=false, texture_quality=standard | 10 | 110 |

All three returned valid=true, warnings=[], errors=[], one text-to-model step, one candidate, face_limit=5000 retained in the normalized API payload. This proves the face budget is forwarded and locally accepted, not that the eventual server mesh will exactly meet it.

The bundled price table at `/opt/homebrew/lib/node_modules/tripo-cli/dist/knowledge/models.js:214` labels its values official model-page list prices, with four tiers: no texture, standard/fast, detailed, extreme. P2 is [100,110,120,130]; H3.1 text generation is [10,20,30,40]. Thus P2 standard color is the 100 bare-geometry tier plus a 10-credit texture-tier increment, **not** 110 caused by rigging/conversion, PBR or face_limit=5000. This is explanatory arithmetic across the table tiers, not two separately submitted API jobs. H3.1 add-ons (not used here): detailed geometry +20, quad +5, smart_low_poly +10, generate_parts +20. `face_limit` alone is not an add-on in this estimator.

CLI default P2 is substantially more expensive than explicitly selected H3.1. H3.1 `--model H3.1` normalizes to `v3.1-20260211`; changing to it requires an explicit new model choice and revised approved plan. No P2 spend has been authorized and no model switch has been applied.

Bare geometry has no generated color textures, so its 10-credit comparison does not meet the colored-sample requirement by itself. H3.1 standard colored may be a lower-cost candidate for a real quality trial, but no generated models exist to assess anatomy, topology, materials or game readiness. Prices are offline CLI list estimates, not a live account quote. Neither these API prices nor the arithmetic establish the separate website/Studio package's pricing or what “3000 credits ≈200 models” assumes. Original 1210 was a valid P2 API estimate but should not be presented as the least-cost path.

Evidence: `p2-colored.json`, `h31-colored.json`, `h31-bare.json` contain exact argv and full normalized payloads; `cli-price-table.txt` snapshots the local estimator's source lines.

## Official website cross-check (2026-11-09)

The root agent independently checked these official pages:

- [Tripo Studio价格方案](https://www.tripo3d.ai/zh/pricing): 3000 monthly credits ≈200 models. Its FAQ lists HD generation at 15 credits, optional textures +10; smart mesh at 35, optional P2 +65. “≈200” is the package estimate, not the API P2 standard-colored rate.
- [Tripo API定价](https://developers.tripo3d.ai/zh/pricing): H-series text-to-3D is 10 without textures / 20 with standard textures; P2 is 100 / 110 respectively. API pricing states 1 credit = US$0.01. At that listed rate, 11 standard-colored H-series generations estimate 220 API credits (US$2.20), versus 1210 P2 credits (US$12.10), excluding other operations.
- [Tripo API homepage FAQ](https://developers.tripo3d.ai/zh): “不能。Tripo API 与 Tripo Studio 的计费体系相互独立，需要分别购买。” Studio subscription credits therefore must not be treated as this CLI/API account's balance.

These page observations support the local preflight comparison; counts and dollar amounts remain estimates for the stated generation-only settings, not a purchase or quality guarantee. The original P2 budget has not received user confirmation. No credits have been spent on this sample.
