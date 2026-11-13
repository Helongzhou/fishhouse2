# SDD ledger — plan: docs/superpowers/plans/2026-11-09-aquatic-taxonomy-compatibility.md

User requested a small set of suitable tank mates and more balanced catalog categories that accommodate freshwater, marine and brackish fish. Earlier authorization to choose recommendations and have Sol implement persists. Root coordinates and accepts; Sol owns product changes.

| Task | Owner | Status |
| --- | --- | --- |
| 1: Contract and content | catalog_runtime (Sol) | Complete; four content tests and independent content review passed |
| 2–3: Taxonomy filters and recommendation UI | catalog_ui (Sol) | Complete; final chip polish independently verified |
| 4: Build / managed production service | catalog_runtime (Sol) | Complete; final managed production build running |
| Independent acceptance | taxonomy_acceptance (fresh Sol) | Accepted; logic 6/6, production 5/5 and final chip checks 4/4 passed |

## Rulings

- Ruling: Reuse the existing non-Git project in place — no Git worktree or commits are possible; retain existing archives and use documented file ownership.
- Ruling: Water type describes ordinary display/keeping conditions, not the complete breeding lifecycle — existing nerite freshwater profile and brackish breeding note can both remain valid.
- Ruling: UI label is 虾螺 / Shrimp & snails, with `type=invertebrate` as the aggregate query — familiar wording fits a hobbyist catalog; old shrimp/snail queries keep exact semantics.
- Ruling: Choose neon–ember, neon–panda and cherry shrimp–ramshorn — avoids the especially narrow panda–ember temperature overlap. Conditions explicitly describe shared temperature and separate shoal/feeding needs.
- Ruling: Rule visibility controls the public recommendation section; the read-only catalog retains original hidden records — no new API confidentiality contract was requested.
- Ruling: Valid `water=marine|brackish` deep links retain their filter and return no results when unavailable; invalid values normalize away — never turn an empty marine search into freshwater matches.
- Ruling: Water options come from active biological entries within the selected type scope, not from search/remaining facet results — options must not vanish because they filter themselves out.
- Ruling: Root has reviewed the contract/content changes; independent reviewer will check the final UI and integration before acceptance. Owners have separate file scopes; no competing production servers.

## Evidence so far

- Builder check resolved exact recommendation pairs, normal-keeping water semantics, hidden-rule API meaning, and aggregate URL compatibility.
- Task 1: 26 entities preserved, 18 freshwater biological profiles, 12 old rules preserved with hidden visibility, 3 new bilingual recommendation records. Four content assertions passed after a recorded initial failure.

- Task 1 accepted: final visible conditions include separate shoals and cory bottom feeding. Independent reviewer confirmed exact original rule/media preservation, unchanged eight material objects, all 18 freshwater entries, and the actual temperature/pH/GH intersections. No content blockers.

- Tasks 2–3 passed 12 focused checks plus typecheck; integration passed 16 tests, typecheck/build and 13 HTTP checks. Independent review passed 6 logic and 5 production interaction groups.
- Root visually checked 1280px library/type controls, shrimp→snail recommendation→original list with water/type preserved, and 390px filter layout. No clipping or broken links observed.
- Final review P3: legacy shrimp/snail scopes were visually indistinguishable from the parent aggregate. UI now adds a removable atom-type chip only for those old URLs; removal returns to invertebrate and preserves other query state. Nine focused taxonomy checks and typecheck passed; awaiting final production rebuild/recheck.

## Final acceptance

- Final UI chip change passed nine focused taxonomy checks and typecheck; runtime stopped the old service, built, typechecked and restarted successfully. Five final HTTP checks passed without repeating unchanged suites.
- Independent final check: four cases (Chinese/English × shrimp/snail), visible chip, removal to invertebrate, preserved water/search/sort, no extra chip for aggregate, 390px no overflow. No outstanding findings.
- Root accepted actual IAB desktop and mobile views and recommendation navigation; final screenshot `platform/qa-artifacts/taxonomy-compatibility/library-final.jpg` was saved and visually inspected.
- Runtime remains local at http://127.0.0.1:4174/zh-CN/aquatic; 4173 redirects with path/query preserved. Final web PID 35758 and redirect PID 35760. PostgreSQL remains stopped.
- Delivery accepted 2026-11-09. Current data has freshwater entries; marine/brackish are supported in schema/filter behavior and appear as UI options when those records are added. No new species content was fabricated.
