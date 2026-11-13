# FishAtlas data contract

P0 entities are stored as JSON arrays in `data/` so the first frontend can run without a database. The files map to the future CMS entities:

- `species.json`: stable identity and taxonomy.
- `species_localizations.json`: one record per species and locale, including localized labels for the fish-library filters.
- `care_profiles.json`: display fields plus reserved `calculator_*` fields for P1.
- `care_guides.json`: two shared bilingual care guides for new arrivals and water maintenance.
- `compatibility_rules.json`: manually written relationship summaries, with optional `reason_localized` and `conditions_localized` maps for the P0 locales.
- `content_notes.json`: editorial notes and uncertainty labels.
- `media_assets.json`: formal image, placeholder, or no-image state.
- `tank_profiles.json` and `tank_plan_items.json`: empty P1 calculator storage placeholders.

The P1 calculator fields are estimates for this site. They are not a universal aquarium stocking formula and are not used by the P0 validator beyond type/range checks.

Free-text care and compatibility fields should provide `en` and `zh-CN` values before publication; the frontend falls back to English only for an incomplete draft.

## Fish-library filter fields

`species.json` stores the shared, language-neutral filter values:

- `organism_type`: `fish`, `shrimp`, or `snail`.
- `category_tags[]`: `tetra`, `rasbora`, `barb`, `danio`, `pleco`, `corydoras`, `gourami`, `betta`, `cichlid`, `livebearer`, `catfish`, `shrimp`, `snail`, `other`. Chinese display: `rasbora` = 波鱼类, `barb` = 鲃类, `danio` = 斑马鱼类, `pleco` = 异型, `catfish` = 鲶鱼类, `betta` = 斗鱼.
- `primary_colors[]`: `red`, `orange`, `yellow`, `blue`, `green`, `black`, `white`, `silver`, `transparent`, `brown`, `mixed`.
- `size_class`: `small`, `medium`, or `large`. It is checked against `care_profiles.adult_length_cm` using `<=4 cm`, `>4–8 cm`, and `>8 cm` thresholds.
- `reproduction_mode`: `egg_laying`, `livebearing`, or `unknown`.
- `social_pattern[]`: `schooling`, `pairing`, `solitary_tendency`, `colony`, or `unknown`.
- `body_shape_tags[]`: `slender`, `deep_bodied`, `laterally_compressed`, `short_round`, `elongated`, or `unknown`. Bottom dwelling is represented by `water_layers`, not body shape.

`care_profiles.water_layers[]` is a nonempty set of `top`, `mid`, and `bottom`; the existing `water_layer` is the primary layer and must appear in it. `husbandry` separates oxygen need (`standard`, `high`, `unknown`) from preferred flow (`minimal`, `gentle`, `moderate`, `strong`). `standard` still requires adequate dissolved oxygen, and `minimal` does not mean stagnant water. Unknown flow, substrate, habitat, feeding-zone, or food-form tags use an empty array; unknown minimum tank length uses `null` rather than a value inferred from liters. The six bilingual `notes` fields may be empty strings when no species-specific detail is known. Alerts use stable codes, bilingual text, and `caution` or `important` severity.

`group_mode` controls interpretation of the retained `school_min` and `school_recommended` numbers: `shoal` uses minimum and recommended group sizes, `colony` and `loose_group` use a suggested starting group, `single` means one individual, and `conditional_pair` means one is acceptable with a pair considered only under suitable conditions. These fields do not change calculator inputs.

Multi-value arrays are deduplicated. `unknown` is only valid by itself for social and body-shape fields, so an unknown value cannot be mistaken for a known match. Each species has `en` and `zh-CN` `filter_labels` in `species_localizations.json`; these are display text only and must have the same item counts as the corresponding shared arrays.

The four added prototype profiles (guppy, betta, zebra danio, bristlenose pleco) contain care suggestions marked `needs_review`. Their calculators are disabled and unavailable; unknown calculator values stay `null` rather than receiving invented load estimates. Enabled or ready calculator profiles must still provide valid numeric inputs. These additions do not create compatibility rules.

Validation (2026-11-05): Python and the bundled Node runtime both pass for 10 species, 20 localizations, 10 care profiles, and 4 compatibility rules. The Node validator uses `fileURLToPath` so workspace paths containing spaces resolve correctly.
