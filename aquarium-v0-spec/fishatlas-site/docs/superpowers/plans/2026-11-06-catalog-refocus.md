# Catalog Refocus Implementation Plan

> Execute with Sol owners and independent acceptance. User explicitly authorized removal and implementation; no repeated approval step.

**Goal:** Remove the aquarium designer and admin system; deliver a focused bilingual aquatic catalog with useful pellet-diameter feeding guidance.

**Architecture:** Keep Next/React catalog UI and photos. Replace Payload/Postgres publication reads with a local published catalog JSON. Retired code/database are recoverably archived, outside the active runtime; do not erase the user's saved browser plans. Public navigation and homepage focus on discovering and caring for aquatic life.

**Tech Stack:** Existing Next16/React19/TypeScript; remove Payload/Postgres/GraphQL CMS dependencies from the application.

**Spec:** User's current three instructions supersede the prior designer/CMS scope. Keep existing filters, details, bilingual behavior, photos, material information and URLs that still apply.

## Shared constraints

- Working app: fishatlas-site/platform. No Git repository. Original prototype/unrelated projects remain untouched.
- Public catalog baseline: docs/implementation/inputs/2026-11-06-catalog-before-simplify.json (26 objects).
- Runtime source: platform/src/data/catalog.json, owned by content agent. Backend reads it through existing getPublicRelease signature so frontend migration stays small.
- Optional fish-only care.feeding: { pelletDiameterMm: Range | null; foodForm: 'micro-pellet'|'pellet'|'sinking-pellet'|'wafer'; lifeStage: 'adult'; advice: Bilingual }. Null for wafer/grazing use where swallowed-pellet diameter is not the right measure. Display units mm, practical reference wording, observe swallowing/size variation; fry need separate guidance. No invented scientific precision or new source/license-entry workflow.
- UI designer/calculator old links redirect to localized library; /admin and CMS/release mutation APIs removed and return404/410. No hidden functioning CMS or designer.
- Root coordinates and accepts; Sol owns product edits. Three independent owners max plus root; disjoint paths.

## Tasks

1. **Runtime removal — catalog_runtime**: own package/config/lock, src/server, src/app/(payload), src/app/api, media route, scripts, README. Archive retired server/CMS/source/config/tests before removing active modules. Replace database reads with local catalog; remove CMS/dependencies/API routes, safely stop project PostgreSQL after backup. Keep persistent service and 4173 redirect, old pictures available. Build/restart after other owners freeze. Verify catalog works with DB stopped and retired APIs unavailable.
2. **Content — catalog_feeding**: own src/data/catalog.json, src/domain/types.ts, content notes/test only. Copy exported active catalog, disable designer capability/clear templates; keep pair-rule reminders for detail. Add justified practical per-fish pellet diameter and bilingual advice for all nine fish; explicitly distinguish bottom feeders/wafers. Coordinate type with frontend. Preserve existing data and actual photo gaps. Internal research notes may cite manufacturer or husbandry references without adding user entry requirements.
3. **UI — catalog_ui**: own src/app/(site), src/components/site, src/styles. Apply existing frontend-design direction, remove all planner CTAs/template cards/plan UI and SEO claims; remove active designer components, retire route to localized library redirect. Home should lead search, organism categories, useful browsing and featured organisms. Keep three-column responsive catalog and old filters. Feeding guidance integrated into existing feeding area, not a parameter wall. Preserve language/filter return paths and no hidden fallback.
4. **Acceptance — root + fresh Sol reviewer**: independently verify removed endpoints/links, database-free production, fish-specific numeric guidance and wafer exception, zh/en, desktop/mobile, filters/return paths, no runtime errors. Test only relevant changes. Review reports before declaring local delivery.

## Review focus

- Stale navigation/metadata/legacy query adapters must not resurrect the removed features.
- Static catalog must preserve the latest published 26 records and photo paths without database fallback.
- Diameter guidance is not fish body length, wafer diameter or a universal safety limit.
- Changes to fish feeding must not add fish-only fields to shrimp/snails/plants/materials.
- Build/start must survive agent termination and preserve existing local URLs.
