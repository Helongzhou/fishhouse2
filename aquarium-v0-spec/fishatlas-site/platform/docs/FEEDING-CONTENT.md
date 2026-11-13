# Feeding content — 2026-11-06

The runtime source is `src/data/catalog.json`. It preserves the exported 26 published objects, their locale content, discovery/filter data, photos (including genuine missing photos), and all pair-rule reminders. Designer capability is false and templates are empty. Catalog ID is `catalog-2026-11-06-refocus`; catalog timestamp, locale revisions and the nine edited fish revisions are renewed so the previous immutable release ID does not describe changed data. Existing entity IDs and media hashes are preserved.

## Meaning and limits

`care.feeding` applies only to fish. `pelletDiameterMm` describes a practical **adult starting reference**, not body length, a scientifically established optimum, a hard safe limit, or the only usable food size. It is an editorial choice informed by available commercial particle sizes and feeding form. Product dimensions demonstrate actual food sizes; they do not prove a species-specific optimum. No body-length conversion formula was used. Texture, swelling, individual mouth size and feeding position still matter. Public bilingual advice asks keepers to observe swallowing, use smaller particles when food is repeatedly spat out, and use separate finer foods for fry.

| Fish | Adult starting reference, mm | Form and editorial reasoning |
|---|---:|---|
| Ember tetra / 火焰灯 | 0.2–0.5 | Fine end first for a very small mouth; slowly sinking micro-granules |
| Neon tetra / 霓虹灯 | 0.2–0.5 | Small-mouth micro-granules intercepted in midwater; finely crushed flakes also suitable |
| Harlequin rasbora / 三角灯 | 0.5–0.8 | Small soft, slowly sinking granules for shoaling midwater feeders |
| Honey gourami / 蜜丽丽 | 0.5–0.8 | Small soft floating/slowly sinking food; calm feeding location |
| Guppy / 孔雀鱼 | 0.5–0.8 | Omnivore micro-granules; smaller-mouthed males need observation; not the larger betta pellet default |
| Zebra danio / 斑马鱼 | 0.5–0.8 | Small floating/slowly sinking granules, spread to reduce competition |
| Betta / 暹罗斗鱼 | 1–1.4 | Adult small floating pellet starting range, calibrated to an actual betta food; smaller-mouth individuals start finer |
| Panda corydoras / 熊猫鼠 | 0.5–0.8 | Small softening granules that reach the bottom; target feed and protect slowly sinking food from interception |
| Bristlenose pleco / 胡子异型 | null | Vegetable-rich sinking wafers for rasping; wafer diameter is deliberately not represented as a swallowed pellet diameter |

The 0.2–0.5 and 0.5–0.8 bands use commercial size classes rather than invented decimal precision. Recommendations for individual species within these classes remain practical editorial guidance, not controlled feeding-trial results. Betta's 1–1.4 band is a rounded adult trial range close to the manufacturer's approximate 1.1–1.4 mm; this is not a recommendation to enlarge food when a fish already eats smaller pellets well. Corydoras can also eat suitable soft wafers: the numeric range here is for the small granular alternative only, not an assertion that it must swallow a whole wafer.

## Sources checked

Manufacturer pages retrieved 2026-11-06:

- [Aquarium Münster — DR. BASSLEER FISH FOOD BABY+NANO](https://www.aquarium-munster.com/en/products/food/dr-bassleer-fish-food-babynano/): explicitly describes fine granulated food for nano fish such as small Rasbora, and lists size S at 0.2–0.5 mm. Its generic fish-length chart was not used to calculate species ranges.
- [Aquarium Münster — DR. BASSLEER FISH FOOD REGULAR](https://www.aquarium-munster.com/en/products/food/dr-bassleer-fish-food-regular/): size M 0.5–0.8 mm; describes slowly sinking granules supplying surface, middle and bottom feeders. Used for a commercially grounded small-pellet band, not as proof of an optimum for any listed species.
- [Hikari — Betta Bio-Gold](https://www.hikariusa.com/tropical_folder/betta_bio_gold.html): approximate 1.1–1.4 mm floating pellets; advises smaller bettas use Micro Pellets and observing consumption one pellet at a time. Only feeding form/particle information was used; unrelated care recommendations on the page were not imported.
- [Hikari — Micro Pellets](https://www.hikariusa.com/tropical_folder/micro_pellets.html): explicitly small-mouth tropical fish including tetras; softening, slowly sinking form. The page did not expose a numeric diameter, so no numeric size is attributed to it here.
- [Hikari — Fancy Guppy](https://www.hikariusa.com/tropical_folder/fancy_guppy.html): softening, slowly sinking granules for guppies/livebearers. Supports feeding form; does not establish a numerical optimum.
- [Hikari — Sinking Wafers](https://www.hikariusa.com/tropical_folder/sinking_wafers.html): Corydoras/bottom-feeder food, soft disc, approximately 6.5–7.5 mm. This is a **wafer** dimension and was not copied into pellet diameter.
- [Hikari — Algae Wafers](https://www.hikariusa.com/tropical_folder/algae_wafers.html): disc designed for plecos' natural feeding action, approximately 17–18 mm. Supports the rasping-wafer exception; this is not a 17–18 mm swallowed-pellet recommendation.
- [Hikari — Pellet types](https://www.hikari.info/tec/ep.html): distinguishes disk diets for plecos/Corydoras from tiny granular diets for small tropical fish.
- [sera — Betta Granules](https://www.sera.de/en/product/freshwater-aquarium/sera-betta-granules/): slowly sinking soft fine granules are also a suitable form for labyrinth fish. This supports an alternative feeding form, not a numeric diameter.

No source URL or license entry workflow is added to the public catalog. These notes are internal maintenance context.

## Validation

`tests/catalog/content.test.ts` checks preservation against the pre-simplification export (all original entity fields except edited fish revisions, plus pair rules/categories/parameter definitions/locales), coverage of all nine fish only, positive finite ordered numeric ranges, bilingual advice, the pleco null/wafer exception and corydoras sinking-pellet form. It does not hardcode an exact per-species numeric answer as if that were a scientific invariant.

Verification run: `PATH=/Users/zhouhelong/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin:$PATH pnpm test` — 2 tests passed, 0 failed. The bundled Node path is required in this tool shell; the ordinary shell's `pnpm` shim alone could not find Node.
