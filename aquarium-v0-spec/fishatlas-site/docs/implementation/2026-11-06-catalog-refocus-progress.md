# Execution ledger — catalog refocus

Plan: docs/superpowers/plans/2026-11-06-catalog-refocus.md

User explicitly rejected the designer and CMS and authorized Sol/Terra implementation of their removal plus fish pellet-size guidance. This supersedes the prior platform scope; no repeated design approval is needed.

| Owner | Scope | State |
| --- | --- | --- |
| catalog_runtime (Sol) | Archive and remove CMS/designer runtime dependencies; file catalog; build/start | Complete; final production build running |
| catalog_feeding (Sol) | Preserve published catalog; practical fish-specific feeding sizes and bilingual copy | Complete; 2/2 content tests pass |
| catalog_ui (Sol) | Catalog-first homepage/navigation/details; remove designer; show feeding guidance | Complete; acceptance fixes verified |
| Root + independent reviewer | Integration, production endpoints, content and visual acceptance | Accepted on production 4174 |

## Decisions

- File catalog replaces live Payload/Postgres reads. Tiny read-only catalog/entity/deployment compatibility APIs may remain; CMS and publication mutation endpoints do not.
- Materials remain useful reference content. Old designer/calculator URLs lead to the localized catalog.
- Preserve saved browser data and database backup, without shipping a functioning hidden designer/CMS.
- Pellet diameter advice applies to adult fish; wafers intended for grazing must not be presented as swallowed-pellet diameter. No source/license-entry workflow.
- Archive completed before removal: platform/.runtime/retired-catalog-refocus/platform-before-refocus.tgz (0600). Database/media backup retained in platform/.runtime/backups with timestamp20261106-153624.
- Three owners have non-overlapping write paths. Root handles coordination/docs/verification; Sol agents implement product changes.

## Acceptance evidence

- Runtime: build/typecheck pass, 58/58 production HTTP checks pass, dedicated PostgreSQL stopped, all 75 media references verified. Active catalog ID: `catalog-2026-11-06-refocus`. Evidence: `platform/.runtime/evidence/catalog-runtime-http.json`.
- Independent Sol review: production browser suite 7/7 pass, including all nine fish in both languages, wafer exception and non-fish exclusion. Additional independent checks 6/6 pass. Final report: `platform/qa-artifacts/catalog-refocus-review/report.md`.
- Root visual checks: actual homepage and desktop three-column aquatic library, tetra filter, filtered detail return URL, Chinese and English feeding text, and 390px feeding layout checked in IAB. No browser errors found.
- Root found low-contrast shortcut buttons, a duplicate aggregate care paragraph, and an untranslated diet enum. UI owner fixed all three; final production rebuilt after stopping the service, followed by successful typecheck and restart. Final HTTP smoke 10/10 pass.
- Independent final UI check 2/2 pass: 30 button samples minimum text contrast 4.8:1; 24 bilingual care detail pages retain distinct notes and feeding, remove duplicate summaries, and localize diet. No unresolved blocking findings.
- Root rechecked the final production shortcut appearance, Chinese feeding row and diet label, and 390px feeding layout. Screenshots: `platform/qa-artifacts/catalog-refocus-final/home.jpg`, `feeding-desktop.jpg`, `feeding-mobile.jpg`.

## Delivery

Accepted locally on 2026-11-06. Running homepage: http://127.0.0.1:4174/zh-CN; 4173 redirects to 4174 while retaining path and query. Final managed process IDs at acceptance: web 54700, redirect 54701. The project database remains stopped. All implementation owners are frozen; no additional work required for this scope.
