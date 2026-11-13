# Catalog UI delivery

2026-11-06. UI source frozen for production build and independent acceptance.

## Changed

- Kept the established warm white, green and real aquarium photography direction. Homepage now leads with search, four organism kinds, then six featured records with care facts. Removed template section and all designer/plan CTAs.
- Header links to aquatic library and material reference. Removed planner claims from homepage metadata, footer and load-error copy.
- Removed active `designer.tsx`, `scene.tsx`, `modal.tsx`, `TemplatePreview`, planner-only CSS and `templateName` UI re-export after runtime owner confirmed the full-source archive.
- `/{locale}/designer` and root `?view=calculator&lang=…` redirect to localized aquatic library. No planner UI remains reachable from site components.
- Fish-only `care.feeding` is rendered inside the existing feeding care row. Numeric ranges use “颗粒直径 … mm” / “Pellet diameter … mm” with “成鱼参考” / “Adult reference” and bilingual advice. A null wafer profile uses “啃食沉底片” / “Grazing on sinking wafers”; it never displays an empty or zero numeric value. Missing feeding profile does not generate placeholder UI. Existing feeding note stays beside the new guidance.
- Preserved library filters and their dimensions, three-column desktop layout, responsive list, material information, return URLs, scroll behavior and language switching. No package/server/CMS/data edits by this owner.

## Checked

Read platform AGENTS and installed Next redirect / metadata docs. Applied installed frontend-design direction and fetched current Web Interface Guidelines. Source review found the nested feeding guidance would inherit the old `.care-rows div` row grid; narrowed that selector to `.care-rows > div` before freeze.

New source was exercised on temporary dev `http://127.0.0.1:4175`, not the old production process. `qa-artifacts/catalog-ui/check.mjs` and `results.json`: **6/6 PASS**, installed headless desktop Google Chrome, 1440 px desktop and 390/320 px mobile viewport emulation:

1. Chinese and English homepage, real hero image, four taxonomy links, six featured entries, no planner links.
2. Fish entry navigates to `?type=fish`, nine fish results, three desktop columns and expanded desktop filters.
3. Valid `type=fish&social=schooling&body=slender` URL survives detail/back navigation and language switching. Taxonomy remains distinct from social/body facets.
4. Both localized retired designer URLs and legacy English calculator query redirect to the aquatic library.
5. Chinese/English homepage, fish list, English detail and materials fit 390/320 px without horizontal overflow. Mobile filters default collapsed and can open.
6. No page exceptions during the checks.

Desktop Chinese and 320 px Chinese homepage screenshots were visually inspected; bilingual 1440 and mobile 390/320 screenshots are in `qa-artifacts/catalog-ui/`. Screenshots temporarily force preexisting offscreen `content-visibility` cards to render and hide the Next dev badge; no product CSS is changed by the screenshot helper.

## Final acceptance handoff

Content owner had not yet frozen feeding types/data when these six UI checks ran, so numeric feeding DOM coverage is prepared but not claimed as executed here. The root and independent reviewer own final production build, all nine fish × two languages, wafer exception and non-fish exclusion checks. Enable that extra DOM check after production restart with `CATALOG_UI_BASE=http://127.0.0.1:4174 CATALOG_UI_FEEDING=1` when running `qa-artifacts/catalog-ui/check.mjs` with the bundled Node runtime.

Initial return-URL probe mistakenly used unsupported `body=streamlined`; normalization correctly removed it. Replaced the fixture with supported `body=slender` and the final run passed. This was a test fixture correction, not a filter behavior change.

Mobile checks are viewport simulations, not real-device evidence. Full screen-reader/Safari testing is outside this focused delivery. The old FRONTEND-VERIFICATION document describes the now-retired scope and is historical; this document is the current UI handoff.

## Production acceptance follow-up

Root found shortcut filter text/background contrast, repeated detail care-summary prose and untranslated diet values in production. Fixed and source frozen again for runtime owner's unified build/restart:

- Shortcut button styling now explicitly sets light green background and dark green text; hover increases contrast, active/applied state uses deep green and white, keyboard focus keeps a visible green outline. `aria-pressed` reflects matching shortcut query conditions. Active filter chip hover also has explicit contrasting colors. The regression came from a shared text-button base rule being inside the removed planner CSS; scoped shortcut rules now fully own their colors.
- Structured care-row sentences are removed from the separate care-summary remainder. Pure duplicate summaries disappear; distinct remaining summary text, summaries without structured notes, and material preparation remain renderable.
- Detail diet calls the existing library-filter localization helper, covering omnivore, grazer, carnivore and herbivore in both languages.

Targeted Chrome check on new source at temporary 4175: **4/4 PASS**, `qa-artifacts/catalog-ui/followup-check.mjs` / `followup-results.json`. Tested shortcut default/hover/focus/active/applied states in zh/en at 1280 px; measured text contrast 8.06–12.44:1 (all visible filter buttons ≥4.5:1). Checked neon tetra, cherry shrimp, bristlenose pleco and betta in both languages for one rendering per structured care sentence, localized diet and retained fish-only feeding; branch wood preparation and anubias description remain present. No page exceptions. Temporary dev process stopped before runtime build handoff. Root/independent reviewer owns final production confirmation.
